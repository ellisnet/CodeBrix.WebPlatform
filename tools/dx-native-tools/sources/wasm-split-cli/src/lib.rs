use anyhow::{Context, Result};
use itertools::Itertools;
use rayon::prelude::{IntoParallelIterator, ParallelIterator};
use std::{
    collections::{BTreeMap, BTreeSet, HashMap, HashSet, VecDeque},
    hash::Hash,
    ops::Range,
    sync::{Arc, RwLock},
};
use walrus::{
    ir::{self, dfs_in_order, Visitor},
    ConstExpr, DataKind, ElementItems, ElementKind, ExportId, ExportItem, FunctionBuilder,
    FunctionId, FunctionKind, GlobalKind, ImportId, ImportKind, Module, ModuleConfig, RefType,
    TableId, TypeId,
};
use wasmparser::{
    BinaryReader, Linking, LinkingSectionReader, Payload, RelocSectionReader, RelocationEntry,
    SymbolInfo,
};

pub const MAKE_LOAD_JS: &str = include_str!("./__wasm_split.js");

/// CodeBrix change: new with patch 0007. How the code and data that several split modules share
/// (and the main module does not hold) are grouped into shared chunks. The default is upstream's
/// behaviour as patch 0006 completed it: ONE chunk holds all of it. See
/// tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0007.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SplitOptions {
    /// `None`: one chunk for all shared code. `Some(bytes)`: the shared symbols are grouped by
    /// the exact set of split modules that use them, and a group whose code and data, measured
    /// before optimisation, come to at least this many bytes gets a chunk of its own, which only
    /// the split modules that use it fetch. Smaller groups go into the chunk of the smallest
    /// larger group that every one of their users also uses, else into the common chunk (chunk
    /// 0). At least 1.
    pub chunk_threshold: Option<usize>,

    /// The most shared chunks, the common chunk included: when more groups reach the threshold,
    /// the largest keep their own chunks and the others are grouped as the smaller ones are. At
    /// least 1; 1 gives the one chunk of the default.
    pub chunk_limit: usize,
}

impl SplitOptions {
    /// The chunk limit when none is given.
    pub const DEFAULT_CHUNK_LIMIT: usize = 16;
}

impl Default for SplitOptions {
    fn default() -> Self {
        Self {
            chunk_threshold: None,
            chunk_limit: Self::DEFAULT_CHUNK_LIMIT,
        }
    }
}

/// A parsed wasm module with additional metadata and functionality for splitting and patching.
///
/// This struct assumes that relocations will be present in incoming wasm binary.
/// Upon construction, all the required metadata will be constructed.
pub struct Splitter<'a> {
    /// The original module we use as a reference
    source_module: Module,

    // The byte sources of the pre and post wasm-bindgen .wasm files
    // We need the original around since wasm-bindgen ruins the relocation locations.
    original: &'a [u8],
    bindgened: &'a [u8],

    // Mapping of indices of source functions
    // This lets us use a much faster approach to emitting split modules simply by maintaining a mapping
    // between the original Module and the new Module. Ideally we could just index the new module
    // with old FunctionIds but the underlying IndexMap actually checks that a key belongs to a particular
    // arena.
    fns_to_ids: HashMap<FunctionId, usize>,
    _ids_to_fns: Vec<FunctionId>,

    shared_symbols: BTreeSet<Node>,
    split_points: Vec<SplitPoint>,
    chunks: Vec<HashSet<Node>>,
    data_symbols: BTreeMap<usize, DataSymbol>,
    main_graph: HashSet<Node>,
    call_graph: HashMap<Node, HashSet<Node>>,
    parent_graph: HashMap<Node, HashSet<Node>>,
    // CodeBrix change: how shared code is grouped into chunks (patch 0007).
    options: SplitOptions,
}

/// The results of splitting the wasm module with some additional metadata for later use.
pub struct OutputModules {
    /// The main chunk
    pub main: SplitModule,

    /// The modules of the wasm module that were split.
    pub modules: Vec<SplitModule>,

    /// The chunks that might be imported by the main modules
    pub chunks: Vec<SplitModule>,
}

/// A wasm module that was split from the main module.
///
/// All IDs here correspond to *this* module - not the parent main module
pub struct SplitModule {
    pub module_name: String,
    pub hash_id: Option<String>,
    pub component_name: Option<String>,
    pub bytes: Vec<u8>,
    // CodeBrix change: an ordered set, so that dx lists a module's chunks in the loader in one
    // fixed order now that there can be several (patch 0007; see patch 0003).
    pub relies_on_chunks: BTreeSet<usize>,
}

impl<'a> Splitter<'a> {
    /// Create a new "splitter" instance using the original wasm and the wasm from the output of wasm-bindgen.
    ///
    /// This will use the relocation data from the original module to create a call graph that we
    /// then use with the post-bindgened module to create the split modules.
    ///
    /// It's important to compile the wasm with --emit-relocs such that the relocations are available
    /// to construct the callgraph.
    pub fn new(original: &'a [u8], bindgened: &'a [u8]) -> Result<Self> {
        // CodeBrix change: the default options give upstream's one shared chunk (patch 0007).
        Self::new_with_options(original, bindgened, SplitOptions::default())
    }

    /// CodeBrix change: new with patch 0007. As [`Splitter::new`], with the grouping of shared
    /// code into chunks set by `options`. Fails when the threshold or the limit is 0.
    pub fn new_with_options(
        original: &'a [u8],
        bindgened: &'a [u8],
        options: SplitOptions,
    ) -> Result<Self> {
        anyhow::ensure!(
            options.chunk_limit >= 1,
            "the shared chunk limit must be at least 1"
        );
        anyhow::ensure!(
            options.chunk_threshold != Some(0),
            "the shared chunk threshold must be at least 1 byte"
        );

        let (module, ids, fns_to_ids) = parse_module_with_ids(bindgened)?;

        let split_points = accumulate_split_points(&module);

        // Note that we can't trust the normal symbols - just the data symbols - and we can't use the data offset
        // since that's not reliable after bindgening
        let raw_data = parse_bytes_to_data_segment(bindgened)?;

        let mut module = Self {
            source_module: module,
            original,
            bindgened,
            split_points,
            data_symbols: raw_data.data_symbols,
            _ids_to_fns: ids,
            fns_to_ids,
            main_graph: Default::default(),
            chunks: Default::default(),
            call_graph: Default::default(),
            parent_graph: Default::default(),
            shared_symbols: Default::default(),
            options,
        };

        module.build_call_graph()?;
        module.build_split_chunks();

        Ok(module)
    }

    /// Split the module into multiple modules at the boundaries of split points.
    ///
    /// Note that the binaries might still be "large" at the end of this process. In practice, you
    /// need to push these binaries through wasm-bindgen and wasm-opt to take advantage of the
    /// optimizations and splitting. We perform a few steps like zero-ing out the data segments
    /// that will only be removed by the memory-packing step of wasm-opt.
    ///
    /// This returns the list of chunks, an import map, and some javascript to link everything together.
    pub fn emit(self) -> Result<OutputModules> {
        tracing::info!("Emitting split modules.");

        let chunks = (0..self.chunks.len())
            .into_par_iter()
            .map(|idx| self.emit_split_chunk(idx))
            .collect::<Result<Vec<SplitModule>>>()?;

        let modules = (0..self.split_points.len())
            .into_par_iter()
            .map(|idx| self.emit_split_module(idx))
            .collect::<Result<Vec<SplitModule>>>()?;

        // Emit the main module, consuming self since we're going to
        let main = self.emit_main_module()?;

        Ok(OutputModules {
            modules,
            chunks,
            main,
        })
    }

    /// Emit the main module.
    ///
    /// This will analyze the call graph and then perform some transformations on the module.
    /// - Clear out active segments that the split modules will initialize
    /// - Wipe away unused functions and data symbols
    /// - Re-export the memories, globals, and other items that the split modules will need
    /// - Convert the split module import functions to real functions that call the indirect function
    ///
    /// Once this is done, all the split module functions will have been removed, making the main module smaller.
    ///
    /// Emitting the main module is conceptually pretty simple. Emitting the split modules is more
    /// complex.
    fn emit_main_module(mut self) -> Result<SplitModule> {
        tracing::info!("Emitting main bundle split module");

        // Perform some analysis of the module before we start messing with it
        let unused_symbols = self.unused_main_symbols();

        // Use the original module that contains all the right ids
        let mut out = std::mem::take(&mut self.source_module);

        // 1. Clear out the active segments that try to initialize functions for modules we just split off.
        //    When the side modules load, they will initialize functions into the table where the "holes" are.
        self.replace_segments_with_holes(&mut out, &unused_symbols);

        // 2. Wipe away the unused functions and data symbols
        self.prune_main_symbols(&mut out, &unused_symbols)?;

        // 3. Change the functions called from split modules to be local functions that call the indirect function
        self.create_ifunc_table(&mut out);

        // 4. Re-export the memories, globals, and other stuff
        self.re_export_items(&mut out);

        // 6. Remove the reloc and linking custom sections
        self.remove_custom_sections(&mut out);

        // 7. Run the garbage collector to remove unused functions
        walrus::passes::gc::run(&mut out);

        Ok(SplitModule {
            module_name: "main".to_string(),
            component_name: None,
            bytes: out.emit_wasm(),
            relies_on_chunks: Default::default(),
            hash_id: None,
        })
    }

