//! The shared frontend code of the CodeBrix.WebPlatform web application platform.
//!
//! Every platform frontend compiles this crate in. The Rust client code that is generated from an
//! application's backend calls into [`api`] (one function per REST endpoint) and [`signalr`] (one
//! type per SignalR hub); an application's own pages use [`dates`], [`query`] and [`browser`]
//! directly, and [`signalr`] for anything the generated hub types do not offer.
//!
//! An application sets the crate up once, on its first render, with ONE call: the generated
//! `configure_platform` (in the client code generated from its backend), which fills in what the
//! backend declares - its request marker and where an expired session is renewed - and calls
//! [`configure`] with a [`PlatformConfig`]. That sets up the REST client and the SignalR client
//! together. Code the crate runs on its own tasks (hub handlers, the session refresh) runs inside
//! the application's Dioxus runtime; the application need do nothing for it.
//!
//! [`browser`] holds what every platform page needs from the browser and cannot get from Dioxus
//! under the platform's Content-Security-Policy, the page title above all ([`browser::set_title`]).

pub mod api;
mod app_runtime;
pub mod browser;
pub mod dates;
pub mod query;
pub mod setup;
pub mod signalr;

pub use setup::{configure, PlatformConfig};
