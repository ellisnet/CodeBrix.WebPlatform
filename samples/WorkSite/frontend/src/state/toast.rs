//! Short notices shown in the corner of the screen after an action succeeds or fails.

use std::cell::Cell;

use dioxus::prelude::*;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ToastKind {
    Success,
    Error,
    Info,
    /// Something to look at, though nothing failed (a notification from the server).
    Warning,
}

#[derive(Clone, Debug, PartialEq)]
pub struct Toast {
    pub id: u64,
    pub kind: ToastKind,
    pub message: String,
}

pub static TOASTS: GlobalSignal<Vec<Toast>> = Signal::global(Vec::new);

thread_local! {
    static NEXT_ID: Cell<u64> = const { Cell::new(1) };
}

/// The most notices on screen at once; older ones give way.
const MAX_VISIBLE: usize = 4;

/// Shows a notice of any kind.
pub fn show(kind: ToastKind, message: impl Into<String>) {
    let id = NEXT_ID.with(|next| {
        let id = next.get();
        next.set(id + 1);
        id
    });
    let mut toasts = TOASTS.write();
    toasts.push(Toast {
        id,
        kind,
        message: message.into(),
    });
    let excess = toasts.len().saturating_sub(MAX_VISIBLE);
    toasts.drain(..excess);
}

pub fn success(message: impl Into<String>) {
    show(ToastKind::Success, message);
}

pub fn error(message: impl Into<String>) {
    show(ToastKind::Error, message);
}

pub fn info(message: impl Into<String>) {
    show(ToastKind::Info, message);
}

pub fn dismiss(id: u64) {
    TOASTS.write().retain(|t| t.id != id);
}
