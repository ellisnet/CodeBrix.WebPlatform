//! The browser facilities every platform application needs, reached through the `web-sys` /
//! `js-sys` Rust bindings: the page's origin and title, the query the page was opened with, the
//! local date and clock, navigation, sleeping and whether WebGPU has an adapter. No JavaScript is
//! written here, and none is generated from text at run time.

use std::cell::OnceCell;

use wasm_bindgen::{JsCast, JsValue};

use crate::dates::Date;

thread_local! {
    /// The query string of the address the page was opened with, once it has been read.
    static OPENED_QUERY: OnceCell<String> = const { OnceCell::new() };
}

fn window() -> Option<web_sys::Window> {
    web_sys::window()
}

/// `window.location.origin`, e.g. `https://app.example`. Empty outside a browser.
pub fn origin() -> String {
    window()
        .and_then(|w| w.location().origin().ok())
        .unwrap_or_default()
}

/// Sets the page title (`document.title`) through the DOM binding, and only when it changes.
///
/// Use this instead of Dioxus's `document::Title` component: that one sets the title by running
/// script text, which the Content-Security-Policy a platform site is served with forbids (the
/// browser refuses it and logs a violation). Call it from a page's component, for example in
/// `use_effect` or straight from the component body.
pub fn set_title(title: &str) {
    if let Some(document) = window().and_then(|w| w.document()) {
        if document.title() != title {
            document.set_title(title);
        }
    }
}

/// The browser's local hour, 0-23.
pub fn local_hour() -> u32 {
    js_sys::Date::new_0().get_hours()
}

/// The browser's local time of day (`14:05`) of an ISO-8601 instant such as
/// `2026-11-18T13:05:00Z`, or empty when the text is not one.
pub fn local_time(instant: &str) -> String {
    let date = js_sys::Date::new(&js_sys::JsString::from(instant));
    if date.get_time().is_nan() {
        return String::new();
    }
    clock_text(date.get_hours(), date.get_minutes())
}

/// `hh:mm`, both with two digits.
fn clock_text(hours: u32, minutes: u32) -> String {
    format!("{hours:02}:{minutes:02}")
}

/// The browser's local calendar date.
pub fn today() -> Date {
    let now = js_sys::Date::new_0();
    Date::new(now.get_full_year() as i32, now.get_month() + 1, now.get_date())
        .unwrap_or(Date { year: 1970, month: 1, day: 1 })
}

/// Sends the browser to a same-origin address (`location.assign`). For a download the server
/// answers with `Content-Disposition: attachment`, so the page stays where it is.
pub fn navigate(path: &str) {
    if let Some(window) = window() {
        let _ = window.location().assign(&format!("{}{}", origin(), path));
    }
}

/// Keeps the query string of the address the page was opened with (`window.location.search`,
/// such as `?view=north&stats`), so that [`opened_query`] and [`opened_query_value`] give it later.
///
/// Call it FIRST in `main`, before `dioxus::launch`: when the router starts, it replaces the
/// browser's address with the route's own form whenever the two differ, and the query of a route
/// that declares none is lost then. Calling it again changes nothing; the first query is kept.
pub fn capture_opened_query() {
    remember_opened_query(current_query);
}

/// The query string the page was opened with, `?` included (`?view=north&stats`), or empty for
/// none: what [`capture_opened_query`] kept. Before any capture it captures the current one, which
/// is the opened one only while the router has not started.
pub fn opened_query() -> String {
    remember_opened_query(current_query)
}

/// One value of the query string the page was opened with ([`opened_query`]), decoded:
/// `Some("north")` for `?view=north`, `Some("")` for a name given without a value (`?stats`),
/// `None` when the name is not there.
pub fn opened_query_value(name: &str) -> Option<String> {
    crate::query::value(&opened_query(), name)
}

/// The kept query, reading it with `read` when nothing is kept yet.
fn remember_opened_query(read: impl FnOnce() -> String) -> String {
    OPENED_QUERY.with(|kept| kept.get_or_init(read).clone())
}

/// `window.location.search`. Empty outside a browser.
fn current_query() -> String {
    window()
        .and_then(|w| w.location().search().ok())
        .unwrap_or_default()
}

/// Whether the browser gives this page a WebGPU adapter: `navigator.gpu` exists AND
/// `navigator.gpu.requestAdapter()` resolves to an adapter. `navigator.gpu` alone does not tell:
/// a browser can have it and still give no adapter (Chrome on Linux without its WebGPU flags).
///
/// It asks through `js-sys` reflection, so a page that only wants to know (to show a notice
/// instead of a WebGPU canvas, or to choose which piece to load) pulls in no WebGPU bindings.
/// False outside a browser and when the request fails.
pub async fn has_webgpu_adapter() -> bool {
    let Some(window) = window() else {
        return false;
    };
    let Some(gpu) = property(window.as_ref(), "navigator").and_then(|n| property(&n, "gpu")) else {
        return false;
    };
    let Some(request) = property(&gpu, "requestAdapter").and_then(|f| f.dyn_into::<js_sys::Function>().ok()) else {
        return false;
    };
    let Some(promise) = request
        .call0(&gpu)
        .ok()
        .and_then(|p| p.dyn_into::<js_sys::Promise>().ok())
    else {
        return false;
    };
    match wasm_bindgen_futures::JsFuture::from(promise).await {
        Ok(adapter) => is_present(&adapter),
        Err(_) => false,
    }
}

/// `target[name]`, when it is neither `undefined` nor `null`.
fn property(target: &JsValue, name: &str) -> Option<JsValue> {
    js_sys::Reflect::get(target, &JsValue::from_str(name))
        .ok()
        .filter(is_present)
}

fn is_present(value: &JsValue) -> bool {
    !value.is_undefined() && !value.is_null()
}

/// Resolves after `ms` milliseconds (a `setTimeout` wrapped in a promise).
pub async fn sleep_ms(ms: i32) {
    let Some(window) = window() else {
        return;
    };
    let promise = js_sys::Promise::new(&mut |resolve, _reject| {
        let _ = window.set_timeout_with_callback_and_timeout_and_arguments_0(&resolve, ms);
    });
    let _ = wasm_bindgen_futures::JsFuture::from(promise).await;
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn the_first_query_kept_is_the_opened_one() {
        assert_eq!(remember_opened_query(|| "?view=north&stats".to_string()), "?view=north&stats");
        assert_eq!(remember_opened_query(|| "".to_string()), "?view=north&stats");
        assert_eq!(remember_opened_query(|| panic!("read again")), "?view=north&stats");
    }

    #[test]
    fn the_clock_text_has_two_digits_for_hours_and_minutes() {
        assert_eq!(clock_text(9, 5), "09:05");
        assert_eq!(clock_text(23, 59), "23:59");
        assert_eq!(clock_text(0, 0), "00:00");
    }
}
