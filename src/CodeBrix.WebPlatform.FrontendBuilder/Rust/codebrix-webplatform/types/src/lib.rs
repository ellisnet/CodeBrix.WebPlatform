//! The browser-free types of the CodeBrix.WebPlatform web application platform: what the Rust
//! client code generated from an application's backend names besides serde, the calendar date
//! above all ([`dates::Date`], the Rust type of a .NET `DateOnly`).
//!
//! The shared crate `codebrix-webplatform` re-exports this crate, so a frontend writes
//! `codebrix_webplatform::dates::Date` and never names this one. Code that uses the contract
//! types WITHOUT the browser-bound shared crate - a native program or test that reads the
//! generated `models.rs` - names this crate under the shared crate's name in its `Cargo.toml`,
//! so that the generated `use codebrix_webplatform::dates::Date;` finds it:
//!
//! ```toml
//! codebrix-webplatform = { package = "codebrix-webplatform-types", path = "../frontend/platform/codebrix-webplatform/types" }
//! ```
//!
//! Pure logic on std and serde only: compiles and is tested on the host.

pub mod dates;