    /// Write the contents of the split modules to the output
    fn emit_split_module(&self, split_idx: usize) -> Result<SplitModule> {
        let split = self.split_points[split_idx].clone();

        // These are the symbols that will only exist in this module and not in the main module.
        let mut unique_symbols = split
            .reachable_graph
            .difference(&self.main_graph)
            .cloned()
            .collect::<HashSet<_>>();

        // The functions we'll need to import
        let mut symbols_to_import: HashSet<_> = split
            .reachable_graph
            .intersection(&self.main_graph)
            .cloned()
            .collect();

        // Identify the functions we'll delete
        let symbols_to_delete: HashSet<_> = self
            .main_graph
            .difference(&split.reachable_graph)
            .cloned()
            .collect();

        // Convert split chunk functions to imports
        let mut relies_on_chunks = BTreeSet::new();
        for (idx, chunk) in self.chunks.iter().enumerate() {
            let nodes_to_extract = unique_symbols
                .intersection(chunk)
                .cloned()
                .collect::<Vec<_>>();
            for node in nodes_to_extract {
                if !self.main_graph.contains(&node) {
                    unique_symbols.remove(&node);
                    symbols_to_import.insert(node);
                    relies_on_chunks.insert(idx);
                }
            }
        }

        tracing::info!(
            "Emitting module {}/{} {}: {:?}",
            split_idx,
            self.split_points.len(),
            split.module_name,
            relies_on_chunks
        );

        let (mut out, ids_to_fns, _fns_to_ids) = parse_module_with_ids(self.bindgened)?;

        // Remap the graph to our module's IDs
        let shared_funcs = self
            .shared_symbols
            .iter()
            .map(|f| self.remap_id(&ids_to_fns, f))
            .collect::<Vec<_>>();

        // CodeBrix change: the functions of every shared chunk, in the order of their table
        // slots, with this module's ids (patch 0006, see chunk_functions).
        let chunk_funcs = self.remapped_chunk_functions(&ids_to_fns);

        let unique_symbols = self.remap_ids(&unique_symbols, &ids_to_fns);
        let symbols_to_delete = self.remap_ids(&symbols_to_delete, &ids_to_fns);
        let symbols_to_import = self.remap_ids(&symbols_to_import, &ids_to_fns);
        let split_export_func = ids_to_fns[self.fns_to_ids[&split.export_func]];

        // Do some basic cleanup of the module to make it smaller
        // This removes exports, imports, and the start function
        self.prune_split_module(&mut out);

        // Clear away the data segments
        self.clear_data_segments(&mut out, &unique_symbols);

        // Clear out the element segments and then add in the initializers for the shared imports
        self.create_ifunc_initializers(&mut out, &unique_symbols);

        // Convert our split module's functions to real functions that call the indirect function
        self.add_split_imports(
            &mut out,
            split.index,
            split_export_func,
            split.export_name,
            &symbols_to_import,
            &shared_funcs,
            &chunk_funcs,
        );

        // Delete all the functions that are not reachable from the main module
        self.delete_main_funcs_from_split(&mut out, &symbols_to_delete);

        // Remove the reloc and linking custom sections
        self.remove_custom_sections(&mut out);

        // Run the gc to remove unused functions - also validates the module to ensure we can emit it properly
        // todo(jon): prefer to delete the items as we go so we don't need to run a gc pass. it/it's quite slow
        walrus::passes::gc::run(&mut out);

        Ok(SplitModule {
            bytes: out.emit_wasm(),
            module_name: split.module_name.clone(),
            component_name: Some(split.component_name.clone()),
            relies_on_chunks,
            hash_id: Some(split.hash_name.clone()),
        })
    }

    /// Write a split chunk - this is a chunk with no special functions, just exports + initializers
    fn emit_split_chunk(&self, idx: usize) -> Result<SplitModule> {
        tracing::info!("emitting chunk {}", idx);

        let unique_symbols = &self.chunks[idx];

        // CodeBrix change: everything the chunk's symbols reach. Upstream took the chunk's own
        // symbols here, of which none is in the main module, so it imported nothing and deleted
        // EVERY function of the main module from the chunk - also the ones the chunk's functions
        // call, and walrus panicked ("assertion failed: !self.dead.contains(&id)") as soon as a
        // chunk had a function. A chunk is now treated like a split module: the main module's
        // functions it reaches become calls through the function table, the rest of the main
        // module's functions are deleted. (What a chunk's symbol reaches is in the main module
        // or in the chunk: every split module that reaches the symbol reaches it too.) For an
        // empty chunk both sets are what they were. See
        // tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0006.
        let chunk_reachable = reachable_graph(&self.call_graph, unique_symbols);

        // The functions we'll need to import
        //
        // CodeBrix change: also what the chunk reaches in ANOTHER chunk, which becomes calls
        // through that chunk's table slots (convert_chunk_funcs_to_imports below). With only the
        // main module's symbols here, a chunk kept a private copy of every function of another
        // chunk it calls. See tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0007.
        let symbols_to_import: HashSet<_> = chunk_reachable
            .iter()
            .filter(|node| {
                self.main_graph.contains(node)
                    || self
                        .chunks
                        .iter()
                        .enumerate()
                        .any(|(other, chunk)| other != idx && chunk.contains(node))
            })
            .cloned()
            .collect();

        // Delete everything except the symbols that are reachable from this module
        let symbols_to_delete: HashSet<_> = self
            .main_graph
            .difference(&chunk_reachable)
            .cloned()
            .collect();

        // Make sure to remap any ids from the main module to this module
        let (mut out, ids_to_fns, _fns_to_ids) = parse_module_with_ids(self.bindgened)?;

        // Remap the graph to our module's IDs
        let shared_funcs = self
            .shared_symbols
            .iter()
            .map(|f| self.remap_id(&ids_to_fns, f))
            .collect::<Vec<_>>();

        // CodeBrix change: this chunk's functions in the order of their table slots, with this
        // module's ids (patch 0006, see chunk_functions).
        let chunk_funcs = self.remapped_chunk_functions(&ids_to_fns);

        let unique_symbols = self.remap_ids(unique_symbols, &ids_to_fns);
        let symbols_to_import = self.remap_ids(&symbols_to_import, &ids_to_fns);
        let symbols_to_delete = self.remap_ids(&symbols_to_delete, &ids_to_fns);

        self.prune_split_module(&mut out);

        // Clear away the data segments
        self.clear_data_segments(&mut out, &unique_symbols);

        // Clear out the element segments and then add in the initializers for the shared imports
        self.create_ifunc_initializers(&mut out, &unique_symbols);

        // We have to make sure our table matches that of the other tables even though we don't call them.
        let ifunc_table_id = self.load_funcref_table(&mut out);
        // CodeBrix change: the table also has the slots of the chunks' functions (patch 0006).
        let segment_start = self
            .expand_ifunc_table_max(&mut out, ifunc_table_id, self.ifunc_table_growth())
            .unwrap();

        self.convert_shared_to_imports(&mut out, segment_start, &shared_funcs, &symbols_to_import);

        // CodeBrix change: the chunk fills the table slots of its own functions, through which
        // the split modules call them, and calls the functions of any other chunk through
        // theirs (patch 0006).
        self.create_chunk_initializers(&mut out, segment_start, idx, &chunk_funcs);
        self.convert_chunk_funcs_to_imports(
            &mut out,
            segment_start,
            &chunk_funcs,
            &symbols_to_import,
            Some(idx),
        );

        // Make sure we haven't deleted anything important....
        self.delete_main_funcs_from_split(&mut out, &symbols_to_delete);

        // Remove the reloc and linking custom sections
        self.remove_custom_sections(&mut out);

        // Run the gc to remove unused functions - also validates the module to ensure we can emit it properly
        walrus::passes::gc::run(&mut out);

        Ok(SplitModule {
            bytes: out.emit_wasm(),
            module_name: "split".to_string(),
            component_name: None,
            relies_on_chunks: Default::default(),
            hash_id: None,
        })
    }

    /// CodeBrix change: new with patch 0006. The functions of a shared chunk, in a fixed order
    /// (that of their ids in the source module). The position of a function in this list decides its
    /// slot in the function table, so the main module, the chunk and every split module must
    /// take the list from here.
    fn chunk_functions(&self, idx: usize) -> Vec<Node> {
        self.chunks[idx]
            .iter()
            .filter(|node| matches!(node, Node::Function(_)))
            .copied()
            .sorted_unstable()
            .collect()
    }

    /// CodeBrix change: new with patch 0006. chunk_functions of every chunk, with the ids of
    /// the module `ids_to_fns` belongs to.
    fn remapped_chunk_functions(&self, ids_to_fns: &[FunctionId]) -> Vec<Vec<Node>> {
        (0..self.chunks.len())
            .map(|idx| {
                self.chunk_functions(idx)
                    .iter()
                    .map(|node| self.remap_id(ids_to_fns, node))
                    .collect()
            })
            .collect()
    }

