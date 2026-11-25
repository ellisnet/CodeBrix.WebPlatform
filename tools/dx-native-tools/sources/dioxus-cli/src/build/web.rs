//! ### Web:
//!
//! Create a folder that is somewhat similar to an app-image (exe + asset)
//! The server is dropped into the `web` folder, even if there's no `public` folder.
//! If there's no server (SPA), we still use the `web` folder, but it only contains the
//! public folder.
//!
//! ```
//! web/
//!     server
//!     assets/
//!     public/
//!         index.html
//!         wasm/
//!            app.wasm
//!            glue.js
//!            snippets/
//!                ...
//!         assets/
//!            logo.png
//! ```
//!
//! ### Linux:
//!
//! <https://docs.appimage.org/reference/appdir.html#ref-appdir>
//! current_exe.join("Assets")
//! ```
//! app.appimage/
//!     AppRun
//!     app.desktop
//!     package.json
//!     assets/
//!         logo.png
//! ```

use crate::{
    opt::{js_is_module, AppManifest},
    BuildMode, BuildRequest,
};
use crate::{BuildContext, BundleFormat, Result, TraceSrc, WasmBindgen, WasmOptConfig};
use anyhow::Context;
use dioxus_cli_config::format_base_path_meta_element;
use manganis::AssetOptions;
use manganis_core::AssetVariant;
use std::{
    io::Write,
    path::{Path, PathBuf},
};
use uuid::Uuid;

impl BuildRequest {
    pub async fn verify_web_tooling(&self) -> Result<()> {
        // Wasm bindgen
        let krate_bindgen_version =
            self.workspace
                .wasm_bindgen_version()
                .ok_or(anyhow::anyhow!(
                    "failed to detect wasm-bindgen version, unable to proceed"
                ))?;

        WasmBindgen::verify_install(&krate_bindgen_version).await?;

        // esbuild is used for JS asset processing
        //
        // CodeBrix change: a missing esbuild no longer stops the build. Without it the asset
        // pipeline copies the generated JavaScript as it is, with wasm-bindgen's snippets
        // folder beside it (see opt/js.rs), so the JavaScript is neither bundled into one file
        // nor minified. See tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt.
        if let Err(err) = crate::esbuild::Esbuild::get_or_install().await {
            tracing::debug!("esbuild is not available; JavaScript will not be minified: {err}");
        }

        Ok(())
    }

