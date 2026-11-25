//! Talking to the backend's REST API: [`client`] holds the transport rules (headers, session
//! refresh and retry, error parsing) and [`error`] the error every call returns.

pub mod client;
pub mod error;

pub use client::{Auth, ClientConfig, Method, RefreshOutcome, REQUEST_MARKER_HEADER};
pub use error::ApiError;

/// What every generated API function returns.
pub type ApiResult<T> = Result<T, ApiError>;
