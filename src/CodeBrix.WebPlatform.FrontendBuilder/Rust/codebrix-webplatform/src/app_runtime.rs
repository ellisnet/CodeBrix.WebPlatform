//! Running application code inside the application's Dioxus runtime from places Dioxus does not
//! poll: the SignalR client's own tasks (where hub messages, state changes and reconnects are
//! handled, and where an expired session is refreshed), the browser's WebSocket and timer
//! callbacks, and the REST client's shared session refresh. Code there that touches a signal (a
//! global one above all) or spawns a task needs a current runtime, or Dioxus panics.
//!
//! The runtime is captured where it is current - when the application configures the clients,
//! connects a hub or registers a handler, all of which it does while rendering or from a Dioxus
//! task - and is entered again, at the root scope, when that code runs. The most recently seen
//! runtime is also remembered for the thread, for code that is set up before the application has
//! rendered. Only weak references are kept: once the application's runtime is gone, or where
//! there never was one (native unit tests), the code simply runs.

use std::cell::RefCell;
use std::future::Future;
use std::pin::Pin;
use std::rc::{Rc, Weak};
use std::task::{Context, Poll};

use dioxus::dioxus_core::{Runtime, ScopeId};

thread_local! {
    static REMEMBERED: RefCell<Option<Weak<Runtime>>> = const { RefCell::new(None) };
}

/// The Dioxus runtime that application code must run in, as captured at one moment.
#[derive(Clone, Default)]
pub(crate) struct AppRuntime {
    runtime: Option<Weak<Runtime>>,
}

impl AppRuntime {
    /// The runtime that is current now, which is also remembered for the thread; outside a
    /// runtime, the one remembered last (if it still exists).
    pub(crate) fn capture() -> AppRuntime {
        match Runtime::try_current() {
            Some(current) => {
                let runtime = Rc::downgrade(&current);
                let _ = REMEMBERED.try_with(|remembered| *remembered.borrow_mut() = Some(runtime.clone()));
                AppRuntime { runtime: Some(runtime) }
            }
            None => AppRuntime { runtime: REMEMBERED.try_with(|remembered| remembered.borrow().clone()).ok().flatten() },
        }
    }

    /// Runs `f` with the runtime current, at the root scope; directly when it is current already,
    /// or when there is no runtime to enter.
    pub(crate) fn run<O>(&self, f: impl FnOnce() -> O) -> O {
        match self.runtime.as_ref().and_then(Weak::upgrade) {
            Some(runtime) if !is_current(&runtime) => runtime.in_scope(ScopeId::ROOT, f),
            _ => f(),
        }
    }

    /// `future`, polled with the runtime current each time.
    pub(crate) fn enter<F: Future>(&self, future: F) -> InRuntime<F> {
        InRuntime { runtime: self.clone(), future: Box::pin(future) }
    }

    /// Whether a runtime was captured that still exists.
    #[cfg(test)]
    pub(crate) fn is_live(&self) -> bool {
        self.runtime.as_ref().and_then(Weak::upgrade).is_some()
    }
}

fn is_current(runtime: &Rc<Runtime>) -> bool {
    Runtime::try_current().is_some_and(|current| Rc::ptr_eq(&current, runtime))
}

/// A future polled inside an [`AppRuntime`].
pub(crate) struct InRuntime<F: Future> {
    runtime: AppRuntime,
    future: Pin<Box<F>>,
}

impl<F: Future> Future for InRuntime<F> {
    type Output = F::Output;

    fn poll(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<F::Output> {
        let this = self.get_mut();
        let future = &mut this.future;
        this.runtime.run(|| future.as_mut().poll(cx))
    }
}

/// Forgets the remembered runtime (tests share threads when run with one test thread).
#[cfg(test)]
pub(crate) fn forget_for_tests() {
    let _ = REMEMBERED.try_with(|remembered| *remembered.borrow_mut() = None);
}

#[cfg(test)]
mod tests {
    use std::cell::Cell;

    use dioxus::prelude::*;

    use super::*;

    static COUNT: GlobalSignal<i32> = Signal::global(|| 0);

    fn app() -> Element {
        rsx! {}
    }

    struct Forget;

    impl Drop for Forget {
        fn drop(&mut self) {
            forget_for_tests();
        }
    }

    #[test]
    fn outside_any_runtime_code_runs_directly() {
        let _forget = Forget;
        forget_for_tests();
        let captured = AppRuntime::capture();
        assert!(!captured.is_live());
        assert!(!captured.run(|| Runtime::try_current().is_some()));
    }

    #[test]
    fn a_captured_runtime_is_entered_from_outside_it() {
        let _forget = Forget;
        let dom = VirtualDom::new(app);
        let captured = dom.in_runtime(AppRuntime::capture);
        assert!(Runtime::try_current().is_none());
        // A global signal needs the runtime: this is what panicked before.
        captured.run(|| *COUNT.write() += 1);
        assert_eq!(dom.in_runtime(|| *COUNT.peek()), 1);
    }

    #[test]
    fn the_runtime_is_remembered_for_code_set_up_outside_it() {
        let _forget = Forget;
        let dom = VirtualDom::new(app);
        dom.in_runtime(AppRuntime::capture);
        let later = AppRuntime::capture();
        assert!(later.is_live());
        assert!(later.run(|| Runtime::try_current().is_some()));
    }

    #[test]
    fn a_runtime_that_is_gone_is_not_entered() {
        let _forget = Forget;
        let dom = VirtualDom::new(app);
        let captured = dom.in_runtime(AppRuntime::capture);
        drop(dom);
        assert!(!captured.is_live());
        assert!(!AppRuntime::capture().is_live());
        assert_eq!(captured.run(|| 7), 7);
    }

    #[test]
    fn a_future_is_polled_inside_the_runtime() {
        let _forget = Forget;
        let dom = VirtualDom::new(app);
        let captured = dom.in_runtime(AppRuntime::capture);
        let seen = Rc::new(Cell::new(false));
        let sink = seen.clone();
        let future = captured.enter(async move {
            sink.set(Runtime::try_current().is_some());
            // Spawning needs the runtime too (the shared session refresh does it).
            dioxus::dioxus_core::spawn_forever(async {});
        });
        crate::signalr::testing::block_on(future);
        assert!(seen.get());
    }
}
