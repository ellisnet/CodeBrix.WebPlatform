//! The code split off BY HAND, beside the per-route pieces the router makes. Each of the three
//! lower-level ways Dioxus 0.7 offers is used once, in a module of its own:
//!
//! - `order_details`: `wasm_split::lazy_loader!` and a `LazyLoader` static, for a panel loaded
//!   on demand (`.load().await`, then `.call(..)`);
//! - `forecast`: `#[wasm_split(forecast)]` on an async function whose code is its own piece,
//!   downloaded the first time the function is called;
//! - `insights`: `#[component(lazy)]`, a component whose body is its own piece and which
//!   suspends until that piece has arrived.
//!
//! In a build without bundle splitting these are ordinary code (the loaders are already loaded).

pub mod forecast;
pub mod insights;
pub mod order_details;
