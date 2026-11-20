//! The page for an address below /admin that no other page has, and the type that holds such an
//! address.

use dioxus::prelude::*;
use dioxus::router::routable::FromRouteSegments;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{PageTitle, BTN_PRIMARY};
use crate::routes::Route;

#[component]
pub fn NotFound(segments: UnknownPath) -> Element {
    rsx! {
        PageTitle { title: "Page not found" }
        div { class: "mx-auto max-w-lg py-16 text-center",
            p { class: "font-mono text-sm text-cyan-400", "404" }
            h1 { id: "page-title", class: "mt-2 text-3xl font-semibold tracking-tight text-white", "Page not found" }
            p { class: "mt-3 text-zinc-400",
                "The admin console has no page at "
                code { class: "rounded bg-white/10 px-1.5 py-0.5 text-sm text-zinc-200", "/admin{segments.display()}" }
                "."
            }
            Link { to: Route::Users {}, class: "{BTN_PRIMARY} mt-8",
                Icon { name: IconName::ArrowLeft, class: "size-4" }
                "Back to users"
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
