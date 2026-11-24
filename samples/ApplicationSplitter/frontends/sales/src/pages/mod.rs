//! The pages of the sales application, one module each. Every page is a route, and so, in the
//! split build, a piece of WebAssembly of its own.

pub mod customers;
pub mod diagnostics;
pub mod live;
pub mod new_quote;
pub mod not_found;
pub mod orders;
pub mod overview;
pub mod reports;
