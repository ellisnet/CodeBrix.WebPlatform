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
pub mod query;
pub mod setup;
pub mod signalr;

pub use codebrix_webplatform_types::dates;
pub use setup::{configure, PlatformConfig};

#[cfg(test)]
mod tests {
    use crate::dates::Date;

    /// The date of the browser-free types crate is the one the shared crate offers as `dates`,
    /// and its JSON is the text `yyyy-MM-dd` (a .NET `DateOnly`).
    #[test]
    fn dates_is_the_types_crate_and_its_json_is_the_iso_text() {
        let date = Date::new(2026, 11, 17).unwrap();
        let same: codebrix_webplatform_types::dates::Date = date;
        assert_eq!(same, date);
        assert_eq!(serde_json::to_string(&date).unwrap(), "\"2026-11-17\"");
        assert_eq!(serde_json::from_str::<Date>("\"2026-11-17\"").unwrap(), date);
        assert_eq!(serde_json::from_str::<Date>("\"2026-11-17T08:00:00Z\"").unwrap(), date);
        assert!(serde_json::from_str::<Date>("\"17/11/2026\"").is_err());
        assert!(serde_json::from_str::<Date>("20261117").is_err());
    }
}
