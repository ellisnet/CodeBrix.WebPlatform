//! The page for an address that none of the three applications has, and the type that holds
//! such an address.

use dioxus::prelude::*;
use dioxus::router::routable::FromRouteSegments;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{PageTitle, BTN_PRIMARY};
use crate::routes::Route;

#[component]
pub fn NotFound(segments: UnknownPath) -> Element {
    rsx! {
        PageTitle { title: "Page not found \u{00b7} ApplicationSplitter" }
        div { class: "mx-auto max-w-lg px-4 py-24 text-center",
            p { class: "text-sm font-semibold text-indigo-700", "404" }
            h1 { id: "page-title", class: "mt-2 text-3xl font-bold tracking-tight text-slate-900", "Page not found" }
            p { class: "mt-3 text-slate-700",
                "There is no page at "
                code { class: "rounded bg-slate-100 px-1.5 py-0.5 text-sm text-slate-800", "{segments.display()}" }
                "."
            }
            Link { to: Route::Home {}, class: "{BTN_PRIMARY} mt-8",
                Icon { name: IconName::Home, class: "size-4" }
                "Back to the overview"
            }
        }
    }
}

/// The segments of a path that no other route matches. A type of its own (rather than
/// `Vec<String>`) so that reading it can fail, which keeps the router's generated code free of
/// unreachable branches.
#[derive(Clone, Debug, Default, PartialEq)]
pub struct UnknownPath(pub Vec<String>);

impl UnknownPath {
    /// The path as it was typed, such as `/no/such/page`.
    pub fn display(&self) -> String {
        format!("/{}", self.0.join("/"))
    }
}

impl<'a> IntoIterator for &'a UnknownPath {
    type Item = &'a String;
    type IntoIter = std::slice::Iter<'a, String>;

    fn into_iter(self) -> Self::IntoIter {
        self.0.iter()
    }
}

impl FromRouteSegments for UnknownPath {
    type Err = String;

    fn from_route_segments(segments: &[&str]) -> Result<Self, Self::Err> {
        if segments.iter().any(|s| s.chars().any(char::is_control)) {
            return Err("The path contains control characters.".to_string());
        }
        Ok(UnknownPath(segments.iter().map(|s| s.to_string()).collect()))
    }
}