    /// CodeBrix change: new with patch 0006. How many slots the splitter adds at the end of the
    /// function table: upstream's `split_points.len() + shared_symbols.len()` - one slot for
    /// each split point's entry function and one for each function the split modules call in
    /// the main module (the data symbols in `shared_symbols` are counted too and their slots
    /// stay empty, as upstream has it) - and then one slot for each function of each shared
    /// chunk. The main module, every chunk and every split module must declare the same table,
    /// so all of them take the number from here.
    fn ifunc_table_growth(&self) -> usize {
        let chunk_functions: usize = (0..self.chunks.len())
            .map(|idx| self.chunk_functions(idx).len())
            .sum();
        self.split_points.len() + self.shared_symbols.len() + chunk_functions
    }

    /// CodeBrix change: new with patch 0006. The first of the added table slots that belongs to
    /// the functions of the shared chunks, counted from the first added slot.
    fn chunk_slots_start(&self) -> usize {
        self.split_points.len() + self.shared_symbols.len()
    }

    /// CodeBrix change: new with patch 0006. In a chunk, fill the table slots of the chunk's own
    /// functions (one element segment; none for a chunk without functions). The slots of chunk
    /// `idx` follow those of the chunks before it.
    fn create_chunk_initializers(
        &self,
        out: &mut Module,
        segment_start: usize,
        idx: usize,
        chunk_funcs: &[Vec<Node>],
    ) {
        let functions: Vec<FunctionId> = chunk_funcs[idx]
            .iter()
            .filter_map(|node| match node {
                Node::Function(id) => Some(*id),
                Node::DataSymbol(_) => None,
            })
            .collect();
        if functions.is_empty() {
            return;
        }

        let before: usize = chunk_funcs[..idx].iter().map(|funcs| funcs.len()).sum();
        let offset = segment_start + self.chunk_slots_start() + before;
        let ifunc_table_id = self.load_funcref_table(out);
        out.tables
            .get_mut(ifunc_table_id)
            .elem_segments
            .insert(out.elements.add(
                ElementKind::Active {
                    table: ifunc_table_id,
                    offset: ConstExpr::Value(ir::Value::I32(offset as i32)),
                },
                ElementItems::Functions(functions),
            ));
    }

    /// CodeBrix change: new with patch 0006. Turn the functions of the shared chunks that this
    /// module uses (`symbols_to_import`) into calls through the chunks' table slots, so that the
    /// module no longer carries their code. `own_chunk` is the chunk being written, whose functions
    /// stay as they are; a split module passes None.
    fn convert_chunk_funcs_to_imports(
        &self,
        out: &mut Module,
        segment_start: usize,
        chunk_funcs: &[Vec<Node>],
        symbols_to_import: &HashSet<Node>,
        own_chunk: Option<usize>,
    ) {
        let ifunc_table_id = self.load_funcref_table(out);

        let mut slot = segment_start + self.chunk_slots_start();
        for (chunk_idx, funcs) in chunk_funcs.iter().enumerate() {
            for node in funcs {
                if let Node::Function(func) = node {
                    if own_chunk != Some(chunk_idx) && symbols_to_import.contains(node) {
                        let ty_id = out.funcs.get(*func).ty();
                        out.funcs.get_mut(*func).kind =
                            self.make_stub_funcs(out, ifunc_table_id, ty_id, slot as _);
                    }
                }

                slot += 1;
            }
        }
    }

    /// Convert functions coming in from outside the module to indirect calls to the ifunc table created in the main module
    fn convert_shared_to_imports(
        &self,
        out: &mut Module,
        segment_start: usize,
        ifuncs: &Vec<Node>,
        symbols_to_import: &HashSet<Node>,
    ) {
        let ifunc_table_id = self.load_funcref_table(out);

        let mut idx = self.split_points.len();
        for node in ifuncs {
            if let Node::Function(ifunc) = node {
                if symbols_to_import.contains(node) {
                    let ty_id = out.funcs.get(*ifunc).ty();
                    let stub = (idx + segment_start) as _;
                    out.funcs.get_mut(*ifunc).kind =
                        self.make_stub_funcs(out, ifunc_table_id, ty_id, stub);
                }

                idx += 1;
            }
        }
    }

    /// Convert split import functions to local functions that call an indirect function that will
    /// be filled in from the loaded split module.
    ///
    /// This is because these imports are going to be delayed until the split module is loaded
    /// and loading in the main module these as imports won't be possible since the imports won't
    /// be resolved until the split module is loaded.
    fn create_ifunc_table(&self, out: &mut Module) {
        let ifunc_table = self.load_funcref_table(out);
        let dummy_func = self.make_dummy_func(out);

        out.exports.add("__indirect_function_table", ifunc_table);

        // Expand the ifunc table to accommodate the new ifuncs
        //
        // CodeBrix change: also by the slots of the shared chunks' functions, which stay empty
        // here: a chunk fills its own when it is loaded (patch 0006, see ifunc_table_growth).
        let segment_start = self
            .expand_ifunc_table_max(out, ifunc_table, self.ifunc_table_growth())
            .expect("failed to expand ifunc table");

        // Delete the split import functions and replace them with local functions
        //
        // Start by pushing all the shared imports into the list
        // These don't require an additional stub function
        let mut ifuncs = vec![];

        // Push the split import functions into the list - after we've pushed in the shared imports
        for idx in 0..self.split_points.len() {
            // this is okay since we're in the main module
            let import_func = self.split_points[idx].import_func;
            let import_id = self.split_points[idx].import_id;
            let ty_id = out.funcs.get(import_func).ty();
            let stub_idx = segment_start + ifuncs.len();

            // Replace the import function with a local function that calls the indirect function
            out.funcs.get_mut(import_func).kind =
                self.make_stub_funcs(out, ifunc_table, ty_id, stub_idx as _);

            // And remove the corresponding import
            out.imports.delete(import_id);

            // Push into the list the properly typed dummy func so the entry is populated
            // unclear if the typing is important here
            ifuncs.push(dummy_func);
        }

        // Add the stub functions to the ifunc table
        // The callers of these functions will call the stub instead of the import
        let mut _idx = 0;
        for func in self.shared_symbols.iter() {
            if let Node::Function(id) = func {
                ifuncs.push(*id);
                _idx += 1;
            }
        }

        // Now add segments to the ifunc table
        out.tables
            .get_mut(ifunc_table)
            .elem_segments
            .insert(out.elements.add(
                ElementKind::Active {
                    table: ifunc_table,
                    offset: ConstExpr::Value(ir::Value::I32(segment_start as _)),
                },
                ElementItems::Functions(ifuncs),
            ));
    }

    /// Re-export the memories, globals, and other items from the main module to the side modules
    fn re_export_items(&self, out: &mut Module) {
        // Re-export memories
        for (idx, memory) in out.memories.iter().enumerate() {
            let name = memory
                .name
                .clone()
                .unwrap_or_else(|| format!("__memory_{}", idx));
            out.exports.add(&name, memory.id());
        }

        // Re-export globals
        for (idx, global) in out.globals.iter().enumerate() {
            let global_name = format!("__global__{idx}");
            out.exports.add(&global_name, global.id());
        }

        // Export any tables
        for (idx, table) in out.tables.iter().enumerate() {
            if table.element_ty != RefType::Funcref {
                let table_name = format!("__imported_table_{}", idx);
                out.exports.add(&table_name, table.id());
            }
        }
    }

    fn prune_main_symbols(&self, out: &mut Module, unused_symbols: &HashSet<Node>) -> Result<()> {
        // Wipe the split point exports
        for split in self.split_points.iter() {
            // it's okay that we're not re-mapping IDs since this is just used by the main module
            out.exports.delete(split.export_id);
        }

        // CodeBrix change: the bytes of the first data segment that a data symbol the main module
        // KEEPS covers. Data symbols can overlap: at -O2 (which rustc asks of wasm-ld at opt-level
        // 2, 3, s and z) the linker stores a constant that is the tail of a longer constant inside
        // the longer one. Upstream zeroed the whole range of every symbol only split modules
        // reach, and with it any constant of the main module stored in that range - for one, the
        // three-byte template of `format!("{}{}", a, b)` in the tail of a split page's template,
        // after which that expression gave "" in the main module until the page's module was
        // loaded. The loop below leaves these bytes alone. See
        // tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0004.
        let mut kept_bytes = match out.data.iter().next() {
            Some(data) => vec![false; data.value.len()],
            None => Vec::new(),
        };
        for (index, symbol) in self.data_symbols.iter() {
            if symbol.which_data_segment == 0
                && !unused_symbols.contains(&Node::DataSymbol(*index))
            {
                let end = (symbol.segment_offset + symbol.symbol_size).min(kept_bytes.len());
                for kept in kept_bytes.iter_mut().take(end).skip(symbol.segment_offset) {
                    *kept = true;
                }
            }
        }

        // And then any actual symbols from the callgraph
        for symbol in unused_symbols.iter().cloned() {
            match symbol {
                // Simply delete functions
                Node::Function(id) => {
                    out.funcs.delete(id);
                }

                // Otherwise, zero out the data segment, which should lead to elimination by wasm-opt
                Node::DataSymbol(id) => {
                    // CodeBrix change: a data symbol that is not in the table has no bytes to
                    // zero - the table (parse_bytes_to_data_segment) leaves out the symbols of
                    // size 0, such as a zero-sized static, and the undefined ones - so it is
                    // skipped, as clear_data_segments already skips it. Upstream failed the
                    // whole build here ("Failed to find data symbol") when only split modules
                    // named such a symbol. See
                    // tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0005.
                    let Some(symbol) = self.data_symbols.get(&id) else {
                        continue;
                    };

                    // VERY IMPORTANT
                    //
                    // apparently wasm-bindgen makes data segments that aren't the main one
                    // even *touching* those will break the vtable / binding layer
                    // We can only interact with the first data segment - the rest need to stay available
                    // for the `.js` to interact with.
                    if symbol.which_data_segment == 0 {
                        let data_id = out.data.iter().nth(symbol.which_data_segment).unwrap().id();
                        let data = out.data.get_mut(data_id);
                        for i in symbol.segment_offset..symbol.segment_offset + symbol.symbol_size {
                            // CodeBrix change: not the bytes a kept symbol covers (above).
                            if !kept_bytes[i] {
                                data.value[i] = 0;
                            }
                        }
                    }
                }
            }
        }

        Ok(())
    }