    /// Bundle the web app
    /// - Run wasm-bindgen
    /// - Bundle split
    /// - Run wasm-opt
    /// - Register the .wasm and .js files with the asset system
    pub async fn bundle_web(
        &self,
        ctx: &BuildContext,
        exe: &Path,
        assets: &mut AppManifest,
    ) -> Result<()> {
        use crate::{wasm_bindgen::WasmBindgen, wasm_opt};
        use std::fmt::Write;

        // Locate the output of the build files and the bindgen output
        // We'll fill these in a second if they don't already exist
        let bindgen_outdir = self.wasm_bindgen_out_dir();
        let post_bindgen_wasm = self.wasm_bindgen_wasm_output_file();
        let should_bundle_split: bool = self.wasm_split;
        let bindgen_version = self
            .workspace
            .wasm_bindgen_version()
            .expect("this should have been checked by tool verification");

        // Prepare any work dirs
        _ = std::fs::remove_dir_all(&bindgen_outdir);
        std::fs::create_dir_all(&bindgen_outdir)?;

        // Lift the internal functions to exports
        if ctx.mode == BuildMode::Fat {
            let unprocessed = std::fs::read(exe)?;
            let all_exported_bytes = crate::build::prepare_wasm_base_module(&unprocessed)?;
            std::fs::write(exe, all_exported_bytes)?;
        }

        // Prepare our configuration
        //
        // we turn on debug symbols in dev mode
        //
        // We leave demangling to false since it's faster and these tools seem to prefer the raw symbols.
        // todo(jon): investigate if the chrome extension needs them demangled or demangles them automatically.
        let keep_debug = self.config.web.wasm_opt.debug
            || self.debug_symbols
            || self.wasm_split
            || !self.release
            || ctx.mode == BuildMode::Fat;
        let keep_names = self.config.web.wasm_opt.keep_names
            || self.keep_names
            || self.wasm_split
            || ctx.mode == BuildMode::Fat;
        let demangle = false;
        let wasm_opt_options = WasmOptConfig {
            memory_packing: self.wasm_split,
            debug: self.debug_symbols,
            ..self.config.web.wasm_opt.clone()
        };

        // Run wasm-bindgen. Some of the options are not "optimal" but will be fixed up by wasm-opt
        //
        // There's performance implications here. Running with --debug is slower than without
        // We're keeping around lld sections and names but wasm-opt will fix them
        // todo(jon): investigate a good balance of wiping debug symbols during dev (or doing a double build?)
        ctx.status_wasm_bindgen_start();
        tracing::debug!(dx_src = ?TraceSrc::Bundle, "Running wasm-bindgen");
        let start = std::time::Instant::now();
        WasmBindgen::new(&bindgen_version)
            .input_path(exe)
            .target("web")
            .debug(keep_debug)
            .demangle(demangle)
            .keep_debug(keep_debug)
            .keep_lld_exports(true)
            .out_name(self.executable_name())
            .out_dir(&bindgen_outdir)
            .remove_name_section(!keep_names)
            .remove_producers_section(!keep_names)
            .run()
            .await
            .context("Failed to generate wasm-bindgen bindings")?;
        tracing::debug!(dx_src = ?TraceSrc::Bundle, "wasm-bindgen complete in {:?}", start.elapsed());

        // Run bundle splitting if the user has requested it
        // It's pretty expensive but because of rayon should be running separate threads, hopefully
        // not blocking this thread. Dunno if that's true
        if should_bundle_split {
            ctx.status_splitting_bundle();

            // Load the contents of these binaries since we need both of them
            // We're going to use the default makeLoad glue from wasm-split
            let original = std::fs::read(exe)?;
            let bindgened = std::fs::read(&post_bindgen_wasm)?;
            let mut glue = wasm_split_cli::MAKE_LOAD_JS.to_string();

            // Run the emitter
            let splitter = wasm_split_cli::Splitter::new(&original, &bindgened);
            let modules = splitter
                .context("Failed to parse wasm for splitter")?
                .emit()
                .context("Failed to emit wasm split modules")?;

            // Write the chunks that contain shared imports
            // These will be in the format of chunk_0_modulename.wasm - this is hardcoded in wasm-split
            tracing::debug!("Writing split chunks to disk");
            for (idx, chunk) in modules.chunks.iter().enumerate() {
                let path = bindgen_outdir.join(format!("chunk_{}_{}.wasm", idx, chunk.module_name));
                wasm_opt::write_wasm(&chunk.bytes, &path, &wasm_opt_options).await?;
                // CodeBrix change: the address starts "/assets/" for an application at the site
                // root (upstream: "/./assets/"); see base_path_prefix.
                writeln!(
                    glue, "export const __wasm_split_load_chunk_{idx} = makeLoad(\"{base_path}/assets/{url}\", [], fusedImports);",
                    base_path = self.base_path_prefix(),
                    url = assets
                        .register_asset(&path, AssetOptions::builder().into_asset_options())?.bundled_path(),
                )?;
            }

            // Write the modules that contain the entrypoints
            tracing::debug!("Writing split modules to disk");
            for (idx, module) in modules.modules.iter().enumerate() {
                let comp_name = module
                    .component_name
                    .as_ref()
                    .context("generated bindgen module has no name?")?;

                let path = bindgen_outdir.join(format!("module_{idx}_{comp_name}.wasm"));
                wasm_opt::write_wasm(&module.bytes, &path, &wasm_opt_options).await?;

                let hash_id = module
                    .hash_id
                    .as_ref()
                    .context("generated wasm-split bindgen module has no hash id?")?;

                // CodeBrix change: "/assets/" at the site root, not "/./assets/"; see
                // base_path_prefix.
                writeln!(
                    glue,
                    "export const __wasm_split_load_{module}_{hash_id}_{comp_name} = makeLoad(\"{base_path}/assets/{url}\", [{deps}], fusedImports);",
                    module = module.module_name,

                    base_path = self.base_path_prefix(),

                    // Again, register this wasm with the asset system
                    url = assets
                        .register_asset(&path, AssetOptions::builder().into_asset_options())?
                        .bundled_path(),

                    // This time, make sure to write the dependencies of this chunk
                    // The names here are again, hardcoded in wasm-split - fix this eventually.
                    deps = module
                        .relies_on_chunks
                        .iter()
                        .map(|idx| format!("__wasm_split_load_chunk_{idx}"))
                        .collect::<Vec<_>>()
                        .join(", ")
                )?;
            }

            // Write the js binding
            // It's not registered as an asset since it will get included in the main.js file
            let js_output_path = bindgen_outdir.join("__wasm_split.js");
            std::fs::write(&js_output_path, &glue)?;

            // Make sure to write some entropy to the main.js file so it gets a new hash
            // If we don't do this, the main.js file will be cached and never pick up the chunk names
            let uuid = Uuid::new_v5(&Uuid::NAMESPACE_URL, glue.as_bytes());
            std::fs::OpenOptions::new()
                .append(true)
                .open(self.wasm_bindgen_js_output_file())
                .context("Failed to open main.js file")?
                .write_all(format!("/*{uuid}*/").as_bytes())?;

            // Write the main wasm_bindgen file and register it with the asset system
            // This will overwrite the file in place
            // We will wasm-opt it in just a second...
            std::fs::write(&post_bindgen_wasm, modules.main.bytes).unwrap();
        }

        // CodeBrix change: a build WITHOUT bundle splitting (no `--wasm-split`, and every
        // `dx serve`) of an application that has split points (`wasm_split::lazy_loader!`,
        // `#[component(lazy)]`, `#[wasm_split]`) still imports their loader,
        // `./__wasm_split.js`, which upstream writes only for a split build, so the page never
        // started. dx now writes a stand-in loader for such a build: every split point reports
        // itself as already loaded and every call into a piece goes to the same function in the
        // main module, where all of the code is. See
        // tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt.
        if !should_bundle_split {
            write_unsplit_split_loader(&post_bindgen_wasm, &bindgen_outdir)
                .context("Failed to write the stand-in loader for the split points of an unsplit build")?;
        }

        if matches!(ctx.mode, BuildMode::Fat) {
            // add `export { __wbg_get_imports };` to the end of the wasmbindgen js file
            let mut js = std::fs::read(self.wasm_bindgen_js_output_file())?;
            writeln!(js, "\nexport {{ __wbg_get_imports }};")?;
            std::fs::write(self.wasm_bindgen_js_output_file(), js)?;
        }

        // Make sure to optimize the main wasm file if requested or if bundle splitting
        //
        // CodeBrix change: a release build leaves the main module unshrunk when the caller sets
        // the environment variable DX_SKIP_WASM_OPT to anything but an empty value, "0" or
        // "false". wasm-opt is then neither looked for nor run, so the build needs none. A build
        // with bundle splitting is not affected: it runs wasm-opt as upstream does. See
        // tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt.
        let skip_wasm_opt = !should_bundle_split
            && std::env::var("DX_SKIP_WASM_OPT").is_ok_and(|value| {
                let value = value.trim();
                !value.is_empty() && value != "0" && !value.eq_ignore_ascii_case("false")
            });
        if skip_wasm_opt && self.release {
            tracing::info!("Skipping wasm-opt at the caller's request (DX_SKIP_WASM_OPT is set): the WebAssembly is not shrunk");
        }
        if (should_bundle_split || self.release) && !skip_wasm_opt {
            ctx.status_optimizing_wasm();
            wasm_opt::optimize(&post_bindgen_wasm, &post_bindgen_wasm, &wasm_opt_options).await?;
        }

        if self.should_bundle_to_asset() {
            // Make sure to register the main wasm file with the asset system
            assets.register_asset(
                &post_bindgen_wasm,
                AssetOptions::builder().into_asset_options(),
            )?;
        }

        // Now that the wasm is registered as an asset, we can write the js glue shim
        self.write_js_glue_shim(assets)?;

        if self.should_bundle_to_asset() {
            // Register the main.js with the asset system so it bundles in the snippets and optimizes
            assets.register_asset(
                &self.wasm_bindgen_js_output_file(),
                AssetOptions::js()
                    .with_minify(true)
                    .with_preload(true)
                    .into_asset_options(),
            )?;
        }

        // Write the index.html file with the pre-configured contents we got from pre-rendering
        self.write_index_html(assets)?;

        Ok(())
    }

