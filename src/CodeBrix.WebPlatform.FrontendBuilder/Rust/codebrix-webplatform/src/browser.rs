//! The browser facilities every platform application needs, reached through the `web-sys` /
//! `js-sys` Rust bindings: the page's origin and title, the local date and clock, navigation and
//! sleeping. No JavaScript is written here, and none is generated from text at run time.

use crate::dates::Date;

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
    fn the_clock_text_has_two_digits_for_hours_and_minutes() {
        assert_eq!(clock_text(9, 5), "09:05");
        assert_eq!(clock_text(23, 59), "23:59");
        assert_eq!(clock_text(0, 0), "00:00");
    }
}
