//! The doorway to the backend's REST contract. The endpoint functions and the contract types are
//! generated from the backend (`crate::generated`); the transport and the error type come from
//! the platform's shared crate. This module only gathers them under one name, and `ext` adds the
//! few helpers this application wants on top.

pub mod ext;

pub use crate::generated::api::{auth, clients, dashboard, meta, projects, reports, time_entries};
pub use crate::generated::models;
// (`ApiResult<T>` is not re-exported: no hand-written code names it, and an unused re-export
// warns. The generated functions return it; reach it as `codebrix_webplatform::api::ApiResult`.)
pub use codebrix_webplatform::api::ApiError;
