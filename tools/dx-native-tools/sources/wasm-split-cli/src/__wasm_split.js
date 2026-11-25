// when running the harness we need to make sure to uncommon this out...

// CodeBrix change: a file is fetched and instantiated once, also when it is asked for again
// while it is still loading (a shared chunk is asked for by every module that needs it); the
// returned function answers whether the file is loaded, and a module is not instantiated when
// a chunk it needs did not load. See wasm-split-cli's PATCH-NOTES.txt, patch 0006.
export function makeLoad(url, deps, fusedImports, initIt) {
  let alreadyLoaded = false;
  let loading = null;
  return async (callbackIndex, callbackData) => {
    const depsLoaded = await Promise.all(deps.map((dep) => dep()));
    if (alreadyLoaded) return true;
    try {
      if (depsLoaded.includes(false)) {
        throw new Error("a chunk this module needs did not load");
      }

      const initSync = initIt || globalThis.__wasm_split_main_initSync;
      const mainExports = initSync(undefined, undefined);

      if (loading === null) {
        loading = (async () => {
          const response = await fetch(url);

          let imports = {
            env: {
              memory: mainExports.memory,
            },
            __wasm_split: {
              __indirect_function_table: mainExports.__indirect_function_table,
              __stack_pointer: mainExports.__stack_pointer,
              __tls_base: mainExports.__tls_base,
              memory: mainExports.memory,
            },
          };

          for (let mainExport in mainExports) {
            imports["__wasm_split"][mainExport] = mainExports[mainExport];
          }

          for (let name in fusedImports) {
            imports["__wasm_split"][name] = fusedImports[name];
          }

          let new_exports = await WebAssembly.instantiateStreaming(
            response,
            imports
          );

          alreadyLoaded = true;

          for (let name in new_exports.instance.exports) {
            fusedImports[name] = new_exports.instance.exports[name];
          }
        })();
      }

      try {
        await loading;
      } finally {
        // a load that failed is tried again by the next call
        loading = null;
      }

      if (callbackIndex !== undefined) {
        mainExports.__indirect_function_table.get(callbackIndex)(
          callbackData,
          true
        );
      }

      return true;
    } catch (e) {
      console.error(
        "Failed to load wasm-split module",
        e,
        url,
        deps,
        fusedImports
      );
      return false;
    }
  };
}

let fusedImports = {};