    fn write_js_glue_shim(&self, assets: &AppManifest) -> Result<()> {
        let wasm_path = self.bundled_wasm_path(assets);

        // Load and initialize wasm without requiring a separate javascript file.
        // This also allows using a strict Content-Security-Policy.
        let mut js = std::fs::OpenOptions::new()
            .append(true)
            .open(self.wasm_bindgen_js_output_file())?;
        let mut buf_writer = std::io::BufWriter::new(&mut js);
        // CodeBrix change: the .wasm address has no "/./" for an application at the site root;
        // see base_path_prefix.
        writeln!(
            buf_writer,
            r#"
globalThis.__wasm_split_main_initSync = initSync;

// Actually perform the load
__wbg_init({{module_or_path: "{}/{wasm_path}"}}).then((wasm) => {{
    // assign this module to be accessible globally
    globalThis.__dx_mainWasm = wasm;
    globalThis.__dx_mainInit = __wbg_init;
    globalThis.__dx_mainInitSync = initSync;
    globalThis.__dx___wbg_get_imports = __wbg_get_imports;

    if (wasm.__wbindgen_start == undefined) {{
        wasm.main();
    }}
}});
"#,
            self.base_path_prefix(),
        )?;

        Ok(())
    }

    /// Write the index.html file to the output directory. This must be called after the wasm and js
    /// assets are registered with the asset system if this is a release build.
    pub(crate) fn write_index_html(&self, assets: &AppManifest) -> Result<()> {
        let wasm_path = self.bundled_wasm_path(assets);
        let js_path = self.bundled_js_path(assets);

        // Write the index.html file with the pre-configured contents we got from pre-rendering
        std::fs::write(
            self.root_dir().join("index.html"),
            self.prepare_html(assets, &wasm_path, &js_path).unwrap(),
        )?;

        Ok(())
    }

