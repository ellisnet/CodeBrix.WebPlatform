// The build script of the CodeBrix libgit2-sys stand-in. It compiles NOTHING: it tells cargo
// where the prebuilt libgit2 is and what it is called, so the program that uses git2 links it
// as a shared library. (Upstream's build.rs compiles libgit2's C source or probes for a system
// libgit2; neither is done here.)
//
//   CODEBRIX_LIBGIT2_LIB_DIR    REQUIRED. The folder that holds the library to link against,
//                               taken from the CodeBrix.LibGit2.MitLicenseForever package:
//                                 Linux    lib<name>.so      (runtimes/<rid>/native)
//                                 macOS    lib<name>.dylib   (runtimes/<rid>/native)
//                                 Windows  <name>.lib        an import library made from
//                                                            <name>.dll (the package has no .lib)
//   CODEBRIX_LIBGIT2_LINK_NAME  the library's link name; default git2-49e408b (libgit2 1.9.7,
//                               commit 49e408b, as the package names it)
//
// The build scripts in tools/dx-native-tools (linux/, windows/, macos/) set both from pins.env.

use std::env;
use std::path::PathBuf;

const DEFAULT_LINK_NAME: &str = "git2-49e408b";

fn main() {
    println!("cargo:rerun-if-changed=build.rs");
    println!("cargo:rerun-if-env-changed=CODEBRIX_LIBGIT2_LIB_DIR");
    println!("cargo:rerun-if-env-changed=CODEBRIX_LIBGIT2_LINK_NAME");
    // lib.rs keeps upstream's `cfg!(libgit2_vendored)`; this build never sets it (nothing is
    // vendored), so `git2::Version::vendored()` answers false.
    println!("cargo:rustc-check-cfg=cfg(libgit2_vendored)");

    let link_name = env::var("CODEBRIX_LIBGIT2_LINK_NAME")
        .ok()
        .filter(|name| !name.is_empty())
        .unwrap_or_else(|| DEFAULT_LINK_NAME.to_string());
    let lib_dir = match env::var_os("CODEBRIX_LIBGIT2_LIB_DIR") {
        Some(dir) if !dir.is_empty() => PathBuf::from(dir),
        _ => fail(
            "CODEBRIX_LIBGIT2_LIB_DIR is not set. It must name the folder that holds the prebuilt \
             libgit2 from the CodeBrix.LibGit2.MitLicenseForever package (see \
             tools/dx-native-tools/sources/codebrix-libgit2-sys/README.txt). This crate never \
             compiles libgit2.",
        ),
    };

    let target_os = env::var("CARGO_CFG_TARGET_OS").unwrap_or_default();
    let target_env = env::var("CARGO_CFG_TARGET_ENV").unwrap_or_default();
    let file_name = match (target_os.as_str(), target_env.as_str()) {
        ("windows", "msvc") => format!("{link_name}.lib"),
        ("macos", _) => format!("lib{link_name}.dylib"),
        ("windows", _) => fail("only the MSVC Windows target is supported (an import library made from the package's DLL)"),
        _ => format!("lib{link_name}.so"),
    };
    let library = lib_dir.join(&file_name);
    if !library.is_file() {
        fail(&format!(
            "{} does not exist. CODEBRIX_LIBGIT2_LIB_DIR must hold {file_name} for this target.",
            library.display()
        ));
    }

    println!("cargo:rustc-link-search=native={}", lib_dir.display());
    println!("cargo:rustc-link-lib=dylib={link_name}");
}

fn fail(message: &str) -> ! {
    eprintln!("error: libgit2-sys (CodeBrix stand-in): {message}");
    std::process::exit(1);
}