    // 2.1 Create a dummy func that will be overridden later as modules pop in
    // 2.2 swap the segment entries with the dummy func, leaving hole in its placed that will be filled in later
    fn replace_segments_with_holes(&self, out: &mut Module, unused_symbols: &HashSet<Node>) {
        let dummy_func = self.make_dummy_func(out);
        for element in out.elements.iter_mut() {
            match &mut element.items {
                ElementItems::Functions(vec) => {
                    for item in vec.iter_mut() {
                        if unused_symbols.contains(&Node::Function(*item)) {
                            *item = dummy_func;
                        }
                    }
                }
                ElementItems::Expressions(_ref_type, const_exprs) => {
                    for item in const_exprs.iter_mut() {
                        if let &mut ConstExpr::RefFunc(id) = item {
                            if unused_symbols.contains(&Node::Function(id)) {
                                *item = ConstExpr::RefFunc(dummy_func);
                            }
                        }
                    }
                }
            }
        }
    }

    /// Creates the jump points
    fn create_ifunc_initializers(&self, out: &mut Module, unique_symbols: &HashSet<Node>) {
        let ifunc_table = self.load_funcref_table(out);

        // CodeBrix change: a BTreeMap, not a HashMap, so that the element segments below are
        // added - and written into the module - in the order of their table offsets. A HashMap
        // iterates in an order that differs in every run of dx, so the same input gave split
        // modules with differently ordered element sections, other bytes and other content-hashed
        // file names. See tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0003.
        let mut initializers = BTreeMap::new();
        for segment in out.elements.iter_mut() {
            let ElementKind::Active { offset, .. } = &mut segment.kind else {
                continue;
            };

            let ConstExpr::Value(ir::Value::I32(offset)) = offset else {
                continue;
            };

            match &segment.items {
                ElementItems::Functions(vec) => {
                    for (idx, id) in vec.iter().enumerate() {
                        if unique_symbols.contains(&Node::Function(*id)) {
                            initializers
                                .insert(*offset + idx as i32, ElementItems::Functions(vec![*id]));
                        }
                    }
                }

                ElementItems::Expressions(ref_type, const_exprs) => {
                    for (idx, expr) in const_exprs.iter().enumerate() {
                        if let ConstExpr::RefFunc(id) = expr {
                            if unique_symbols.contains(&Node::Function(*id)) {
                                initializers.insert(
                                    *offset + idx as i32,
                                    ElementItems::Expressions(
                                        *ref_type,
                                        vec![ConstExpr::RefFunc(*id)],
                                    ),
                                );
                            }
                        }
                    }
                }
            }
        }

        // Wipe away references to these segments
        for table in out.tables.iter_mut() {
            table.elem_segments.clear();
        }

        // Wipe away the element segments themselves
        let segments_to_delete: Vec<_> = out.elements.iter().map(|e| e.id()).collect();
        for id in segments_to_delete {
            out.elements.delete(id);
        }

        // Add in our new segments
        let ifunc_table_ = out.tables.get_mut(ifunc_table);
        for (offset, items) in initializers {
            let kind = ElementKind::Active {
                table: ifunc_table,
                offset: ConstExpr::Value(ir::Value::I32(offset)),
            };

            ifunc_table_
                .elem_segments
                .insert(out.elements.add(kind, items));
        }
    }

    fn add_split_imports(
        &self,
        out: &mut Module,
        split_idx: usize,
        split_export_func: FunctionId,
        split_export_name: String,
        symbols_to_import: &HashSet<Node>,
        ifuncs: &Vec<Node>,
        chunk_funcs: &[Vec<Node>],
    ) {
        let ifunc_table_id = self.load_funcref_table(out);
        // CodeBrix change: the table also has the slots of the chunks' functions (patch 0006).
        let segment_start = self
            .expand_ifunc_table_max(out, ifunc_table_id, self.ifunc_table_growth())
            .unwrap();

        // Make sure to re-export the split func
        out.exports.add(&split_export_name, split_export_func);

        // Add the elements back to the table
        out.tables
            .get_mut(ifunc_table_id)
            .elem_segments
            .insert(out.elements.add(
                ElementKind::Active {
                    table: ifunc_table_id,
                    offset: ConstExpr::Value(ir::Value::I32((segment_start + split_idx) as i32)),
                },
                ElementItems::Functions(vec![split_export_func]),
            ));

        self.convert_shared_to_imports(out, segment_start, ifuncs, symbols_to_import);

        // CodeBrix change: the functions this module uses that a shared chunk holds become calls
        // through the chunk's table slots. Upstream took them off the module's own list
        // (emit_split_module) but left their code in the module (patch 0006).
        self.convert_chunk_funcs_to_imports(
            out,
            segment_start,
            chunk_funcs,
            symbols_to_import,
            None,
        );
    }

    fn delete_main_funcs_from_split(&self, out: &mut Module, symbols_to_delete: &HashSet<Node>) {
        for node in symbols_to_delete {
            if let Node::Function(id) = *node {
                // if out.exports.get_exported_func(id).is_none() {
                out.funcs.delete(id);
                // }
            }
        }
    }

    /// Remove un-needed stuff and then hoist
    fn prune_split_module(&self, out: &mut Module) {
        // Clear the module's start/main
        if let Some(start) = out.start.take() {
            if let Some(export) = out.exports.get_exported_func(start) {
                out.exports.delete(export.id());
            }
        }

        // We're going to import the funcref table, so wipe it altogether
        for table in out.tables.iter_mut() {
            table.elem_segments.clear();
        }

        // Wipe all our imports - we're going to use a different set of imports
        let all_imports: HashSet<_> = out.imports.iter().map(|i| i.id()).collect();
        for import_id in all_imports {
            out.imports.delete(import_id);
        }

        // Wipe away memories
        let all_memories: Vec<_> = out.memories.iter().map(|m| m.id()).collect();
        for memory_id in all_memories {
            out.memories.get_mut(memory_id).data_segments.clear();
        }

        // Add exports that call the corresponding import
        let exports = out.exports.iter().map(|e| e.id()).collect::<Vec<_>>();
        for export_id in exports {
            out.exports.delete(export_id);
        }

        // Convert the tables to imports.
        // Should be as simple as adding a new import and then writing the `.import` field
        for (idx, table) in out.tables.iter_mut().enumerate() {
            let name = table.name.clone().unwrap_or_else(|| {
                if table.element_ty == RefType::Funcref {
                    "__indirect_function_table".to_string()
                } else {
                    format!("__imported_table_{}", idx)
                }
            });
            let import = out.imports.add("__wasm_split", &name, table.id());
            table.import = Some(import);
        }

        // Convert the memories to imports
        // Should be as simple as adding a new import and then writing the `.import` field
        for (idx, memory) in out.memories.iter_mut().enumerate() {
            let name = memory
                .name
                .clone()
                .unwrap_or_else(|| format!("__memory_{}", idx));
            let import = out.imports.add("__wasm_split", &name, memory.id());
            memory.import = Some(import);
        }

        // Convert the globals to imports
        // We might not use the global, so if we don't, we can just get
        let global_ids: Vec<_> = out.globals.iter().map(|t| t.id()).collect();
        for (idx, global_id) in global_ids.into_iter().enumerate() {
            let global = out.globals.get_mut(global_id);
            let global_name = format!("__global__{idx}");
            let import = out.imports.add("__wasm_split", &global_name, global.id());
            global.kind = GlobalKind::Import(import);
        }
    }

    fn make_dummy_func(&self, out: &mut Module) -> FunctionId {
        let mut b = FunctionBuilder::new(&mut out.types, &[], &[]);
        b.name("dummy".into()).func_body().unreachable();
        b.finish(vec![], &mut out.funcs)
    }