    fn bundled_js_path(&self, assets: &AppManifest) -> String {
        let wasm_bindgen_js_out = self.wasm_bindgen_js_output_file();
        if self.should_bundle_to_asset() {
            let name = assets
                .get_first_asset_for_source(&wasm_bindgen_js_out)
                .expect("The js source must exist before creating index.html");
            format!("assets/{}", name.bundled_path())
        } else {
            format!(
                "wasm/{}",
                wasm_bindgen_js_out.file_name().unwrap().to_str().unwrap()
            )
        }
    }

    /// Get the path to the wasm-bindgen output files. Either the direct file or the optimized one depending on the build mode
    fn bundled_wasm_path(&self, assets: &AppManifest) -> String {
        let wasm_bindgen_wasm_out = self.wasm_bindgen_wasm_output_file();
        if self.should_bundle_to_asset() {
            let name = assets
                .get_first_asset_for_source(&wasm_bindgen_wasm_out)
                .expect("The wasm source must exist before creating index.html");
            format!("assets/{}", name.bundled_path())
        } else {
            format!(
                "wasm/{}",
                wasm_bindgen_wasm_out.file_name().unwrap().to_str().unwrap()
            )
        }
    }

    /// Users create an index.html for their SPA if they want it
    ///
    /// We always write our wasm as main.js and main_bg.wasm
    ///
    /// In prod we run the optimizer which bundles everything together properly
    ///
    /// So their index.html needs to include main.js in the scripts otherwise nothing happens?
    ///
    /// Seems like every platform has a weird file that declares a bunch of stuff
    /// - web: index.html
    /// - ios: info.plist
    /// - macos: info.plist
    /// - linux: appimage root thing?
    /// - android: androidmanifest.xml
    ///
    /// You also might different variants of these files (staging / prod) and different flavors (eu/us)
    ///
    /// web's index.html is weird since it's not just a bundle format but also a *content* format
    pub(crate) fn prepare_html(
        &self,
        assets: &AppManifest,
        wasm_path: &str,
        js_path: &str,
    ) -> Result<String> {
        let mut html = {
            const DEV_DEFAULT_HTML: &str = include_str!("../../assets/web/dev.index.html");
            const PROD_DEFAULT_HTML: &str = include_str!("../../assets/web/prod.index.html");

            let crate_root: &Path = &self.crate_dir();
            let custom_html_file = crate_root.join("index.html");
            let default_html = match self.release {
                true => PROD_DEFAULT_HTML,
                false => DEV_DEFAULT_HTML,
            };
            std::fs::read_to_string(custom_html_file).unwrap_or_else(|_| String::from(default_html))
        };

        // Inject any resources from the config into the html
        self.inject_resources(assets, &mut html)?;

        // Inject loading scripts if they are not already present
        self.inject_loading_scripts(assets, &mut html);

        // Replace any special placeholders in the HTML with resolved values
        self.replace_template_placeholders(&mut html, wasm_path, js_path);

        let title = self.config.web.app.title.clone();
        Self::replace_or_insert_before("{app_title}", "</title", &title, &mut html);

        Ok(html)
    }

