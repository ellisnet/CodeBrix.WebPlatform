//! Every page of the admin application, all inside the one layout (`AdminShell`). The
//! application is built without bundle splitting, so all of them are in its one WebAssembly file.

use dioxus::prelude::*;

use crate::layout::AdminShell;
use crate::pages::audit::Audit;
use crate::pages::not_found::{NotFound, UnknownPath};
use crate::pages::settings::Settings;
use crate::pages::system::System;
use crate::pages::users::Users;

/// The pages, in the order of the navigation.
#[derive(Clone, Debug, PartialEq, Routable)]
#[rustfmt::skip]
pub enum Route {
    #[layout(AdminShell)]
        #[route("/")]
        Users {},
        #[route("/audit")]
        Audit {},
        #[route("/system")]
        System {},
        #[route("/settings")]
        Settings {},
        #[route("/:..segments")]
        NotFound { segments: UnknownPath },
}
