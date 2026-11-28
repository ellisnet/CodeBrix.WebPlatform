//! split-check: checks one frontend application built with bundle splitting (`dx build
//! --wasm-split`), as dx left it or as the platform copied it into frontend-built/: the loader
//! (`__wasm_split*.js`) and every WebAssembly file it names, with the main module. It checks what
//! the bundle splitter (tools/dx-native-tools/sources/wasm-split-cli) must keep true however it
//! groups shared code:
//!
//!   - every file the loader names is there and is valid WebAssembly, and every chunk a module
//!     lists has a loader entry;
//!   - every piece (module or chunk) imports the function table with the main module's limits;
//!   - no table slot is filled by two pieces, and no piece fills a slot the main module fills
//!     with a function of its own (a piece may fill the main module's holes);
//!   - every call through a CONSTANT table index (`i32.const K; call_indirect`: the splitter's
//!     stubs, also after wasm-opt has inlined them) goes to a slot that is filled, by the main
//!     module or by exactly one piece, with a function of the same signature; a module that calls
//!     into a chunk lists that chunk; a module calls into no other module;
//!   - two pieces never write different bytes at one address.
//!
//! Usage:  split-check <site folder>
//!         (the folder with index.html; the files are found below it by name)
//! It prints one summary line and every problem, and exits with 1 when there is a problem.
//! Maintainer tooling: it is not part of dx or of any package.

use std::collections::{BTreeMap, BTreeSet};
use std::path::{Path, PathBuf};
use std::process::ExitCode;
use walrus::{ir, ConstExpr, DataKind, ElementItems, ElementKind, FunctionKind, Module, RefType};

/// One loader entry: the file it fetches and the chunks it waits for.
struct Entry {
    file: String,
    deps: Vec<String>,
}

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().collect();
    if args.len() != 2 || args[1].starts_with('-') {
        eprintln!("usage: split-check <site folder>");
        return ExitCode::from(2);
    }
    match check(Path::new(&args[1])) {
        Ok(problems) if problems.is_empty() => ExitCode::SUCCESS,
        Ok(problems) => {
            for problem in problems {
                println!("PROBLEM {problem}");
            }
            ExitCode::from(1)
        }
        Err(message) => {
            eprintln!("split-check: {message}");
            ExitCode::from(2)
        }
    }
}