    fn is_dev_build(&self) -> bool {
        !self.release
    }

    // Inject any resources from the config into the html
    fn inject_resources(&self, assets: &AppManifest, html: &mut String) -> Result<()> {
        use std::fmt::Write;

        // Collect all resources into a list of styles and scripts
        let resources = &self.config.web.resource;
        let mut style_list = resources.style.clone().unwrap_or_default();
        let mut script_list = resources.script.clone().unwrap_or_default();

        if self.is_dev_build() {
            style_list.extend(resources.dev.style.iter().cloned());
            script_list.extend(resources.dev.script.iter().cloned());
        }

        let mut head_resources = String::new();

        // Add all styles to the head
        for style in &style_list {
            writeln!(
                &mut head_resources,
                "<link rel=\"stylesheet\" href=\"{}\">",
                &style.to_str().unwrap(),
            )?;
        }

        // Add all scripts to the head
        for script in &script_list {
            writeln!(
                &mut head_resources,
                "<script src=\"{}\"></script>",
                &script.to_str().unwrap(),
            )?;
        }

        // Add the base path to the head if this is a debug build
        if self.is_dev_build() {
            if let Some(base_path) = &self.trimmed_base_path() {
                head_resources.push_str(&format_base_path_meta_element(base_path));
            }
        }

        // Inject any resources from manganis into the head
        for asset in assets.unique_assets() {
            let asset_path = asset.bundled_path();
            match asset.options().variant() {
                AssetVariant::Css(css_options) => {
                    if css_options.preloaded() {
                        _ = write!(
                            head_resources,
                            r#"<link rel="preload" as="style" href="/{{base_path}}/assets/{asset_path}" crossorigin>"#
                        );
                    }
                    if css_options.static_head() {
                        _ = write!(
                            head_resources,
                            r#"<link rel="stylesheet" href="/{{base_path}}/assets/{asset_path}" type="text/css">"#
                        );
                    }
                }
                AssetVariant::Image(image_options) => {
                    if image_options.preloaded() {
                        _ = write!(
                            head_resources,
                            r#"<link rel="preload" as="image" href="/{{base_path}}/assets/{asset_path}" crossorigin>"#
                        );
                    }
                }
                AssetVariant::Js(js_options) => {
                    if js_options.preloaded() {
                        _ = write!(
                            head_resources,
                            r#"<link rel="preload" as="script" href="/{{base_path}}/assets/{asset_path}" crossorigin>"#
                        );
                    }
                    if js_options.static_head() {
                        let source = std::path::Path::new(asset.absolute_source_path());
                        let module_attr = if js_is_module(js_options, source) {
                            r#" type="module""#
                        } else {
                            ""
                        };
                        _ = write!(
                            head_resources,
                            r#"<script{module_attr} src="/{{base_path}}/assets/{asset_path}"></script>"#
                        );
                    }
                }
                _ => {}
            }
        }

        // Do not preload the wasm file, because in Safari, preload as=fetch requires additional fetch() options to exactly match the network request
        // And if they do not match then Safari downloads the wasm file twice.
        // See https://github.com/wasm-bindgen/wasm-bindgen/blob/ac51055a4c39fa0affe02f7b63fb1d4c9b3ddfaf/crates/cli-support/src/js/mod.rs#L967
        Self::replace_or_insert_before("{style_include}", "</head", &head_resources, html);

        Ok(())
    }