    fn clear_data_segments(&self, out: &mut Module, unique_symbols: &HashSet<Node>) {
        // Preserve the data symbols for this module and then clear them away
        let data_ids: Vec<_> = out.data.iter().map(|t| t.id()).collect();
        for (idx, data_id) in data_ids.into_iter().enumerate() {
            let data = out.data.get_mut(data_id);

            // Take the data out of the vec - zeroing it out unless we patch it in manually
            let contents = data.value.split_off(0);

            // Zero out the non-primary data segments
            if idx != 0 {
                continue;
            }

            let DataKind::Active { memory, offset } = data.kind else {
                continue;
            };

            let ConstExpr::Value(ir::Value::I32(data_offset)) = offset else {
                continue;
            };

            // And then assign chunks of the data to new data entries that will override the individual slots
            //
            // CodeBrix change: the symbols are taken in sorted order, not in the iteration order
            // of the HashSet, which differs in every run of dx: the data segments added here are
            // written into the module in the order they are added, so the same input gave split
            // modules with differently ordered data sections, other bytes and other
            // content-hashed file names. See
            // tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0003.
            let mut unique_in_order = unique_symbols.iter().collect::<Vec<_>>();
            unique_in_order.sort_unstable();
            for unique in unique_in_order {
                if let Node::DataSymbol(id) = unique {
                    if let Some(symbol) = self.data_symbols.get(id) {
                        if symbol.which_data_segment == idx {
                            let range =
                                symbol.segment_offset..symbol.segment_offset + symbol.symbol_size;
                            let offset = ConstExpr::Value(ir::Value::I32(
                                data_offset + symbol.segment_offset as i32,
                            ));
                            out.data.add(
                                DataKind::Active { memory, offset },
                                contents[range].to_vec(),
                            );
                        }
                    }
                }
            }
        }
    }

    /// Load the funcref table from the main module. This *should* exist for all modules created by
    /// Rustc or Wasm-Bindgen, but we create it if it doesn't exist.
    fn load_funcref_table(&self, out: &mut Module) -> TableId {
        let ifunc_table = out
            .tables
            .iter()
            .find(|t| t.element_ty == RefType::Funcref)
            .map(|t| t.id());

        if let Some(table) = ifunc_table {
            table
        } else {
            out.tables.add_local(false, 0, None, RefType::Funcref)
        }
    }

    /// Convert the imported function to a local function that calls an indirect function from the table
    ///
    /// This will enable the main module (and split modules) to call functions from outside their own module.
    /// The functions might not exist when the main module is loaded, so we'll register some elements
    /// that fill those in eventually.
    fn make_stub_funcs(
        &self,
        out: &mut Module,
        table: TableId,
        ty_id: TypeId,
        table_idx: i32,
    ) -> FunctionKind {
        // Convert the import function to a local function that calls the indirect function from the table
        let ty = out.types.get(ty_id);

        let params = ty.params().to_vec();
        let results = ty.results().to_vec();
        let args: Vec<_> = params.iter().map(|ty| out.locals.add(*ty)).collect();

        // New function that calls the indirect function
        let mut builder = FunctionBuilder::new(&mut out.types, &params, &results);
        let mut body = builder.name("stub".into()).func_body();

        // Push the params onto the stack
        for arg in args.iter() {
            body.local_get(*arg);
        }

        // And then the address of the indirect function
        body.instr(ir::Instr::Const(ir::Const {
            value: ir::Value::I32(table_idx),
        }));

        // And call it
        body.instr(ir::Instr::CallIndirect(ir::CallIndirect {
            ty: ty_id,
            table,
        }));

        FunctionKind::Local(builder.local_func(args))
    }

    /// Expand the ifunc table to accommodate the new ifuncs
    ///
    /// returns the old maximum
    fn expand_ifunc_table_max(
        &self,
        out: &mut Module,
        table: TableId,
        num_ifuncs: usize,
    ) -> Option<usize> {
        let ifunc_table_ = out.tables.get_mut(table);

        if let Some(max) = ifunc_table_.maximum {
            ifunc_table_.maximum = Some(max + num_ifuncs as u64);
            ifunc_table_.initial += num_ifuncs as u64;
            return Some(max as usize);
        }

        None
    }

    // only keep the target-features and names section so wasm-opt can use it to optimize the output
    fn remove_custom_sections(&self, out: &mut Module) {
        let sections_to_delete = out
            .customs
            .iter()
            .filter_map(|(id, section)| {
                if section.name() == "target_features" {
                    None
                } else {
                    Some(id)
                }
            })
            .collect::<Vec<_>>();

        for id in sections_to_delete {
            out.customs.delete(id);
        }
    }

    /// Accumulate any shared funcs between multiple chunks into a single residual chunk.
    /// This prevents duplicates from being downloaded.
    /// Eventually we need to group the chunks into smarter "communities" - ie the Louvain algorithm
    ///
    /// Todo: we could chunk up the main module itself! Not going to now but it would enable parallel downloads of the main chunk
    fn build_split_chunks(&mut self) {
        // create a single chunk that contains all functions used by multiple modules
        //
        // CodeBrix change: upstream's loop had no body after the `continue`, so nothing was ever
        // recorded, the one chunk was always empty and every split module carried its own copy
        // of the code several of them use. The loop now records which split modules reach each
        // symbol that the main module does not hold. A split point's own entry function is left
        // out: its module exports it and puts it into the function table itself. See
        // tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0006.
        //
        // CodeBrix change: ordered maps and sets, and the symbols are grouped into one chunk or
        // several by the options (assign_shared_chunks). See tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0007.
        let split_exports: HashSet<Node> = self
            .split_points
            .iter()
            .map(|split| Node::Function(split.export_func))
            .collect();
        let mut funcs_used_by_chunks: BTreeMap<Node, BTreeSet<usize>> = BTreeMap::new();
        for split in self.split_points.iter() {
            for item in split.reachable_graph.iter() {
                if self.main_graph.contains(item) {
                    continue;
                }

                if split_exports.contains(item) {
                    continue;
                }

                funcs_used_by_chunks
                    .entry(*item)
                    .or_default()
                    .insert(split.index);
            }
        }

        // Only consider funcs that are used by multiple modules - otherwise they can just stay in their respective module
        funcs_used_by_chunks.retain(|_, v| v.len() > 1);

        let chunks = assign_shared_chunks(
            &funcs_used_by_chunks,
            |node| self.node_size(node),
            &self.options,
        );
        self.chunks = chunks
            .into_iter()
            .map(|chunk| chunk.into_iter().collect())
            .collect();
    }

    /// CodeBrix change: new with patch 0007. The bytes a symbol brings, before optimisation:
    /// a function's body in the module wasm-bindgen wrote, a data symbol's size. Imported
    /// functions bring none.
    fn node_size(&self, node: &Node) -> usize {
        match node {
            Node::Function(id) => match &self.source_module.funcs.get(*id).kind {
                FunctionKind::Local(local) => local
                    .original_range
                    .as_ref()
                    .map(|range| range.len())
                    .unwrap_or(0),
                _ => 0,
            },
            Node::DataSymbol(id) => self
                .data_symbols
                .get(id)
                .map(|symbol| symbol.symbol_size)
                .unwrap_or(0),
        }
    }

    fn unused_main_symbols(&self) -> HashSet<Node> {
        self.split_points
            .iter()
            .flat_map(|split| split.reachable_graph.iter())
            .filter(|sym| {
                // Make sure the symbol isn't in the main graph
                if self.main_graph.contains(sym) {
                    return false;
                }

                // And ensure we aren't also exporting it
                match sym {
                    Node::Function(u) => self.source_module.exports.get_exported_func(*u).is_none(),
                    _ => true,
                }
            })
            .cloned()
            .collect()
    }