fn check(site: &Path) -> Result<Vec<String>, String> {
    let files = all_files(site);
    let by_name: BTreeMap<String, PathBuf> = files
        .iter()
        .filter_map(|f| Some((f.file_name()?.to_str()?.to_string(), f.clone())))
        .collect();
    let loaders: Vec<&PathBuf> = files
        .iter()
        .filter(|f| {
            let name = f.file_name().and_then(|n| n.to_str()).unwrap_or("");
            name.starts_with("__wasm_split") && name.ends_with(".js")
        })
        .collect();
    let loader = match loaders.as_slice() {
        [one] => *one,
        [] => return Err(format!("no __wasm_split*.js below {}", site.display())),
        many => return Err(format!("{} loaders below {}; name the folder of one application", many.len(), site.display())),
    };
    let (chunks, modules) = read_loader(loader)?;
    let mut problems = Vec::new();

    // the pieces, by file name
    let mut pieces: BTreeMap<String, Module> = BTreeMap::new();
    for entry in chunks.values().chain(modules.iter()) {
        match by_name.get(&entry.file) {
            None => problems.push(format!("the loader names {}, which is not below the folder", entry.file)),
            Some(path) => match Module::from_file(path) {
                Ok(module) => {
                    pieces.insert(entry.file.clone(), module);
                }
                Err(e) => problems.push(format!("{} is not valid WebAssembly: {e}", entry.file)),
            },
        }
    }
    for module in &modules {
        for dep in &module.deps {
            if !chunks.values().any(|c| &c.file == dep) {
                problems.push(format!("{} waits for a chunk the loader does not define", module.file));
            }
        }
    }

    // the main module: the other .wasm file that exports the function table
    let piece_files: BTreeSet<&String> = pieces.keys().collect();
    let mut mains = Vec::new();
    for path in files.iter().filter(|f| f.extension().is_some_and(|e| e == "wasm")) {
        let name = path.file_name().and_then(|n| n.to_str()).unwrap_or("").to_string();
        if piece_files.contains(&name) || name.starts_with("chunk_") || name.starts_with("module_") {
            continue;
        }
        if let Ok(module) = Module::from_file(path) {
            if module.exports.iter().any(|e| e.name == "__indirect_function_table") {
                mains.push((name, module));
            }
        }
    }
    let (main_name, main) = match mains.len() {
        1 => mains.remove(0),
        n => return Err(format!("{n} main modules (files that export __indirect_function_table) below {}", site.display())),
    };

    let (initial, maximum) = funcref_limits(&main).ok_or("the main module has no function table")?;
    let main_slots = slots(&main);
    let mut filled: BTreeMap<i32, Vec<(String, String)>> = BTreeMap::new();
    for (file, module) in &pieces {
        match funcref_limits(module) {
            Some(limits) if limits == (initial, maximum) => {}
            Some(limits) => problems.push(format!("{file} declares the table as {limits:?}, the main module as {:?}", (initial, maximum))),
            None => problems.push(format!("{file} has no function table")),
        }
        for (slot, (signature, _)) in slots(module) {
            filled.entry(slot).or_default().push((file.clone(), signature));
        }
    }
    for (slot, fillers) in &filled {
        if fillers.len() > 1 {
            problems.push(format!("table slot {slot} is filled by {} pieces: {:?}", fillers.len(), fillers.iter().map(|f| &f.0).collect::<Vec<_>>()));
        }
        if let Some((_, hole)) = main_slots.get(slot) {
            if !hole {
                problems.push(format!("table slot {slot} is filled by the main module and by {}", fillers[0].0));
            }
        }
    }

    // calls through constant table indices
    let deps: BTreeMap<&String, &Vec<String>> = modules.iter().map(|m| (&m.file, &m.deps)).collect();
    let chunk_files: BTreeSet<&String> = chunks.values().map(|c| &c.file).collect();
    let mut calls = 0usize;
    let mut chunk_to_chunk = 0usize;
    for (file, module) in std::iter::once((&main_name, &main)).chain(pieces.iter()) {
        for (slot, signature) in constant_indirect_calls(module) {
            calls += 1;
            let target = match filled.get(&slot) {
                Some(fillers) => Some((fillers[0].0.clone(), fillers[0].1.clone())),
                None => main_slots.get(&slot).filter(|(_, hole)| !hole).map(|(s, _)| (main_name.clone(), s.clone())),
            };
            let Some((target_file, target_signature)) = target else {
                problems.push(format!("{file} calls table slot {slot}, which nothing fills"));
                continue;
            };
            if target_signature != signature {
                problems.push(format!("{file} calls table slot {slot} as {signature}, but {target_file} fills it with {target_signature}"));
            }
            let caller_is_module = deps.contains_key(file);
            if caller_is_module && chunk_files.contains(&target_file) && !deps[file].contains(&target_file) {
                problems.push(format!("{file} calls into {target_file} (slot {slot}) but does not wait for it"));
            }
            if caller_is_module && deps.contains_key(&target_file) && &target_file != file {
                problems.push(format!("{file} calls into another module, {target_file} (slot {slot})"));
            }
            if chunk_files.contains(file) && chunk_files.contains(&target_file) && &target_file != file {
                chunk_to_chunk += 1;
            }
        }
    }

    // data written by the pieces
    let mut written: BTreeMap<i64, (u8, &String)> = BTreeMap::new();
    let mut conflicts = 0usize;
    let mut bytes = 0usize;
    for (file, module) in &pieces {
        for data in module.data.iter() {
            let DataKind::Active { offset: ConstExpr::Value(ir::Value::I32(offset)), .. } = data.kind else { continue };
            for (i, byte) in data.value.iter().enumerate() {
                let address = i64::from(offset) + i as i64;
                match written.get(&address) {
                    Some((other, by)) if *by != file && *other != *byte => conflicts += 1,
                    Some(_) => {}
                    None => {
                        written.insert(address, (*byte, file));
                        bytes += 1;
                    }
                }
            }
        }
    }
    if conflicts > 0 {
        problems.push(format!("{conflicts} data bytes are written differently by two pieces"));
    }

    println!(
        "{}: main {main_name}, {} chunk(s), {} module(s); {} slots filled by pieces; {calls} constant-index calls ({chunk_to_chunk} chunk to chunk); {bytes} data bytes written by pieces; {} problem(s)",
        site.display(),
        chunks.len(),
        modules.len(),
        filled.len(),
        problems.len()
    );
    Ok(problems)
}

/// Every file below a folder.
fn all_files(folder: &Path) -> Vec<PathBuf> {
    let mut out = Vec::new();
    let mut stack = vec![folder.to_path_buf()];
    while let Some(dir) = stack.pop() {
        let Ok(entries) = std::fs::read_dir(&dir) else { continue };
        for entry in entries.flatten() {
            let path = entry.path();
            if path.is_dir() {
                stack.push(path);
            } else {
                out.push(path);
            }
        }
    }
    out.sort();
    out
}