    /// Inject loading scripts if they are not already present
    fn inject_loading_scripts(&self, assets: &AppManifest, html: &mut String) {
        // If the current build opted out of injecting loading scripts, don't inject anything
        if !self.inject_loading_scripts {
            return;
        }

        // If not, insert the script
        //
        // CodeBrix change: src="/wasm/..." or "/assets/..." for an application at the site root
        // (upstream: "/./..."); see base_path_prefix.
        *html = html.replace(
            "</body",
            &format!(
                r#"<script type="module" async src="{}/{}"></script>
            </body"#,
                self.base_path_prefix(),
                self.bundled_js_path(assets)
            ),
        );
    }

    /// Replace any special placeholders in the HTML with resolved values
    fn replace_template_placeholders(&self, html: &mut String, wasm_path: &str, js_path: &str) {
        // CodeBrix change: an address written "/{base_path}/..." (the form dx's own head
        // resources and the documented index.html use) becomes "/..." for an application at the
        // site root instead of "/./..."; a {base_path} anywhere else is replaced as upstream
        // does. See base_path_prefix.
        *html = html.replace("/{base_path}/", &format!("{}/", self.base_path_prefix()));
        let base_path = self.base_path_or_default();
        *html = html.replace("{base_path}", base_path);

        let app_name = &self.executable_name();

        // If the html contains the old `{app_name}` placeholder, replace {app_name}_bg.wasm and {app_name}.js
        // with the new paths
        *html = html.replace("wasm/{app_name}_bg.wasm", wasm_path);
        *html = html.replace("wasm/{app_name}.js", js_path);

        // Otherwise replace the new placeholders
        *html = html.replace("{wasm_path}", wasm_path);
        *html = html.replace("{js_path}", js_path);

        // Replace the app_name if we find it anywhere standalone
        *html = html.replace("{app_name}", app_name);
    }

    /// Replace a string or insert the new contents before a marker
    fn replace_or_insert_before(
        replace: &str,
        or_insert_before: &str,
        with: &str,
        content: &mut String,
    ) {
        if content.contains(replace) {
            *content = content.replace(replace, with);
        } else if let Some(pos) = content.find(or_insert_before) {
            content.insert_str(pos, with);
        }
    }

    /// Check if assets should be pre_compressed. This will only be true in release mode if the user
    /// has enabled pre_compress in the web config.
    pub fn should_pre_compress_web_assets(&self, release: bool) -> bool {
        self.config.web.pre_compress & release
    }

    /// Check if the wasm output should be bundled to an asset type app.
    pub(crate) fn should_bundle_to_asset(&self) -> bool {
        self.release && self.bundle == BundleFormat::Web
    }

    /// CodeBrix change: what an absolute address of the application's files starts with before
    /// "/assets/..." or "/wasm/...": "" for an application at the site root, "/<base path>"
    /// otherwise. Upstream writes "/{base_path_or_default}/", which is "/./" at the site root.
    /// See tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt.
    fn base_path_prefix(&self) -> String {
        self.trimmed_base_path()
            .map(|path| format!("/{path}"))
            .unwrap_or_default()
    }

    /// Get the path to the wasm bindgen temporary output folder
    pub fn wasm_bindgen_out_dir(&self) -> PathBuf {
        self.root_dir().join("wasm")
    }

    /// Get the path to the wasm bindgen javascript output file
    pub(crate) fn wasm_bindgen_js_output_file(&self) -> PathBuf {
        self.wasm_bindgen_out_dir()
            .join(self.executable_name())
            .with_extension("js")
    }