    /// Accumulate the relocations from the original module, create a relocation map, and then convert
    /// that to our *new* module's symbols.
    fn build_call_graph(&mut self) -> Result<()> {
        let original = ModuleWithRelocations::new(self.original)?;

        let old_names: HashMap<String, FunctionId> = original
            .module
            .funcs
            .iter()
            .flat_map(|f| Some((f.name.clone()?, f.id())))
            .collect();

        let new_names: HashMap<String, FunctionId> = self
            .source_module
            .funcs
            .iter()
            .flat_map(|f| Some((f.name.clone()?, f.id())))
            .collect();

        let mut old_to_new = HashMap::new();

        // CodeBrix change: the local map `new_call_graph` that was filled here and below is gone;
        // its edges now go straight into `self.call_graph` (see the change further down).
        // tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0001.
        for (new_name, new_func) in new_names.iter() {
            if let Some(old_func) = old_names.get(new_name) {
                old_to_new.insert(*old_func, new_func);
            }
        }

        let get_old = |old: &Node| -> Option<Node> {
            match old {
                Node::Function(id) => old_to_new.get(id).map(|new_id| Node::Function(**new_id)),
                Node::DataSymbol(id) => Some(Node::DataSymbol(*id)),
            }
        };

        // the symbols that we can't find in the original module touch functions that unfortunately
        // we can't figure out where should exist in the call graph
        //
        // we're going to walk and find every child we possibly can and then add it to the call graph
        // at the root
        //
        // wasm-bindgen will dissolve describe functions into the shim functions, but we don't have a
        // sense of lining up old to new, so we just assume everything ends up in the main chunk.
        let mut lost_children = HashSet::new();
        self.call_graph = original
            .call_graph
            .iter()
            .flat_map(|(old, children)| {
                // If the old function isn't in the new module, we need to move all its descendents into the main chunk
                let Some(new) = get_old(old) else {
                    for child in children {
                        fn descend(
                            lost_children: &mut HashSet<Node>,
                            old_graph: &HashMap<Node, HashSet<Node>>,
                            node: Node,
                        ) {
                            if !lost_children.insert(node) {
                                return;
                            }

                            if let Some(children) = old_graph.get(&node) {
                                for child in children {
                                    descend(lost_children, old_graph, *child);
                                }
                            }
                        }

                        descend(&mut lost_children, &original.call_graph, *child);
                    }
                    return None;
                };

                let mut new_children = HashSet::new();
                for child in children {
                    if let Some(new) = get_old(child) {
                        new_children.insert(new);
                    }
                }

                Some((new, new_children))
            })
            .collect();

        let mut recovered_children = HashSet::new();
        for lost in lost_children {
            match lost {
                // Functions need to be found - the wasm describe functions are usually completely dissolved
                Node::Function(id) => {
                    let func = original.module.funcs.get(id);
                    let name = func.name.as_ref().unwrap();
                    if let Some(entry) = new_names.get(name) {
                        recovered_children.insert(Node::Function(*entry));
                    }
                }

                // Data symbols are unchanged and fine to remap
                Node::DataSymbol(id) => {
                    recovered_children.insert(Node::DataSymbol(id));
                }
            }
        }

        // We're going to attach the recovered children to the main function
        //
        // CodeBrix change: upstream wrote these edges into a local map that nothing read, so the
        // recovered children (the closure `invoke` shims that only dissolved describe functions
        // referenced) and the functions only wasm-bindgen made were never reachable from main.
        // They stay in the main module anyway (they sit in the function table), so a function
        // they call that a split module also reaches was deleted from main while still called,
        // and walrus panicked ("assertion failed: !self.dead.contains(&id)"). The edges now go
        // into the call graph reachability is computed from, as in upstream's open pull request
        // DioxusLabs/dioxus#5795 (commit 3f6e5a9). See
        // tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0001.
        let main_fn = self.source_module.funcs.by_name("main").context("Failed to find `main` function - was this built with LTO, --emit-relocs, and debug symbols?")?;
        let main_fn_entry = self.call_graph.entry(Node::Function(main_fn)).or_default();
        main_fn_entry.extend(recovered_children);

        // Also attach any truly new symbols to the main function. Usually these are the shim functions
        for (name, new) in new_names.iter() {
            if !old_names.contains_key(name) {
                main_fn_entry.insert(Node::Function(*new));
            }
        }

        // Walk the functions and try to disconnect any holes manually
        // This will attempt to resolve any of the new symbols like the shim functions
        for func in self.source_module.funcs.iter() {
            struct CallGrapher<'a> {
                cur: FunctionId,
                call_graph: &'a mut HashMap<Node, HashSet<Node>>,
            }
            impl<'a> Visitor<'a> for CallGrapher<'a> {
                fn visit_function_id(&mut self, function: &walrus::FunctionId) {
                    self.call_graph
                        .entry(Node::Function(self.cur))
                        .or_default()
                        .insert(Node::Function(*function));
                }
            }
            if let FunctionKind::Local(local) = &func.kind {
                let mut call_grapher = CallGrapher {
                    cur: func.id(),
                    call_graph: &mut self.call_graph,
                };
                dfs_in_order(&mut call_grapher, local, local.entry_block());
            }
        }

        // Fill in the parent graph
        for (parnet, children) in self.call_graph.iter() {
            for child in children {
                self.parent_graph.entry(*child).or_default().insert(*parnet);
            }
        }

        // Now go fill in the reachability graph for each of the split points
        // We want to be as narrow as possible since we've reparented any new symbols to the main module
        self.split_points.iter_mut().for_each(|split| {
            let roots: HashSet<_> = [Node::Function(split.export_func)].into();
            split.reachable_graph = reachable_graph(&self.call_graph, &roots);
        });

        // And then the reachability graph for main
        self.main_graph = reachable_graph(&self.call_graph, &self.main_roots());

        // And then the symbols shared between all
        self.shared_symbols = {
            let mut shared_funcs = HashSet::new();

            // Add all the symbols shared between the various modules
            for split in self.split_points.iter() {
                shared_funcs.extend(self.main_graph.intersection(&split.reachable_graph));
            }

            // And then all our imports will be callabale via the ifunc table too
            for import in self.source_module.imports.iter() {
                if let ImportKind::Function(id) = import.kind {
                    shared_funcs.insert(Node::Function(id));
                }
            }

            // Make sure to make this *ordered*
            shared_funcs.into_iter().collect()
        };

        Ok(())
    }

    fn main_roots(&self) -> HashSet<Node> {
        // Accumulate all the split entrypoints
        // This will include wasm_bindgen functions too
        let exported_splits = self
            .split_points
            .iter()
            .map(|f| f.export_func)
            .collect::<HashSet<_>>();

        // And only return the functions that are reachable from the main module's start function
        let mut roots = self
            .source_module
            .exports
            .iter()
            .filter_map(|e| match e.item {
                ExportItem::Function(id) if !exported_splits.contains(&id) => {
                    Some(Node::Function(id))
                }
                _ => None,
            })
            .chain(self.source_module.start.map(Node::Function))
            .collect::<HashSet<Node>>();

        // Also add "imports" to the roots
        for import in self.source_module.imports.iter() {
            if let ImportKind::Function(id) = import.kind {
                roots.insert(Node::Function(id));
            }
        }

        roots
    }

    /// Convert this set of nodes to reference the new module
    fn remap_ids(&self, set: &HashSet<Node>, ids_to_fns: &[FunctionId]) -> HashSet<Node> {
        let mut out = HashSet::with_capacity(set.len());
        for node in set {
            out.insert(self.remap_id(ids_to_fns, node));
        }
        out
    }

    fn remap_id(&self, ids_to_fns: &[id_arena::Id<walrus::Function>], node: &Node) -> Node {
        match node {
            // Remap the function IDs
            Node::Function(id) => Node::Function(ids_to_fns[self.fns_to_ids[id]]),
            // data symbols don't need remapping
            Node::DataSymbol(id) => Node::DataSymbol(*id),
        }
    }
}

/// Parse a module and return the mapping of index to FunctionID.
/// We'll use this mapping to remap ModuleIDs
fn parse_module_with_ids(
    bindgened: &[u8],
) -> Result<(Module, Vec<FunctionId>, HashMap<FunctionId, usize>)> {
    let ids = Arc::new(RwLock::new(Vec::new()));
    let ids_ = ids.clone();
    let module = Module::from_buffer_with_config(
        bindgened,
        ModuleConfig::new().on_parse(move |_m, our_ids| {
            let mut ids = ids_.write().expect("No shared writers");
            let mut idx = 0;
            while let Ok(entry) = our_ids.get_func(idx) {
                ids.push(entry);
                idx += 1;
            }

            Ok(())
        }),
    )?;
    let mut ids_ = ids.write().expect("No shared writers");
    let mut ids = vec![];
    std::mem::swap(&mut ids, &mut *ids_);

    let mut fns_to_ids = HashMap::new();
    for (idx, id) in ids.iter().enumerate() {
        fns_to_ids.insert(*id, idx);
    }

    Ok((module, ids, fns_to_ids))
}

struct ModuleWithRelocations<'a> {
    module: Module,
    symbols: Vec<SymbolInfo<'a>>,
    names_to_funcs: HashMap<String, FunctionId>,
    call_graph: HashMap<Node, HashSet<Node>>,
    parents: HashMap<Node, HashSet<Node>>,
    relocation_map: HashMap<Node, Vec<RelocationEntry>>,
    data_symbols: BTreeMap<usize, DataSymbol>,
    data_section_range: Range<usize>,
}

impl<'a> ModuleWithRelocations<'a> {
    fn new(bytes: &'a [u8]) -> Result<Self> {
        let module = Module::from_buffer(bytes)?;
        let raw_data = parse_bytes_to_data_segment(bytes)?;
        let names_to_funcs = module
            .funcs
            .iter()
            .flat_map(|f| Some((f.name.clone()?, f.id())))
            .collect();

        let mut module = Self {
            module,
            data_symbols: raw_data.data_symbols,
            data_section_range: raw_data.data_range,
            symbols: raw_data.symbols,
            names_to_funcs,
            call_graph: Default::default(),
            relocation_map: Default::default(),
            parents: Default::default(),
        };

        module.build_code_call_graph()?;
        module.build_data_call_graph()?;

        for (func, children) in module.call_graph.iter() {
            for child in children {
                module.parents.entry(*child).or_default().insert(*func);
            }
        }

        Ok(module)
    }

    fn build_code_call_graph(&mut self) -> Result<()> {
        let codes_relocations = self.collect_relocations_from_section("reloc.CODE")?;
        let mut relocations = codes_relocations.iter().peekable();

        for (func_id, local) in self.module.funcs.iter_local() {
            let range = local
                .original_range
                .clone()
                .context("local function has no range")?;

            // Walk with relocation
            while let Some(entry) =
                relocations.next_if(|entry| entry.relocation_range().start < range.end)
            {
                let reloc_range = entry.relocation_range();
                assert!(reloc_range.start >= range.start);
                assert!(reloc_range.end <= range.end);

                // CodeBrix change: a type-index relocation (the signature immediate of a
                // `call_indirect`) carries a TYPE index, not a symbol index. Upstream looked it
                // up as a symbol and linked the caller to whatever symbol has that number - for
                // example a closure `invoke` shim to a route's split export - so, with patch 0001
                // in place, most of a split route's code would be pinned into the main module.
                // The same skip is in upstream's pull request DioxusLabs/dioxus#5915 (closed
                // unmerged). See tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt,
                // patch 0002.
                if entry.ty == wasmparser::RelocationType::TypeIndexLeb {
                    continue;
                }

                if let Some(target) = self.get_symbol_dep_node(entry.index as usize)? {
                    let us = Node::Function(func_id);
                    self.call_graph.entry(us).or_default().insert(target);
                    self.relocation_map.entry(us).or_default().push(*entry);
                }
            }
        }

        assert!(relocations.next().is_none());

        Ok(())
    }

