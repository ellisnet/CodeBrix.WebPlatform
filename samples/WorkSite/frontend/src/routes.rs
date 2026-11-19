//! Every page of the site and the layout it sits in.

use dioxus::prelude::*;
use dioxus::router::routable::FromRouteSegments;

use crate::layouts::app_shell::AppShell;
use crate::layouts::public::PublicLayout;
use crate::pages::app::account::Account;
use crate::pages::app::clients::Clients;
use crate::pages::app::dashboard::Dashboard;
use crate::pages::app::projects::Projects;
use crate::pages::app::reports::Reports;
use crate::pages::app::time::TimeLog;
use crate::pages::public::about::About;
use crate::pages::public::features::Features;
use crate::pages::public::home::Home;
use crate::pages::public::not_found::NotFound;
use crate::pages::public::register::Register;
use crate::pages::public::sign_in::SignIn;
use crate::pages::public::tour::Tour;

/// `SignIn::next` is where to go after signing in (only `/app...` paths are honoured).
#[derive(Clone, Debug, PartialEq, Routable)]
#[rustfmt::skip]
pub enum Route {
    #[layout(PublicLayout)]
        #[route("/")]
        Home {},
        #[route("/features")]
        Features {},
        #[route("/tour")]
        Tour {},
        #[route("/about")]
        About {},
        #[route("/sign-in?:next")]
        SignIn { next: String },
        #[route("/register")]
        Register {},
        #[route("/:..segments")]
        NotFound { segments: UnknownPath },
    #[end_layout]
    #[layout(AppShell)]
        #[route("/app")]
        Dashboard {},
        #[route("/app/time")]
        TimeLog {},
        #[route("/app/clients")]
        Clients {},
        #[route("/app/projects")]
        Projects {},
        #[route("/app/reports")]
        Reports {},
        #[route("/app/account")]
        Account {},
}

/// The segments of a path no other route matches. A type of its own (rather than
/// `Vec<String>`) so that parsing can fail, which keeps the router's generated code free of
/// unreachable branches.
#[derive(Clone, Debug, Default, PartialEq)]
pub struct UnknownPath(pub Vec<String>);

impl UnknownPath {
    /// The path as typed, such as `/no/such/page`.
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