    /// Get the path to the wasm bindgen wasm output file
    pub(crate) fn wasm_bindgen_wasm_output_file(&self) -> PathBuf {
        self.wasm_bindgen_out_dir()
            .join(format!("{}_bg", self.executable_name()))
            .with_extension("wasm")
    }

    pub(crate) fn path_is_in_public_dir(&self, path: &Path) -> bool {
        let Some(static_dir) = self.user_public_dir() else {
            return false;
        };

        // Canonicalize when possible so we work with editors that use tmp files
        let canonical_static =
            dunce::canonicalize(&static_dir).unwrap_or_else(|_| static_dir.clone());
        let canonical_path = dunce::canonicalize(path).unwrap_or_else(|_| path.to_path_buf());

        canonical_path.starts_with(&canonical_static)
    }
}

// CodeBrix change: the stand-in split loader of a build without bundle splitting (see the call
// in `bundle_web` and tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt).

/// The module name under which the `wasm_split` macros import a split point's loader.
const SPLIT_LOADER_MODULE: &str = "./__wasm_split.js";

/// The name under which the stand-in loader finds the module's function table.
const FUNCTION_TABLE_EXPORT: &str = "__indirect_function_table";

/// Write `__wasm_split.js` beside the wasm-bindgen output of a build without bundle splitting
/// when the module imports from it (CodeBrix change). Nothing is written for a module without
/// split points.
///
/// The macros import two kinds of function from it for every split point:
/// `__wasm_split_load_<module>_<hash>_<name>(callback, data)`, which must call the function
/// whose table index is `callback` with `(data, true)` once the piece is loaded, and
/// `__wasm_split_00___<module>___00_import_<hash>_<name>(...)`, the piece's entry point, which
/// the main module also EXPORTS (as `..._export_...`) when the build is not split. The stand-in
/// answers every load at once (in a microtask, as the real loader does after its fetch) and
/// forwards every entry point to that export. The callback needs the function table, which an
/// unsplit module does not export, so it is exported here under `__indirect_function_table`
/// (the name a split main module and a hot-patch build export it under).
fn write_unsplit_split_loader(wasm_path: &Path, out_dir: &Path) -> Result<()> {
    use wasmparser::{ExternalKind, Payload, TypeRef};

    let bytes = std::fs::read(wasm_path)?;
    let mut loads = Vec::new();
    let mut entry_points = Vec::new();
    let mut unknown = Vec::new();
    let mut table_export = None;
    let mut tables = 0u32;
    for payload in wasmparser::Parser::new(0).parse_all(&bytes) {
        match payload? {
            Payload::ImportSection(reader) => {
                for import in reader {
                    let import = import?;
                    if matches!(import.ty, TypeRef::Table(_)) {
                        tables += 1;
                    }
                    if import.module != SPLIT_LOADER_MODULE {
                        continue;
                    }
                    let name = import.name.to_string();
                    if !matches!(import.ty, TypeRef::Func(_)) {
                        unknown.push(name);
                    } else if name.starts_with("__wasm_split_load_") {
                        loads.push(name);
                    } else if name.starts_with("__wasm_split_00") && name.contains("___00_import_") {
                        entry_points.push(name);
                    } else {
                        unknown.push(name);
                    }
                }
            }
            Payload::TableSection(reader) => tables += reader.count(),
            Payload::ExportSection(reader) => {
                for export in reader {
                    let export = export?;
                    if export.kind == ExternalKind::Table && export.index == 0 {
                        table_export = Some(export.name.to_string());
                    }
                }
            }
            _ => {}
        }
    }

    if loads.is_empty() && entry_points.is_empty() && unknown.is_empty() {
        return Ok(());
    }

    let table_export = match table_export {
        Some(name) => name,
        None if !loads.is_empty() => {
            anyhow::ensure!(
                tables > 0,
                "the module imports split loaders but has no function table"
            );
            let exported = add_table_export(&bytes, FUNCTION_TABLE_EXPORT)?;
            std::fs::write(wasm_path, exported)?;
            FUNCTION_TABLE_EXPORT.to_string()
        }
        None => FUNCTION_TABLE_EXPORT.to_string(),
    };

    let mut js = String::new();
    js.push_str(
        "// The split-point loader of a build WITHOUT bundle splitting, written by dx: all of the\n\
         // application's code is in the main module, so every split point is already loaded and\n\
         // every call into a piece goes to the same function of the main module.\n",
    );
    js.push_str(
        "const main = () => globalThis.__wasm_split_main_initSync(undefined, undefined);\n",
    );
    js.push_str(&format!(
        "const loaded = (callbackIndex, callbackData) => {{\n  Promise.resolve().then(() => main()[{table}].get(callbackIndex)(callbackData, true));\n}};\n",
        table = js_string(&table_export),
    ));
    for name in &loads {
        js.push_str(&format!("export const {name} = loaded;\n"));
    }
    for name in &entry_points {
        let export = name.replacen("___00_import_", "___00_export_", 1);
        js.push_str(&format!(
            "export const {name} = (...args) => main()[{export}](...args);\n",
            export = js_string(&export),
        ));
    }
    for name in &unknown {
        js.push_str(&format!(
            "export const {name} = () => {{ throw new Error({message}); }};\n",
            message = js_string(&format!(
                "{name} is not a split-point function dx knows; build with --wasm-split"
            )),
        ));
    }

    std::fs::write(out_dir.join("__wasm_split.js"), js)?;
    tracing::debug!(
        "Wrote the stand-in split loader for {} split point(s) of an unsplit build",
        loads.len()
    );
    Ok(())
}

