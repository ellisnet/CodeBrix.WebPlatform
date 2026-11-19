//! The live features: what the application does with the backend's SignalR hubs (the hub types
//! are generated in `crate::generated::hubs`; the client underneath is the platform's
//! `codebrix_webplatform::signalr`).
//!
//! Public pages: world clocks (`clocks`), the activity ticker (`activity`) and the "try it"
//! estimator (`estimator`). The signed-in app: the running timer (`timer`), reloading data that
//! changed elsewhere (`changes`), notifications (`notifications`), report progress
//! (`report_progress`), the project activity feed (`project_feed`), signing out when the session
//! ends elsewhere and the connection indicator (`app_hubs`).
//!
//! Each module keeps its decisions in plain functions and types that native `cargo test` drives
//! against the platform's `signalr::testing::FakeHub`; the components around them are thin.
//! Handlers may touch any signal: the client runs them inside the Dioxus runtime itself.

pub mod activity;
pub mod app_hubs;
pub mod changes;
pub mod clocks;
pub mod estimator;
pub mod notifications;
pub mod project_feed;
pub mod report_progress;
pub mod timer;
