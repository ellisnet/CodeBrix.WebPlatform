//! Tasks and time for the connection logic.
//!
//! In the browser (wasm32): `spawn_local`, the `Date` clock and `setTimeout` (cleared when a
//! sleep is dropped). In a native build, which exists for unit tests: a single-threaded executor
//! and a virtual clock that moves only when a test advances it, so backoff and keep-alive are
//! exercised without sleeping for real.

use std::cell::{Cell, RefCell};
use std::task::{Context, Poll, Waker};

pub(crate) use imp::{now_ms, sleep, spawn, Sleep};
#[cfg(not(target_arch = "wasm32"))]
pub(crate) use imp::{advance, block_on, run_until_stalled};

/// A counter that tasks can wait on: `notify` bumps it and wakes everyone waiting for a change.
#[derive(Default)]
pub(crate) struct Notify {
    count: Cell<u64>,
    wakers: RefCell<Vec<Waker>>,
}

impl Notify {
    pub(crate) fn notify(&self) {
        self.count.set(self.count.get().wrapping_add(1));
        let wakers: Vec<Waker> = self.wakers.borrow_mut().drain(..).collect();
        for waker in wakers {
            waker.wake();
        }
    }

    pub(crate) fn count(&self) -> u64 {
        self.count.get()
    }

    /// Wakes the current task at the next `notify`.
    pub(crate) fn register(&self, cx: &Context<'_>) {
        let mut wakers = self.wakers.borrow_mut();
        if !wakers.iter().any(|waker| waker.will_wake(cx.waker())) {
            wakers.push(cx.waker().clone());
        }
    }

    /// Ready once the counter differs from `seen`.
    pub(crate) fn poll_changed(&self, cx: &Context<'_>, seen: u64) -> Poll<()> {
        if self.count.get() != seen {
            Poll::Ready(())
        } else {
            self.register(cx);
            Poll::Pending
        }
    }
}

#[cfg(target_arch = "wasm32")]
mod imp {
    use std::cell::{Cell, RefCell};
    use std::future::Future;
    use std::pin::Pin;
    use std::rc::Rc;
    use std::task::{Context, Poll, Waker};

    use wasm_bindgen::closure::Closure;
    use wasm_bindgen::JsCast;

    pub(crate) fn spawn(task: impl Future<Output = ()> + 'static) {
        wasm_bindgen_futures::spawn_local(task);
    }

    /// Milliseconds since the epoch.
    pub(crate) fn now_ms() -> f64 {
        js_sys::Date::now()
    }

    #[derive(Default)]
    struct Shared {
        fired: Cell<bool>,
        waker: RefCell<Option<Waker>>,
    }

    /// Resolves after a `setTimeout`; dropping it clears the timeout.
    pub(crate) struct Sleep {
        shared: Rc<Shared>,
        handle: Option<i32>,
        _callback: Option<Closure<dyn FnMut()>>,
    }

    pub(crate) fn sleep(ms: u32) -> Sleep {
        let shared = Rc::new(Shared::default());
        if ms == 0 {
            shared.fired.set(true);
            return Sleep { shared, handle: None, _callback: None };
        }
        let target = shared.clone();
        let callback = Closure::<dyn FnMut()>::new(move || {
            target.fired.set(true);
            let waker = target.waker.borrow_mut().take();
            if let Some(waker) = waker {
                waker.wake();
            }
        });
        let timeout = i32::try_from(ms).unwrap_or(i32::MAX);
        let handle = web_sys::window().and_then(|window| {
            window
                .set_timeout_with_callback_and_timeout_and_arguments_0(callback.as_ref().unchecked_ref(), timeout)
                .ok()
        });
        Sleep { shared, handle, _callback: Some(callback) }
    }

    impl Future for Sleep {
        type Output = ();

        fn poll(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<()> {
            if self.shared.fired.get() {
                Poll::Ready(())
            } else {
                *self.shared.waker.borrow_mut() = Some(cx.waker().clone());
                Poll::Pending
            }
        }
    }

    impl Drop for Sleep {
        fn drop(&mut self) {
            if let Some(handle) = self.handle.take() {
                if !self.shared.fired.get() {
                    if let Some(window) = web_sys::window() {
                        window.clear_timeout_with_handle(handle);
                    }
                }
            }
        }
    }
}

#[cfg(not(target_arch = "wasm32"))]
mod imp {
    use std::cell::{Cell, RefCell};
    use std::collections::{BTreeMap, HashMap, VecDeque};
    use std::future::Future;
    use std::pin::{pin, Pin};
    use std::rc::Rc;
    use std::sync::atomic::{AtomicBool, Ordering};
    use std::sync::{Arc, Mutex};
    use std::task::{Context, Poll, Wake, Waker};

    type Task = Pin<Box<dyn Future<Output = ()>>>;
    type ReadyQueue = Arc<Mutex<VecDeque<u64>>>;

    struct TaskWaker {
        id: u64,
        ready: ReadyQueue,
    }

    impl Wake for TaskWaker {
        fn wake(self: Arc<Self>) {
            self.wake_by_ref();
        }

        fn wake_by_ref(self: &Arc<Self>) {
            if let Ok(mut ready) = self.ready.lock() {
                ready.push_back(self.id);
            }
        }
    }

    #[derive(Default)]
    struct Timer {
        fired: Cell<bool>,
        waker: RefCell<Option<Waker>>,
    }

    impl Timer {
        fn fire(&self) {
            self.fired.set(true);
            let waker = self.waker.borrow_mut().take();
            if let Some(waker) = waker {
                waker.wake();
            }
        }
    }