/// Return a copy of a WebAssembly module with one more export: table 0 under `name`
/// (CodeBrix change). Only the export section changes; every other section is copied byte for
/// byte, so offsets inside the code section (DWARF) stay valid.
fn add_table_export(bytes: &[u8], name: &str) -> Result<Vec<u8>> {
    fn read_leb(bytes: &[u8], at: &mut usize) -> Result<u32> {
        let mut value = 0u32;
        let mut shift = 0;
        loop {
            let byte = *bytes.get(*at).context("truncated WebAssembly module")?;
            *at += 1;
            value |= u32::from(byte & 0x7f) << shift;
            if byte & 0x80 == 0 {
                return Ok(value);
            }
            shift += 7;
            anyhow::ensure!(shift < 35, "bad LEB128 number in WebAssembly module");
        }
    }
    fn write_leb(out: &mut Vec<u8>, mut value: u32) {
        loop {
            let byte = (value & 0x7f) as u8;
            value >>= 7;
            if value == 0 {
                out.push(byte);
                return;
            }
            out.push(byte | 0x80);
        }
    }

    anyhow::ensure!(
        bytes.len() >= 8 && bytes[..4] == *b"\0asm",
        "not a WebAssembly module"
    );
    let mut at = 8;
    while at < bytes.len() {
        let section_start = at;
        let id = bytes[at];
        at += 1;
        let size = read_leb(bytes, &mut at)? as usize;
        let content_start = at;
        let content_end = content_start + size;
        anyhow::ensure!(content_end <= bytes.len(), "truncated WebAssembly module");
        if id == 7 {
            let mut entries_start = content_start;
            let count = read_leb(bytes, &mut entries_start)?;
            let mut content = Vec::with_capacity(size + name.len() + 8);
            write_leb(&mut content, count + 1);
            content.extend_from_slice(&bytes[entries_start..content_end]);
            write_leb(&mut content, name.len() as u32);
            content.extend_from_slice(name.as_bytes());
            content.push(0x01); // a table
            write_leb(&mut content, 0); // table 0, the one function pointers index

            let mut out = Vec::with_capacity(bytes.len() + name.len() + 16);
            out.extend_from_slice(&bytes[..section_start]);
            out.push(7);
            write_leb(&mut out, content.len() as u32);
            out.extend_from_slice(&content);
            out.extend_from_slice(&bytes[content_end..]);
            return Ok(out);
        }
        at = content_end;
    }
    anyhow::bail!("the WebAssembly module has no export section")
}

/// A JavaScript string literal for an export name (CodeBrix change).
fn js_string(value: &str) -> String {
    serde_json::to_string(value).unwrap_or_else(|_| format!("\"{value}\""))
}