    fn build_data_call_graph(&mut self) -> Result<()> {
        let data_relocations = self.collect_relocations_from_section("reloc.DATA")?;
        let mut relocations = data_relocations.iter().peekable();

        let symbols_sorted = self
            .data_symbols
            .values()
            .sorted_by(|a, b| a.range.start.cmp(&b.range.start));

        for symbol in symbols_sorted {
            let start = symbol.range.start - self.data_section_range.start;
            let end = symbol.range.end - self.data_section_range.start;
            let range = start..end;

            while let Some(entry) =
                relocations.next_if(|entry| entry.relocation_range().start < range.end)
            {
                let reloc_range = entry.relocation_range();
                assert!(reloc_range.start >= range.start);
                assert!(reloc_range.end <= range.end);

                if let Some(target) = self.get_symbol_dep_node(entry.index as usize)? {
                    let dep = Node::DataSymbol(symbol.index);
                    self.call_graph.entry(dep).or_default().insert(target);
                    self.relocation_map.entry(dep).or_default().push(*entry);
                }
            }
        }

        assert!(relocations.next().is_none());

        Ok(())
    }

    /// Accumulate all relocations from a section.
    ///
    /// Parses the section using the RelocSectionReader and returns a vector of relocation entries.
    fn collect_relocations_from_section(&self, name: &str) -> Result<Vec<RelocationEntry>> {
        let (_reloc_id, code_reloc) = self
            .module
            .customs
            .iter()
            .find(|(_, c)| c.name() == name)
            .context("Module does not contain the reloc section")?;

        let code_reloc_data = code_reloc.data(&Default::default());
        let reader = BinaryReader::new(&code_reloc_data, 0);
        let relocations = RelocSectionReader::new(reader)
            .context("failed to parse reloc section")?
            .entries()
            .into_iter()
            .flatten()
            .collect();

        Ok(relocations)
    }

    /// Get the symbol's corresponding entry in the call graph
    ///
    /// This might panic if the source module isn't built properly. Make sure to enable LTO and `--emit-relocs`
    /// when building the source module.
    fn get_symbol_dep_node(&self, index: usize) -> Result<Option<Node>> {
        let res = match self.symbols[index] {
            SymbolInfo::Data { .. } => Some(Node::DataSymbol(index)),
            SymbolInfo::Func { name, .. } => Some(Node::Function({
                let name = name.context(
                    "Function symbol has no name - did you forget to enable debug symbols",
                )?;

                let func_id = self.names_to_funcs.get(name);

                // wbindgen will synthesize some functions that don't exist in the original module (eg describe functions)
                // Previously this was a hard error, but now we just ignore it. It used to mean that the user
                let Some(res) = func_id else {
                    if !name.contains("__wbindgen_") {
                        tracing::error!("Could not find function symbol {name:?} in module - was this built with LTO, --emit-relocs, and debug symbols? Ignoring.");
                    }
                    return Ok(None);
                };

                *res
            })),

            _ => None,
        };

        Ok(res)
    }
}

#[derive(Debug, Clone)]
pub struct SplitPoint {
    module_name: String,
    import_id: ImportId,
    export_id: ExportId,
    import_func: FunctionId,
    export_func: FunctionId,
    component_name: String,
    index: usize,
    reachable_graph: HashSet<Node>,
    hash_name: String,

    #[allow(unused)]
    import_name: String,

    #[allow(unused)]
    export_name: String,
}

/// Search the module's imports and exports for functions marked as split points.
///
/// These will be in the form of:
///
/// `__wasm_split_00<module>00_<import|export>_<hash>_<function>`
///
/// For a function named `SomeRoute2` in the module `add_body_element`, the pairings would be:
///
/// `__wasm_split_00add_body_element00_import_abef5ee3ebe66ff17677c56ee392b4c2_SomeRoute2`
/// `__wasm_split_00add_body_element00_export_abef5ee3ebe66ff17677c56ee392b4c2_SomeRoute2`
///
fn accumulate_split_points(module: &Module) -> Vec<SplitPoint> {
    let mut index = 0;

    module
        .imports
        .iter()
        .sorted_by(|a, b| a.name.cmp(&b.name))
        .flat_map(|import| {
            if !import.name.starts_with("__wasm_split_00") {
                return None;
            }

            let ImportKind::Function(import_func) = import.kind else {
                return None;
            };

            // Parse the import name to get the module name, the hash, and the function name
            let remain = import.name.trim_start_matches("__wasm_split_00___");
            // CodeBrix change: split at "___00_import_", not at the first "___00", so that a
            // module name holding "___00" (an extern name such as "a___00b") is read whole
            // instead of ending in a panic ("Could not find export"). See
            // tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt, patch 0007.
            let (module_name, rest) = remain.split_once("___00_import_").unwrap();
            let (hash, fn_name) = rest.split_once("_").unwrap();

            // Look for the export with the same name
            let export_name =
                format!("__wasm_split_00___{module_name}___00_export_{hash}_{fn_name}");
            let export_func = module
                .exports
                .get_func(&export_name)
                .expect("Could not find export");
            let export = module.exports.get_exported_func(export_func).unwrap();

            let our_index = index;
            index += 1;

            Some(SplitPoint {
                export_id: export.id(),
                import_id: import.id(),
                module_name: module_name.to_string(),
                import_name: import.name.clone(),
                import_func,
                export_func,
                export_name,
                hash_name: hash.to_string(),
                component_name: fn_name.to_string(),
                index: our_index,
                reachable_graph: Default::default(),
            })
        })
        .collect()
}

/// CodeBrix change: new with patch 0007. Groups the shared symbols into chunks.
///
/// `users` holds every shared symbol with the split modules (by index) that reach it, two or
/// more each. Without a threshold, all of them form one chunk. With one, the symbols are grouped
/// by their exact set of users; a group whose `size` sum reaches the threshold is "large" and
/// gets a chunk of its own - the largest first, ties by user set, at most `chunk_limit - 1` of
/// them, the common chunk taking the last place. Every other group goes to the large group with
/// the fewest users among those whose users include all of its own (ties: the smaller group,
/// then the user set), so that no split module fetches a chunk it did not already fetch; when
/// there is none, to the common chunk, chunk 0. The large groups' chunks follow in the order of
/// their user sets. Every container is ordered and every tie is broken, so the result depends
/// on nothing but the input. See tools/dx-native-tools/vendoring/wasm-split-cli/PATCH-NOTES.txt,
/// patch 0007.
fn assign_shared_chunks<T: Ord + Copy>(
    users: &BTreeMap<T, BTreeSet<usize>>,
    size: impl Fn(&T) -> usize,
    options: &SplitOptions,
) -> Vec<BTreeSet<T>> {
    let Some(threshold) = options.chunk_threshold else {
        return vec![users.keys().copied().collect()];
    };

    let mut groups: BTreeMap<&BTreeSet<usize>, BTreeSet<T>> = BTreeMap::new();
    for (symbol, set) in users.iter() {
        groups.entry(set).or_default().insert(*symbol);
    }
    let sizes: BTreeMap<&BTreeSet<usize>, usize> = groups
        .iter()
        .map(|(set, symbols)| (*set, symbols.iter().map(&size).sum()))
        .collect();

    let mut large: Vec<&BTreeSet<usize>> = groups
        .keys()
        .copied()
        .filter(|set| sizes[set] >= threshold)
        .collect();
    large.sort_by(|a, b| sizes[b].cmp(&sizes[a]).then_with(|| a.cmp(b)));
    large.truncate(options.chunk_limit.saturating_sub(1));
    large.sort();

    let mut chunks: Vec<BTreeSet<T>> = vec![BTreeSet::new(); large.len() + 1];
    for (set, symbols) in groups.iter() {
        let target = match large.iter().position(|chunk| chunk == set) {
            Some(own) => own + 1,
            None => large
                .iter()
                .enumerate()
                .filter(|(_, chunk)| set.is_subset(chunk))
                .min_by(|(_, a), (_, b)| {
                    a.len()
                        .cmp(&b.len())
                        .then_with(|| sizes[*a].cmp(&sizes[*b]))
                        .then_with(|| a.cmp(b))
                })
                .map(|(index, _)| index + 1)
                .unwrap_or(0),
        };
        chunks[target].extend(symbols.iter().copied());
    }

    chunks
}

#[derive(Debug, PartialEq, Eq, Hash, Copy, PartialOrd, Ord, Clone)]
pub enum Node {
    Function(FunctionId),
    DataSymbol(usize),
}

fn reachable_graph(deps: &HashMap<Node, HashSet<Node>>, roots: &HashSet<Node>) -> HashSet<Node> {
    let mut queue: VecDeque<Node> = roots.iter().copied().collect();
    let mut reachable = HashSet::<Node>::new();
    let mut parents = HashMap::<Node, Node>::new();

    while let Some(node) = queue.pop_front() {
        reachable.insert(node);
        let Some(children) = deps.get(&node) else {
            continue;
        };
        for child in children {
            if reachable.contains(child) {
                continue;
            }
            parents.entry(*child).or_insert(node);
            queue.push_back(*child);
        }
    }

    reachable
}