    #[derive(Default)]
    struct Executor {
        tasks: RefCell<HashMap<u64, Task>>,
        next_task: Cell<u64>,
        ready: ReadyQueue,
        running: Cell<bool>,
        now: Cell<u64>,
        timers: RefCell<BTreeMap<(u64, u64), Rc<Timer>>>,
        next_timer: Cell<u64>,
    }

    thread_local! {
        static EXECUTOR: Executor = Executor::default();
    }

    pub(crate) fn spawn(task: impl Future<Output = ()> + 'static) {
        // During thread teardown the executor may already be gone; the task is then dropped.
        let _ = EXECUTOR.try_with(|executor| {
            let id = executor.next_task.get();
            executor.next_task.set(id + 1);
            executor.tasks.borrow_mut().insert(id, Box::pin(task));
            if let Ok(mut ready) = executor.ready.lock() {
                ready.push_back(id);
            }
        });
    }

    /// Virtual milliseconds since the thread started.
    pub(crate) fn now_ms() -> f64 {
        EXECUTOR.try_with(|executor| executor.now.get() as f64).unwrap_or(0.0)
    }

    /// Resolves when the virtual clock reaches its deadline.
    pub(crate) struct Sleep {
        key: Option<(u64, u64)>,
        timer: Rc<Timer>,
    }

    pub(crate) fn sleep(ms: u32) -> Sleep {
        let timer = Rc::new(Timer::default());
        if ms == 0 {
            timer.fired.set(true);
            return Sleep { key: None, timer };
        }
        let key = EXECUTOR
            .try_with(|executor| {
                let seq = executor.next_timer.get();
                executor.next_timer.set(seq + 1);
                let key = (executor.now.get() + u64::from(ms), seq);
                executor.timers.borrow_mut().insert(key, timer.clone());
                key
            })
            .ok();
        Sleep { key, timer }
    }

    impl Future for Sleep {
        type Output = ();

        fn poll(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<()> {
            if self.timer.fired.get() {
                Poll::Ready(())
            } else {
                *self.timer.waker.borrow_mut() = Some(cx.waker().clone());
                Poll::Pending
            }
        }
    }

    impl Drop for Sleep {
        fn drop(&mut self) {
            if let Some(key) = self.key.take() {
                let _ = EXECUTOR.try_with(|executor| executor.timers.borrow_mut().remove(&key));
            }
        }
    }

    struct RunningGuard;

    impl Drop for RunningGuard {
        fn drop(&mut self) {
            let _ = EXECUTOR.try_with(|executor| executor.running.set(false));
        }
    }

    /// Polls every woken task until none is ready. Returns whether any task was polled. Does
    /// nothing when called from inside a task.
    pub(crate) fn run_until_stalled() -> bool {
        let Ok(false) = EXECUTOR.try_with(|executor| executor.running.replace(true)) else {
            return false;
        };
        let _guard = RunningGuard;
        let mut polled = false;
        loop {
            let next = EXECUTOR.with(|executor| executor.ready.lock().ok().and_then(|mut ready| ready.pop_front()));
            let Some(id) = next else {
                break;
            };
            let task = EXECUTOR.with(|executor| executor.tasks.borrow_mut().remove(&id));
            let Some(mut task) = task else {
                continue;
            };
            polled = true;
            let ready = EXECUTOR.with(|executor| executor.ready.clone());
            let waker = Waker::from(Arc::new(TaskWaker { id, ready }));
            let mut cx = Context::from_waker(&waker);
            if task.as_mut().poll(&mut cx).is_pending() {
                EXECUTOR.with(|executor| executor.tasks.borrow_mut().insert(id, task));
            }
        }
        polled
    }

    /// Moves the virtual clock forward by `ms`, firing every timer that falls due on the way in
    /// deadline order and running the tasks it wakes.
    pub(crate) fn advance(ms: u64) {
        let target = EXECUTOR.with(|executor| executor.now.get() + ms);
        loop {
            run_until_stalled();
            let due = EXECUTOR.with(|executor| {
                let mut timers = executor.timers.borrow_mut();
                let first = timers.keys().next().copied();
                match first {
                    Some(key) if key.0 <= target => {
                        executor.now.set(key.0.max(executor.now.get()));
                        timers.remove(&key)
                    }
                    _ => None,
                }
            });
            match due {
                Some(timer) => timer.fire(),
                None => break,
            }
        }
        EXECUTOR.with(|executor| executor.now.set(target));
        run_until_stalled();
    }

    struct Flag(AtomicBool);

    impl Wake for Flag {
        fn wake(self: Arc<Self>) {
            self.0.store(true, Ordering::SeqCst);
        }

        fn wake_by_ref(self: &Arc<Self>) {
            self.0.store(true, Ordering::SeqCst);
        }
    }

    /// Runs `future` to completion together with the spawned tasks. Panics when it cannot
    /// finish without the virtual clock moving. Not for use from inside a task.
    pub(crate) fn block_on<F: Future>(future: F) -> F::Output {
        let flag = Arc::new(Flag(AtomicBool::new(true)));
        let waker = Waker::from(flag.clone());
        let mut cx = Context::from_waker(&waker);
        let mut future = pin!(future);
        loop {
            if flag.0.swap(false, Ordering::SeqCst) {
                if let Poll::Ready(output) = future.as_mut().poll(&mut cx) {
                    return output;
                }
            }
            let polled = run_until_stalled();
            if !polled && !flag.0.load(Ordering::SeqCst) {
                panic!("block_on: the future cannot finish until the virtual clock moves (call testing::advance)");
            }
        }
    }
}