/// The loader's entries: the chunks by number and the modules, each with the file it fetches.
fn read_loader(path: &Path) -> Result<(BTreeMap<String, Entry>, Vec<Entry>), String> {
    let text = std::fs::read_to_string(path).map_err(|e| format!("{}: {e}", path.display()))?;
    let mut chunk_lines = Vec::new();
    let mut module_lines = Vec::new();
    for line in text.lines() {
        let Some(rest) = line.strip_prefix("export const __wasm_split_load_") else { continue };
        let Some((_, after)) = line.split_once("makeLoad(\"") else { continue };
        let url = after.split('"').next().unwrap_or("");
        let file = url.rsplit('/').next().unwrap_or("").to_string();
        let list = after.split_once('[').and_then(|(_, l)| l.split_once(']')).map(|(l, _)| l).unwrap_or("");
        let deps: Vec<String> = list
            .split(',')
            .map(str::trim)
            .filter(|d| !d.is_empty())
            .map(|d| d.trim_start_matches("__wasm_split_load_chunk_").to_string())
            .collect();
        match rest.strip_prefix("chunk_") {
            Some(number) => chunk_lines.push((number.split(' ').next().unwrap_or("").to_string(), file)),
            None => module_lines.push((file, deps)),
        }
    }
    let chunks: BTreeMap<String, Entry> = chunk_lines
        .into_iter()
        .map(|(number, file)| (number, Entry { file, deps: Vec::new() }))
        .collect();
    let mut modules = Vec::new();
    for (file, numbers) in module_lines {
        let mut deps = Vec::new();
        for number in numbers {
            match chunks.get(&number) {
                Some(chunk) => deps.push(chunk.file.clone()),
                None => return Err(format!("{file} waits for chunk {number}, which the loader does not define")),
            }
        }
        modules.push(Entry { file, deps });
    }
    if modules.is_empty() {
        return Err(format!("{} names no split module", path.display()));
    }
    Ok((chunks, modules))
}

fn funcref_limits(module: &Module) -> Option<(u64, Option<u64>)> {
    module
        .tables
        .iter()
        .find(|t| t.element_ty == RefType::Funcref)
        .map(|t| (t.initial, t.maximum))
}

fn signature(module: &Module, ty: walrus::TypeId) -> String {
    let t = module.types.get(ty);
    format!("{:?} -> {:?}", t.params(), t.results())
}

/// A function that does nothing but trap and takes and gives nothing: the splitter's "dummy",
/// with which the main module fills the slots (holes) the pieces fill when they load.
fn is_hole_filler(module: &Module, id: walrus::FunctionId) -> bool {
    let function = module.funcs.get(id);
    let FunctionKind::Local(local) = &function.kind else { return false };
    let ty = module.types.get(function.ty());
    let instrs = &local.block(local.entry_block()).instrs;
    ty.params().is_empty() && ty.results().is_empty() && instrs.len() == 1 && matches!(instrs[0].0, ir::Instr::Unreachable(_))
}

/// slot -> (signature, whether the function is the main module's hole filler) for every active
/// element segment at a constant offset.
fn slots(module: &Module) -> BTreeMap<i32, (String, bool)> {
    let mut out = BTreeMap::new();
    for element in module.elements.iter() {
        let ElementKind::Active { offset: ConstExpr::Value(ir::Value::I32(offset)), .. } = element.kind else { continue };
        let functions: Vec<walrus::FunctionId> = match &element.items {
            ElementItems::Functions(v) => v.clone(),
            ElementItems::Expressions(_, v) => v
                .iter()
                .filter_map(|c| if let ConstExpr::RefFunc(f) = c { Some(*f) } else { None })
                .collect(),
        };
        for (i, f) in functions.into_iter().enumerate() {
            out.insert(offset + i as i32, (signature(module, module.funcs.get(f).ty()), is_hole_filler(module, f)));
        }
    }
    out
}

/// (slot, signature) of every `i32.const K` directly followed by a `call_indirect` on the
/// function table, in every function.
fn constant_indirect_calls(module: &Module) -> Vec<(i32, String)> {
    let table = module.tables.iter().find(|t| t.element_ty == RefType::Funcref).map(|t| t.id());
    let mut out = Vec::new();
    for function in module.funcs.iter() {
        let FunctionKind::Local(local) = &function.kind else { continue };
        let mut sequences = vec![local.entry_block()];
        let mut seen = BTreeSet::new();
        while let Some(id) = sequences.pop() {
            if !seen.insert(id) {
                continue;
            }
            let instrs = &local.block(id).instrs;
            for (i, (instr, _)) in instrs.iter().enumerate() {
                match instr {
                    ir::Instr::Block(b) => sequences.push(b.seq),
                    ir::Instr::Loop(l) => sequences.push(l.seq),
                    ir::Instr::IfElse(e) => {
                        sequences.push(e.consequent);
                        sequences.push(e.alternative);
                    }
                    ir::Instr::CallIndirect(call) if Some(call.table) == table && i > 0 => {
                        if let ir::Instr::Const(ir::Const { value: ir::Value::I32(slot) }) = instrs[i - 1].0 {
                            out.push((slot, signature(module, call.ty)));
                        }
                    }
                    _ => {}
                }
            }
        }
    }
    out
}
