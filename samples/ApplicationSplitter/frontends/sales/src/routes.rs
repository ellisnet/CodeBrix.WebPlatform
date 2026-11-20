//! Every page of the sales application, all inside the one layout (`SalesShell`).
//!
//! THE BUNDLE SPLITTING. dioxus's `wasm-split` feature is on (Cargo.toml) and the build runs
//! `dx build --wasm-split`, so the router macro wraps EVERY route below in a lazy loader: the
//! page's component, and everything only it uses, becomes a piece of WebAssembly of its own
//! (`module_*.wasm`) that the browser downloads when the route is first shown. The layout is not
//! split: it stays on screen, and its `SuspenseBoundary` shows a loading panel while a piece
//! arrives. A second visit to a page needs no download.
//!
//! For each split route the router macro generates a function and a loader static named after
//! the route's variant in camel case (for example `routeOverview...`), which the compiler
//! reports as `non_snake_case`, once per route. The names are the macro's, not ours, and the
//! derive's output is a separate item that an `allow` on the enum does not reach, so the lint is
//! allowed for this module, which holds the enum and nothing else.
#![allow(non_snake_case)]

use dioxus::prelude::*;

use crate::layout::SalesShell;
use crate::pages::customers::Customers;
use crate::pages::live::Live;
use crate::pages::new_quote::NewQuote;
use crate::pages::not_found::{NotFound, UnknownPath};
use crate::pages::orders::Orders;
use crate::pages::overview::Overview;
use crate::pages::reports::Reports;

/// The pages, in the order of the navigation.
#[derive(Clone, Debug, PartialEq, Routable)]
#[rustfmt::skip]
pub enum Route {
    #[layout(SalesShell)]
        #[route("/")]
        Overview {},
        #[route("/orders?:query")]
        Orders { query: String },
        #[route("/customers")]
        Customers {},
        #[route("/quotes/new")]
        NewQuote {},
        #[route("/reports")]
        Reports {},
        #[route("/live")]
        Live {},
        #[route("/:..segments")]
        NotFound { segments: UnknownPath },
}