struct RawDataSection<'a> {
    data_range: Range<usize>,
    symbols: Vec<SymbolInfo<'a>>,
    data_symbols: BTreeMap<usize, DataSymbol>,
}

#[derive(Debug)]
struct DataSymbol {
    index: usize,
    range: Range<usize>,
    segment_offset: usize,
    symbol_size: usize,
    which_data_segment: usize,
}

/// Manually parse the data section from a wasm module
///
/// We need to do this for data symbols because walrus doesn't provide the right range and offset
/// information for data segments. Fortunately, it provides it for code sections, so we only need to
/// do a small amount extra of parsing here.
fn parse_bytes_to_data_segment(bytes: &[u8]) -> Result<RawDataSection<'_>> {
    let parser = wasmparser::Parser::new(0);
    let mut parser = parser.parse_all(bytes);
    let mut segments = vec![];
    let mut data_range = 0..0;
    let mut symbols = vec![];

    // Process the payloads in the raw wasm file so we can extract the specific sections we need
    while let Some(Ok(payload)) = parser.next() {
        match payload {
            Payload::DataSection(section) => {
                data_range = section.range();
                segments = section.into_iter().collect::<Result<Vec<_>, _>>()?
            }
            Payload::CustomSection(section) if section.name() == "linking" => {
                let reader = BinaryReader::new(section.data(), 0);
                let reader = LinkingSectionReader::new(reader)?;
                for subsection in reader.subsections() {
                    if let Linking::SymbolTable(map) = subsection? {
                        symbols = map.into_iter().collect::<Result<Vec<_>, _>>()?;
                    }
                }
            }
            _ => {}
        }
    }

    // Accumulate the data symbols into a btreemap for later use
    let mut data_symbols = BTreeMap::new();
    for (index, symbol) in symbols.iter().enumerate() {
        let SymbolInfo::Data {
            symbol: Some(symbol),
            ..
        } = symbol
        else {
            continue;
        };

        if symbol.size == 0 {
            continue;
        }

        let data_segment = segments
            .get(symbol.index as usize)
            .context("Failed to find data segment")?;
        let offset: usize =
            data_segment.range.end - data_segment.data.len() + (symbol.offset as usize);
        let range = offset..(offset + symbol.size as usize);

        data_symbols.insert(
            index,
            DataSymbol {
                index,
                range,
                segment_offset: symbol.offset as usize,
                symbol_size: symbol.size as usize,
                which_data_segment: symbol.index as usize,
            },
        );
    }

    Ok(RawDataSection {
        data_range,
        symbols,
        data_symbols,
    })
}

/// CodeBrix change: new with patch 0007 - the grouping of shared symbols into chunks.
#[cfg(test)]
mod shared_chunk_tests {
    use super::{assign_shared_chunks, SplitOptions};
    use std::collections::{BTreeMap, BTreeSet};

    fn users(entries: &[(u32, &[usize])]) -> BTreeMap<u32, BTreeSet<usize>> {
        entries
            .iter()
            .map(|(symbol, set)| (*symbol, set.iter().copied().collect()))
            .collect()
    }

    fn sets(chunks: &[BTreeSet<u32>]) -> Vec<Vec<u32>> {
        chunks.iter().map(|chunk| chunk.iter().copied().collect()).collect()
    }

    fn options(threshold: Option<usize>, limit: usize) -> SplitOptions {
        SplitOptions {
            chunk_threshold: threshold,
            chunk_limit: limit,
        }
    }

    // symbol -> size: the symbol number times 10
    fn size(symbol: &u32) -> usize {
        *symbol as usize * 10
    }

    #[test]
    fn without_a_threshold_everything_shared_is_one_chunk() {
        let u = users(&[(1, &[0, 1]), (2, &[1, 2]), (90, &[0, 2])]);
        assert_eq!(sets(&assign_shared_chunks(&u, size, &options(None, 16))), vec![vec![1, 2, 90]]);
    }

    #[test]
    fn nothing_shared_gives_one_empty_chunk() {
        let u = users(&[]);
        assert_eq!(sets(&assign_shared_chunks(&u, size, &options(None, 16))), vec![Vec::<u32>::new()]);
        assert_eq!(sets(&assign_shared_chunks(&u, size, &options(Some(1), 16))), vec![Vec::<u32>::new()]);
    }

    #[test]
    fn a_group_at_the_threshold_gets_its_own_chunk() {
        // {0,1}: 10 + 20 = 30 bytes; {1,2}: 30 bytes; {0,2}: 900 bytes
        let u = users(&[(1, &[0, 1]), (2, &[0, 1]), (3, &[1, 2]), (90, &[0, 2])]);
        let chunks = assign_shared_chunks(&u, size, &options(Some(30), 16));
        // chunks after the common one follow the order of their user sets: {0,1}, {0,2}, {1,2}
        assert_eq!(sets(&chunks), vec![vec![], vec![1, 2], vec![90], vec![3]]);
    }

    #[test]
    fn a_small_group_goes_to_the_large_group_with_the_fewest_users_that_includes_its_users() {
        // large: {0,1,2,3} (500) and {0,1,2} (400); small: {0,1} (10) -> {0,1,2}; {2,3} (20) -> {0,1,2,3}
        let u = users(&[(1, &[0, 1]), (2, &[2, 3]), (40, &[0, 1, 2]), (50, &[0, 1, 2, 3])]);
        let chunks = assign_shared_chunks(&u, size, &options(Some(100), 16));
        assert_eq!(sets(&chunks), vec![vec![], vec![1, 40], vec![2, 50]]);
    }

    #[test]
    fn a_small_group_without_a_large_group_of_its_users_goes_to_the_common_chunk() {
        let u = users(&[(1, &[0, 4]), (50, &[0, 1, 2])]);
        let chunks = assign_shared_chunks(&u, size, &options(Some(100), 16));
        assert_eq!(sets(&chunks), vec![vec![1], vec![50]]);
    }

    #[test]
    fn ties_between_large_groups_of_equal_users_go_to_the_smaller_then_the_first() {
        // {0,1,2} (300) and {0,1,3} (400) both have three users; {0,1} (10) goes to the smaller
        let u = users(&[(1, &[0, 1]), (30, &[0, 1, 2]), (40, &[0, 1, 3])]);
        let chunks = assign_shared_chunks(&u, size, &options(Some(100), 16));
        assert_eq!(sets(&chunks), vec![vec![], vec![1, 30], vec![40]]);
        // equal sizes: the first user set
        let u = users(&[(1, &[0, 1]), (30, &[0, 1, 3]), (31, &[0, 1, 2])]);
        let chunks = assign_shared_chunks(&u, |s: &u32| if *s == 1 { 1 } else { 300 }, &options(Some(100), 16));
        assert_eq!(sets(&chunks), vec![vec![], vec![1, 31], vec![30]]);
    }

    #[test]
    fn the_limit_keeps_the_largest_groups_and_groups_the_others_as_small_ones() {
        // large: {0,1} 100, {0,1,2} 200, {2,3} 300; limit 3 keeps {2,3} and {0,1,2};
        // {0,1} then goes to {0,1,2}
        let u = users(&[(10, &[0, 1]), (20, &[0, 1, 2]), (30, &[2, 3])]);
        let chunks = assign_shared_chunks(&u, size, &options(Some(100), 3));
        assert_eq!(sets(&chunks), vec![vec![], vec![10, 20], vec![30]]);
        // limit 1: the common chunk only, which is the one chunk of no threshold
        let one = assign_shared_chunks(&u, size, &options(Some(100), 1));
        assert_eq!(sets(&one), sets(&assign_shared_chunks(&u, size, &options(None, 16))));
    }

    #[test]
    fn equal_sizes_at_the_limit_keep_the_first_user_sets() {
        let u = users(&[(10, &[2, 3]), (11, &[0, 1]), (12, &[1, 2])]);
        let chunks = assign_shared_chunks(&u, |_| 100, &options(Some(100), 3));
        // {0,1} and {1,2} are kept; {2,3} has no large group of its users
        assert_eq!(sets(&chunks), vec![vec![10], vec![11], vec![12]]);
    }

    #[test]
    fn every_symbol_lands_in_exactly_one_chunk() {
        let u = users(&[(1, &[0, 1]), (2, &[0, 1, 2]), (3, &[1, 2]), (4, &[0, 2]), (5, &[0, 1, 2, 3]), (6, &[3, 4])]);
        for threshold in [None, Some(1), Some(25), Some(45), Some(1000)] {
            for limit in [1, 2, 3, 16] {
                let chunks = assign_shared_chunks(&u, size, &options(threshold, limit));
                assert!(chunks.len() <= limit.max(1));
                let all: Vec<u32> = chunks.iter().flat_map(|c| c.iter().copied()).collect();
                let mut sorted = all.clone();
                sorted.sort();
                sorted.dedup();
                assert_eq!(all.len(), sorted.len());
                assert_eq!(sorted, vec![1, 2, 3, 4, 5, 6]);
            }
        }
    }
}
