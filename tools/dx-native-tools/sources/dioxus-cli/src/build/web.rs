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
        // CodeBrix change: a build with bundle splitting checks DX_WASM_SPLIT_CHUNK_THRESHOLD and
        // DX_WASM_SPLIT_CHUNK_LIMIT before any work, so that a bad value stops it at once with a
        // message of its own. See tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt.
        if self.wasm_split {
            split_chunk_options()?;
        }

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
        // CodeBrix change: wasm-bindgen's --debug (extra checks and error logging in the
        // generated JavaScript) only when the build asks for debug output for its own sake. A
        // release build with bundle splitting needs --keep-debug (the splitter reads the
        // sections it keeps) but not --debug, which upstream passed with it; the WebAssembly
        // wasm-bindgen writes is the same either way. See
        // tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt.
        let bindgen_debug = self.config.web.wasm_opt.debug
            || self.debug_symbols
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
            .debug(bindgen_debug)
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
            //
            // CodeBrix change: the grouping of shared code into chunks comes from the environment
            // variables DX_WASM_SPLIT_CHUNK_THRESHOLD and DX_WASM_SPLIT_CHUNK_LIMIT (unset: one
            // chunk, as before; checked at the start of the build, verify_web_tooling), and one
            // line says what was made. See tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt.
            let split_options = split_chunk_options()?;
            let splitter = wasm_split_cli::Splitter::new_with_options(
                &original,
                &bindgened,
                split_options.clone(),
            );
            let modules = splitter
                .context("Failed to parse wasm for splitter")?
                .emit()
                .context("Failed to emit wasm split modules")?;
            tracing::info!(
                "{}",
                describe_shared_chunks(modules.chunks.len(), &split_options)
            );

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
            //
            // CodeBrix change: a module's file is named after its piece (split_module_file_stems),
            // not after the function the split point calls. See
            // tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt.
            tracing::debug!("Writing split modules to disk");
            let file_stems = split_module_file_stems(&modules.modules)?;
            for (idx, module) in modules.modules.iter().enumerate() {
                let comp_name = module
                    .component_name
                    .as_ref()
                    .context("generated bindgen module has no name?")?;

                let path = bindgen_outdir.join(format!("module_{idx}_{}.wasm", file_stems[idx]));
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

// CodeBrix change: how a build with bundle splitting groups shared code into chunks (see the calls
// in `verify_web_tooling` and `bundle_web`, and tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt).

/// The environment variable that sets the size, in bytes of code and data before optimisation,
/// from which a group of shared code gets a chunk of its own.
const CHUNK_THRESHOLD_VARIABLE: &str = "DX_WASM_SPLIT_CHUNK_THRESHOLD";

/// The environment variable that sets the most shared chunks, the common chunk included.
const CHUNK_LIMIT_VARIABLE: &str = "DX_WASM_SPLIT_CHUNK_LIMIT";

/// The start of the line a build with bundle splitting prints about its shared chunks.
const SHARED_CHUNKS_LINE: &str = "Bundle splitting:";

/// The splitter's options from the environment (CodeBrix change).
fn split_chunk_options() -> Result<wasm_split_cli::SplitOptions> {
    split_chunk_options_from(
        std::env::var(CHUNK_THRESHOLD_VARIABLE),
        std::env::var(CHUNK_LIMIT_VARIABLE),
    )
}

/// The splitter's options from the two variables' values (CodeBrix change): unset or empty, the
/// splitter's defaults (one chunk for all shared code; the default limit).
fn split_chunk_options_from(
    threshold: std::result::Result<String, std::env::VarError>,
    limit: std::result::Result<String, std::env::VarError>,
) -> Result<wasm_split_cli::SplitOptions> {
    let defaults = wasm_split_cli::SplitOptions::default();
    Ok(wasm_split_cli::SplitOptions {
        chunk_threshold: positive_whole_number(
            CHUNK_THRESHOLD_VARIABLE,
            "the size in bytes from which shared code gets a chunk of its own; unset: one chunk for all shared code",
            threshold,
        )?,
        chunk_limit: positive_whole_number(
            CHUNK_LIMIT_VARIABLE,
            "the most shared chunks, the common one included; unset: the splitter's default",
            limit,
        )?
        .unwrap_or(defaults.chunk_limit),
    })
}

/// The value of an environment variable that must be a positive whole number, or None when it
/// is unset or empty (CodeBrix change). Anything else is an error that names the variable, says
/// what it is for and stops the build before anything is built.
fn positive_whole_number(
    variable: &str,
    meaning: &str,
    value: std::result::Result<String, std::env::VarError>,
) -> Result<Option<usize>> {
    let refuse = |shown: String| {
        anyhow::anyhow!(
            "Bundle splitting cannot start: the environment variable {variable} ({meaning}) must be a positive whole number - digits only, at least 1 - or unset; it is {shown}. Nothing was built."
        )
    };
    let value = match value {
        Ok(value) => value,
        Err(std::env::VarError::NotPresent) => return Ok(None),
        Err(std::env::VarError::NotUnicode(value)) => return Err(refuse(format!("{value:?}"))),
    };
    let trimmed = value.trim();
    if trimmed.is_empty() {
        return Ok(None);
    }
    match trimmed.parse::<usize>() {
        Ok(number) if number >= 1 && trimmed.bytes().all(|b| b.is_ascii_digit()) => Ok(Some(number)),
        _ => Err(refuse(format!("{value:?}"))),
    }
}

/// The line that says how the shared code of a split build was grouped (CodeBrix change).
fn describe_shared_chunks(chunks: usize, options: &wasm_split_cli::SplitOptions) -> String {
    let plural = if chunks == 1 { "" } else { "s" };
    match options.chunk_threshold {
        Some(threshold) => format!(
            "{SHARED_CHUNKS_LINE} {chunks} shared chunk{plural} (threshold {threshold} bytes, limit {limit})",
            limit = options.chunk_limit
        ),
        None => format!(
            "{SHARED_CHUNKS_LINE} {chunks} shared chunk{plural} (no threshold: one chunk for all shared code)"
        ),
    }
}

#[cfg(test)]
mod split_chunk_option_tests {
    use super::{describe_shared_chunks, split_chunk_options_from};
    use std::env::VarError;

    fn set(value: &str) -> Result<String, VarError> {
        Ok(value.to_string())
    }

    #[test]
    fn unset_or_empty_gives_the_splitters_defaults() {
        let defaults = wasm_split_cli::SplitOptions::default();
        let unset = split_chunk_options_from(Err(VarError::NotPresent), Err(VarError::NotPresent)).unwrap();
        assert_eq!(unset, defaults);
        let empty = split_chunk_options_from(set(""), set("  ")).unwrap();
        assert_eq!(empty, defaults);
    }

    #[test]
    fn positive_whole_numbers_are_taken() {
        let options = split_chunk_options_from(set("16384"), set(" 3 ")).unwrap();
        assert_eq!(options.chunk_threshold, Some(16384));
        assert_eq!(options.chunk_limit, 3);
        let limit_only = split_chunk_options_from(Err(VarError::NotPresent), set("1")).unwrap();
        assert_eq!(limit_only.chunk_threshold, None);
        assert_eq!(limit_only.chunk_limit, 1);
    }

    #[test]
    fn anything_else_in_the_threshold_is_refused_with_a_message_of_its_own() {
        for bad in ["abc", "0", "-5", "+5", "1.5", "16 KiB", "99999999999999999999999"] {
            let message = split_chunk_options_from(set(bad), Err(VarError::NotPresent)).unwrap_err().to_string();
            assert!(message.starts_with("Bundle splitting cannot start: the environment variable DX_WASM_SPLIT_CHUNK_THRESHOLD"), "{message}");
            assert!(message.contains(&format!("{bad:?}")), "{message}");
            assert!(message.ends_with("Nothing was built."), "{message}");
        }
    }

    #[test]
    fn anything_else_in_the_limit_is_refused_too() {
        for bad in ["abc", "0", "-1", "two"] {
            let message = split_chunk_options_from(set("16384"), set(bad)).unwrap_err().to_string();
            assert!(message.starts_with("Bundle splitting cannot start: the environment variable DX_WASM_SPLIT_CHUNK_LIMIT"), "{message}");
            assert!(message.contains(&format!("{bad:?}")), "{message}");
        }
    }

    #[test]
    fn the_line_says_how_many_chunks_and_the_settings() {
        let options = split_chunk_options_from(set("16384"), set("16")).unwrap();
        assert_eq!(describe_shared_chunks(3, &options), "Bundle splitting: 3 shared chunks (threshold 16384 bytes, limit 16)");
        let none = split_chunk_options_from(Err(VarError::NotPresent), Err(VarError::NotPresent)).unwrap();
        assert_eq!(describe_shared_chunks(1, &none), "Bundle splitting: 1 shared chunk (no threshold: one chunk for all shared code)");
    }
}

// CodeBrix change: the file names of the split modules (see the call in `bundle_web` and
// tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt).

/// The piece name dioxus's `#[component(lazy)]` gives every lazy component
/// (`lazy_loader!(extern "lazy" fn Lazy<Component>(..))`).
const LAZY_COMPONENT_PIECE: &str = "lazy";

/// The name part of each split module's file, `module_<n>_<name part>.wasm`, in the order of
/// `modules` (CodeBrix change). See [`split_module_file_stem`].
fn split_module_file_stems(modules: &[wasm_split_cli::SplitModule]) -> Result<Vec<String>> {
    let mut uses: std::collections::BTreeMap<&str, usize> = std::collections::BTreeMap::new();
    for module in modules {
        *uses.entry(module.module_name.as_str()).or_default() += 1;
    }
    modules
        .iter()
        .map(|module| {
            let function = module
                .component_name
                .as_deref()
                .context("generated bindgen module has no name?")?;
            Ok(split_module_file_stem(
                &module.module_name,
                function,
                uses[module.module_name.as_str()] > 1,
            ))
        })
        .collect()
}

/// The name part of a split module's file (CodeBrix change). `piece` is the name the split
/// point gives its piece - the "NAME" of `lazy_loader!(extern "NAME" fn ...)`, the module of
/// `#[wasm_split(NAME)]`, "lazy" for every `#[component(lazy)]`, and what the router's macro
/// generates for a route - and `function` the function it calls:
///   - a ROUTE (the router names the piece `module<Route><hash>` and the function
///     `route<Route><hash>`, the same 32 hexadecimal digits after the same name): the function's
///     name, `route<Route><hash>`;
///   - a LAZY COMPONENT (`#[component(lazy)] fn Insights` gives the piece "lazy" and the
///     function `LazyInsights`), and any other piece named "lazy": `lazy_<function>`, always, so
///     that adding a second lazy component renames no file;
///   - any other piece name that several split points share: `<piece>_<function>`;
///   - otherwise the piece name; an empty one gives the function's name.
/// Both names are ASCII letters, digits and `_` (the macros make `#[no_mangle]` items of them),
/// so the result is a safe file name; the `module_<n>_` before it keeps every file apart.
fn split_module_file_stem(piece: &str, function: &str, shared: bool) -> String {
    if let Some(route) = piece.strip_prefix("module") {
        let hash = route.len().checked_sub(32).map(|at| &route[at..]);
        if route.len() > 32
            && function.strip_prefix("route") == Some(route)
            && hash.is_some_and(|hash| hash.bytes().all(|b| matches!(b, b'0'..=b'9' | b'a'..=b'f')))
        {
            return function.to_string();
        }
    }

    if piece.is_empty() {
        function.to_string()
    } else if shared || piece == LAZY_COMPONENT_PIECE {
        format!("{piece}_{function}")
    } else {
        piece.to_string()
    }
}

#[cfg(test)]
mod split_module_file_name_tests {
    use super::split_module_file_stem;

    #[test]
    fn a_named_piece_keeps_its_name() {
        assert_eq!(split_module_file_stem("details", "order_details_panel", false), "details");
        assert_eq!(split_module_file_stem("forecast", "run_forecast", false), "forecast");
    }

    #[test]
    fn a_lazy_component_always_carries_its_function_name() {
        assert_eq!(split_module_file_stem("lazy", "LazyInsights", false), "lazy_LazyInsights");
        assert_eq!(split_module_file_stem("lazy", "LazyInsights", true), "lazy_LazyInsights");
    }

    #[test]
    fn a_shared_piece_name_gets_the_function_name() {
        assert_eq!(split_module_file_stem("reports", "monthly", true), "reports_monthly");
    }

    #[test]
    fn a_route_keeps_the_route_name() {
        let hash = "d2348507f81099b5d7c92db749db3f60";
        assert_eq!(
            split_module_file_stem(&format!("moduleOrders{hash}"), &format!("routeOrders{hash}"), false),
            format!("routeOrders{hash}")
        );
    }

    #[test]
    fn only_the_routers_pair_counts_as_a_route() {
        assert_eq!(split_module_file_stem("moduleOrders", "routeOrders", false), "moduleOrders");
        let hash = "d2348507f81099b5d7c92db749db3f60";
        assert_eq!(
            split_module_file_stem(&format!("moduleOrders{hash}"), "panel", false),
            format!("moduleOrders{hash}")
        );
    }

    #[test]
    fn an_empty_piece_name_gives_the_function_name() {
        assert_eq!(split_module_file_stem("", "panel", false), "panel");
    }
}
