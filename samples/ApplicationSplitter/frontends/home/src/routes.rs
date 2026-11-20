//! The pages of the landing application, inside the one layout (`SiteLayout`).

use dioxus::prelude::*;

use crate::layout::SiteLayout;
use crate::pages::home::Home;
use crate::pages::how_it_works::HowItWorks;
use crate::pages::not_found::{NotFound, UnknownPath};

/// The pages, in the order of the navigation.
#[derive(Clone, Debug, PartialEq, Routable)]
#[rustfmt::skip]
pub enum Route {
    #[layout(SiteLayout)]
        #[route("/")]
        Home {},
        #[route("/how-it-works")]
        HowItWorks {},
        #[route("/:..segments")]
        NotFound { segments: UnknownPath },
}
