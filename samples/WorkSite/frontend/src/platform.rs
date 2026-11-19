//! The browser facilities only this application needs, reached through the `web-sys` Rust
//! bindings: WorkSite's own classes on the document, and the scroll lock behind its dialogs. The
//! general ones (origin, page title, local date and clock, navigation, sleeping) are in the
//! platform's shared crate, `codebrix_webplatform::browser`. No JavaScript is written by this app,
//! and none is generated from text at run time.

fn document() -> Option<web_sys::Document> {
    web_sys::window()?.document()
}

/// Puts the page language on `<html>` and the base colours on `<body>` (the generated
/// `index.html` has neither).
pub fn prepare_document() {
    if let Some(document) = document() {
        if let Some(html) = document.document_element() {
            let _ = html.set_attribute("lang", "en");
            let _ = html.set_attribute("class", "h-full scroll-smooth bg-slate-50");
        }
        if let Some(body) = document.body() {
            body.set_class_name("h-full bg-slate-50 text-slate-900 antialiased");
        }
    }
}

/// Stops the page behind a dialog from scrolling while the dialog is open.
pub fn set_scroll_lock(locked: bool) {
    if let Some(body) = document().and_then(|d| d.body()) {
        let _ = body.class_list().toggle_with_force("overflow-hidden", locked);
    }
}
