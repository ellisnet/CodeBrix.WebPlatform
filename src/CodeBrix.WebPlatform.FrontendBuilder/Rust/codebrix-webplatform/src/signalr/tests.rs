//! Behaviour of the client against the fake hub: the same connection logic the browser runs,
//! with the virtual clock.

use std::cell::{Cell, RefCell};
use std::rc::Rc;

use dioxus::prelude::*;
use serde_json::{json, Value};

use super::testing::{advance, block_on, settle, FakeHub};
use super::*;

/// Puts module-wide settings back when a test ends.
struct Reset;

impl Drop for Reset {
    fn drop(&mut self) {
        reset_for_tests();
    }
}

fn record_states(connection: &HubConnection) -> (Rc<RefCell<Vec<ConnectionState>>>, Subscription) {
    let states = Rc::new(RefCell::new(Vec::new()));
    let sink = states.clone();
    let subscription = connection.on_state_changed(move |state| sink.borrow_mut().push(state));
    (states, subscription)
}

fn count_reconnects(connection: &HubConnection) -> (Rc<Cell<usize>>, Subscription) {
    let count = Rc::new(Cell::new(0));
    let sink = count.clone();
    let subscription = connection.on_reconnected(move || sink.set(sink.get() + 1));
    (count, subscription)
}

fn refresh_hook(result: bool, calls: Rc<Cell<usize>>) -> SignalRConfig {
    SignalRConfig {
        refresh: Some(Rc::new(move || {
            calls.set(calls.get() + 1);
            Box::pin(async move { result }) as RefreshFuture
        })),
    }
}

// ---- Connecting ------------------------------------------------------------------------------

#[test]
fn connect_negotiates_opens_the_socket_and_completes_the_handshake() {
    static HUB: HubDescriptor = HubDescriptor { name: "Timer", path: "/hubs/timer", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let connection = connect(&HUB);
    assert_eq!(connection.state(), ConnectionState::Connecting);
    settle();
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(fake.negotiations(), 1);
    assert_eq!(fake.last_negotiate_url().as_deref(), Some("/hubs/timer/negotiate?negotiateVersion=1"));
    assert_eq!(fake.last_socket_url().as_deref(), Some("/hubs/timer?id=token-1"));
    assert_eq!(fake.open_sockets(), 1);
    assert_eq!(connection.hub().name, "Timer");
}

#[test]
fn state_changes_are_reported_in_order() {
    static HUB: HubDescriptor = HubDescriptor { name: "States", path: "/hubs/states", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let connection = connect(&HUB);
    let (states, _subscription) = record_states(&connection);
    settle();
    fake.drop_connection();
    assert_eq!(
        *states.borrow(),
        vec![ConnectionState::Connected, ConnectionState::Reconnecting, ConnectionState::Connected]
    );
}

#[test]
fn a_server_that_cannot_be_reached_is_retried_while_connecting() {
    static HUB: HubDescriptor = HubDescriptor { name: "Away", path: "/hubs/away", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.refuse_connections(true);
    let connection = connect(&HUB);
    settle();
    assert_eq!(connection.state(), ConnectionState::Connecting);
    assert_eq!(fake.negotiations(), 1);
    advance(2_000);
    assert_eq!(fake.negotiations(), 2);
    fake.refuse_connections(false);
    advance(10_000);
    assert_eq!(connection.state(), ConnectionState::Connected);
}

// ---- Invoke and send -------------------------------------------------------------------------

#[test]
fn invoke_returns_the_decoded_result() {
    static HUB: HubDescriptor = HubDescriptor { name: "Math", path: "/hubs/math", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.on_invoke("Add", |args| {
        Ok(json!(arg::<i32>(args, 0).unwrap_or_default() + arg::<i32>(args, 1).unwrap_or_default()))
    });
    let connection = connect(&HUB);
    // Called before the connection is open: it waits for it.
    let sum = block_on(connection.invoke::<i32>("add", vec![to_arg(&2), to_arg(&3)]));
    assert_eq!(sum, Ok(5));
    assert_eq!(fake.sent(), vec![("add".to_string(), vec![json!(2), json!(3)])]);
}

#[test]
fn invoke_of_a_method_without_a_result_returns_unit() {
    static HUB: HubDescriptor = HubDescriptor { name: "Void", path: "/hubs/void", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.on_invoke("Save", |_| Ok(Value::Null));
    let connection = connect(&HUB);
    assert_eq!(block_on(connection.invoke::<()>("Save", vec![json!({"a": 1})])), Ok(()));
}

#[test]
fn invoke_reports_the_server_error() {
    static HUB: HubDescriptor = HubDescriptor { name: "Fails", path: "/hubs/fails", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.on_invoke("Divide", |_| Err("Cannot divide by zero.".to_string()));
    let connection = connect(&HUB);
    let result = block_on(connection.invoke::<i32>("Divide", vec![json!(1), json!(0)]));
    assert_eq!(result, Err(HubError::Server("Cannot divide by zero.".to_string())));
    let unknown = block_on(connection.invoke::<i32>("Missing", vec![]));
    assert!(matches!(unknown, Err(HubError::Server(_))));
}

#[test]
fn invoke_reports_a_decode_error() {
    static HUB: HubDescriptor = HubDescriptor { name: "Odd", path: "/hubs/odd", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.on_invoke("Count", |_| Ok(json!("not a number")));
    let connection = connect(&HUB);
    assert_eq!(block_on(connection.invoke::<i32>("Count", vec![])), Err(HubError::Decode));
}

#[test]
fn invoke_fails_with_not_connected_when_the_server_cannot_be_reached() {
    static HUB: HubDescriptor = HubDescriptor { name: "Gone", path: "/hubs/gone", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.refuse_connections(true);
    let connection = connect(&HUB);
    settle();
    // Waiting in backoff: the call makes the connection try again at once, and fails with it.
    assert_eq!(block_on(connection.invoke::<i32>("Any", vec![])), Err(HubError::NotConnected));
    assert_eq!(fake.negotiations(), 2);
}

#[test]
fn a_pending_invocation_fails_with_closed_when_the_connection_drops() {
    static HUB: HubDescriptor = HubDescriptor { name: "Slow", path: "/hubs/slow", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.never_answer("Slow");
    let connection = connect(&HUB);
    settle();
    let outcome = Rc::new(RefCell::new(None));
    let sink = outcome.clone();
    let caller = connection.clone();
    runtime::spawn(async move {
        *sink.borrow_mut() = Some(caller.invoke::<i32>("Slow", vec![]).await);
    });
    settle();
    assert_eq!(*outcome.borrow(), None);
    fake.drop_connection();
    assert_eq!(*outcome.borrow(), Some(Err(HubError::Closed)));
    assert_eq!(connection.state(), ConnectionState::Connected);
}

#[test]
fn send_is_fire_and_forget() {
    static HUB: HubDescriptor = HubDescriptor { name: "Chat", path: "/hubs/chat", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let connection = connect(&HUB);
    assert_eq!(connection.send("Say", vec![json!("early")]), Err(HubError::NotConnected));
    settle();
    assert_eq!(connection.send("Say", vec![json!("hello")]), Ok(()));
    assert_eq!(fake.sent(), vec![("Say".to_string(), vec![json!("hello")])]);
}

#[test]
fn sent_lists_invokes_sends_and_streams_in_order() {
    static HUB: HubDescriptor = HubDescriptor { name: "Order", path: "/hubs/order", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.on_invoke("First", |_| Ok(json!(1)));
    fake.on_stream("Third", |_| vec![json!(1)]);
    let connection = connect(&HUB);
    let _ = block_on(connection.invoke::<i32>("First", vec![json!("a")]));
    connection.send("Second", vec![json!("b")]).expect("connected");
    let mut stream = connection.stream::<i32>("Third", vec![json!("c")]);
    let _ = block_on(stream.next());
    let names: Vec<String> = fake.sent().into_iter().map(|(name, _)| name).collect();
    assert_eq!(names, vec!["First", "Second", "Third"]);
}

// ---- Server-to-client messages ---------------------------------------------------------------

#[test]
fn server_messages_reach_handlers_by_name_without_regard_to_case() {
    static HUB: HubDescriptor = HubDescriptor { name: "News", path: "/hubs/news", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let connection = connect(&HUB);
    let received = Rc::new(RefCell::new(Vec::new()));
    let sink = received.clone();
    let _subscription = connection.on("headline", move |args| {
        sink.borrow_mut().push(arg::<String>(args, 0).unwrap_or_default());
    });
    settle();
    fake.push("Headline", vec![json!("first")]);
    fake.push("Other", vec![json!("ignored")]);
    fake.push("HEADLINE", vec![json!("second")]);
    assert_eq!(*received.borrow(), vec!["first".to_string(), "second".to_string()]);
    assert_eq!(connection.last_message("headline"), Some(vec![json!("second")]));
}

#[test]
fn handlers_survive_a_reconnect() {
    static HUB: HubDescriptor = HubDescriptor { name: "Ticks", path: "/hubs/ticks", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let connection = connect(&HUB);
    let count = Rc::new(Cell::new(0));
    let sink = count.clone();
    connection.on("Tick", move |_| sink.set(sink.get() + 1)).forget();
    settle();
    fake.push("Tick", vec![]);
    fake.drop_connection();
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(fake.sockets_opened(), 2);
    fake.push("Tick", vec![]);
    assert_eq!(count.get(), 2);
}

#[test]
fn dropping_a_subscription_unregisters_the_handler() {
    static HUB: HubDescriptor = HubDescriptor { name: "Unsub", path: "/hubs/unsub", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let connection = connect(&HUB);
    let count = Rc::new(Cell::new(0));
    let sink = count.clone();
    let subscription = connection.on("Tick", move |_| sink.set(sink.get() + 1));
    settle();
    fake.push("Tick", vec![]);
    drop(subscription);
    fake.push("Tick", vec![]);
    assert_eq!(count.get(), 1);
}

#[test]
fn on_reconnected_fires_after_a_reconnect_but_not_on_the_first_connect() {
    static HUB: HubDescriptor = HubDescriptor { name: "Again", path: "/hubs/again", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let connection = connect(&HUB);
    let (reconnects, _subscription) = count_reconnects(&connection);
    settle();
    assert_eq!(reconnects.get(), 0);
    fake.drop_connection();
    assert_eq!(reconnects.get(), 1);
    fake.drop_connection();
    assert_eq!(reconnects.get(), 2);
}

// ---- Streams ---------------------------------------------------------------------------------

#[test]
fn stream_yields_the_items_then_completes() {
    static HUB: HubDescriptor = HubDescriptor { name: "Count", path: "/hubs/count", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.on_stream("Count", |args| {
        (1..=arg::<i32>(args, 0).unwrap_or_default()).map(|n| json!(n)).collect()
    });
    let connection = connect(&HUB);
    let mut stream = connection.stream::<i32>("Count", vec![json!(3)]);
    let mut items = Vec::new();
    while let Some(item) = block_on(stream.next()) {
        items.push(item);
    }
    assert_eq!(items, vec![Ok(1), Ok(2), Ok(3)]);
    assert_eq!(block_on(stream.next()), None);
    drop(stream);
    settle();
    assert_eq!(fake.cancelled_streams(), 0);
}

#[test]
fn stream_reports_a_server_error() {
    static HUB: HubDescriptor = HubDescriptor { name: "Broken", path: "/hubs/broken", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.on_stream_error("Feed", |_| vec![json!(1)], "The feed failed.");
    let connection = connect(&HUB);
    let mut stream = connection.stream::<i32>("Feed", vec![]);
    assert_eq!(block_on(stream.next()), Some(Ok(1)));
    assert_eq!(block_on(stream.next()), Some(Err(HubError::Server("The feed failed.".to_string()))));
    assert_eq!(block_on(stream.next()), None);
}

#[test]
fn stream_reports_an_item_that_cannot_be_decoded() {
    static HUB: HubDescriptor = HubDescriptor { name: "Mixed", path: "/hubs/mixed", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.on_stream("Mixed", |_| vec![json!(1), json!("two"), json!(3)]);
    let connection = connect(&HUB);
    let mut stream = connection.stream::<i32>("Mixed", vec![]);
    assert_eq!(block_on(stream.next()), Some(Ok(1)));
    assert_eq!(block_on(stream.next()), Some(Err(HubError::Decode)));
    assert_eq!(block_on(stream.next()), Some(Ok(3)));
    assert_eq!(block_on(stream.next()), None);
}

#[test]
fn dropping_a_stream_early_sends_cancel_invocation() {
    static HUB: HubDescriptor = HubDescriptor { name: "Live", path: "/hubs/live", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.on_stream_open("Prices", |_| vec![json!(10), json!(11)]);
    let connection = connect(&HUB);
    let mut stream = connection.stream::<i32>("Prices", vec![]);
    assert_eq!(block_on(stream.next()), Some(Ok(10)));
    assert_eq!(fake.cancelled_streams(), 0);
    drop(stream);
    assert_eq!(fake.cancelled_streams(), 1);
}

#[test]
fn a_stream_never_polled_sends_nothing() {
    static HUB: HubDescriptor = HubDescriptor { name: "Idle", path: "/hubs/idle", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let connection = connect(&HUB);
    settle();
    drop(connection.stream::<i32>("Prices", vec![]));
    settle();
    assert!(fake.sent().is_empty());
    assert_eq!(fake.cancelled_streams(), 0);
}

#[test]
fn an_open_stream_fails_with_closed_when_the_connection_drops() {
    static HUB: HubDescriptor = HubDescriptor { name: "Cut", path: "/hubs/cut", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.on_stream_open("Prices", |_| vec![json!(10)]);
    let connection = connect(&HUB);
    let mut stream = connection.stream::<i32>("Prices", vec![]);
    assert_eq!(block_on(stream.next()), Some(Ok(10)));
    fake.drop_connection();
    assert_eq!(block_on(stream.next()), Some(Err(HubError::Closed)));
    assert_eq!(block_on(stream.next()), None);
    drop(stream);
    assert_eq!(fake.cancelled_streams(), 0);
}

// ---- Shared connections ----------------------------------------------------------------------

#[test]
fn handles_share_one_socket_that_closes_after_the_last_is_dropped() {
    static HUB: HubDescriptor = HubDescriptor { name: "Shared", path: "/hubs/shared", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let first = connect(&HUB);
    let second = connect(&HUB);
    let third = first.clone();
    settle();
    assert_eq!(fake.sockets_opened(), 1);
    let (states, subscription) = record_states(&first);
    subscription.forget();
    drop(first);
    drop(second);
    advance(5_000);
    assert_eq!(fake.open_sockets(), 1);
    assert_eq!(third.state(), ConnectionState::Connected);
    drop(third);
    advance(1_999);
    assert_eq!(fake.open_sockets(), 1);
    advance(1);
    assert_eq!(fake.open_sockets(), 0);
    assert_eq!(*states.borrow(), vec![ConnectionState::Disconnected]);
    // A later handle makes a new connection.
    let again = connect(&HUB);
    settle();
    assert_eq!(again.state(), ConnectionState::Connected);
    assert_eq!(fake.sockets_opened(), 2);
}

#[test]
fn a_new_handle_during_the_linger_keeps_the_socket() {
    static HUB: HubDescriptor = HubDescriptor { name: "Linger", path: "/hubs/linger", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    drop(connect(&HUB));
    settle();
    advance(1_000);
    let connection = connect(&HUB);
    advance(10_000);
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(fake.sockets_opened(), 1);
    assert_eq!(fake.open_sockets(), 1);
}

// ---- Sign-in ---------------------------------------------------------------------------------

#[test]
fn unauthorized_negotiate_refreshes_the_session_and_retries() {
    static HUB: HubDescriptor = HubDescriptor { name: "Secure", path: "/hubs/secure", requires_sign_in: true };
    let _reset = Reset;
    let calls = Rc::new(Cell::new(0));
    configure(refresh_hook(true, calls.clone()));
    let fake = FakeHub::install(&HUB);
    fake.require_sign_in_failure(true);
    let connection = connect(&HUB);
    settle();
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(calls.get(), 1);
    assert_eq!(fake.negotiations(), 2);
}

#[test]
fn a_refused_refresh_leaves_the_connection_disconnected() {
    static HUB: HubDescriptor = HubDescriptor { name: "Refused", path: "/hubs/refused", requires_sign_in: true };
    let _reset = Reset;
    let calls = Rc::new(Cell::new(0));
    configure(refresh_hook(false, calls.clone()));
    let fake = FakeHub::install(&HUB);
    fake.require_sign_in_failure(true);
    let connection = connect(&HUB);
    settle();
    assert_eq!(connection.state(), ConnectionState::Disconnected);
    assert_eq!(calls.get(), 1);
    assert_eq!(fake.negotiations(), 1);
    assert_eq!(block_on(connection.invoke::<i32>("Any", vec![])), Err(HubError::Unauthorized));
    assert_eq!(connection.send("Any", vec![]), Err(HubError::Unauthorized));
    advance(120_000);
    assert_eq!(fake.negotiations(), 1);
}

#[test]
fn without_a_refresh_hook_unauthorized_means_disconnected() {
    static HUB: HubDescriptor = HubDescriptor { name: "NoHook", path: "/hubs/nohook", requires_sign_in: true };
    let _reset = Reset;
    let fake = FakeHub::install(&HUB);
    fake.require_sign_in_failure(true);
    let connection = connect(&HUB);
    settle();
    assert_eq!(connection.state(), ConnectionState::Disconnected);
    assert_eq!(block_on(connection.invoke::<i32>("Any", vec![])), Err(HubError::Unauthorized));
}

#[test]
fn session_ended_closes_signed_in_hubs_until_session_started() {
    static SECURE: HubDescriptor = HubDescriptor { name: "Mine", path: "/hubs/mine", requires_sign_in: true };
    static PUBLIC: HubDescriptor = HubDescriptor { name: "Open", path: "/hubs/open", requires_sign_in: false };
    let _reset = Reset;
    let secure_fake = FakeHub::install(&SECURE);
    let public_fake = FakeHub::install(&PUBLIC);
    let secure = connect(&SECURE);
    let public = connect(&PUBLIC);
    let (reconnects, _subscription) = count_reconnects(&secure);
    settle();
    assert_eq!(secure.state(), ConnectionState::Connected);

    session_ended();
    settle();
    assert_eq!(secure.state(), ConnectionState::Disconnected);
    assert_eq!(public.state(), ConnectionState::Connected);
    assert_eq!(secure_fake.open_sockets(), 0);
    assert_eq!(block_on(secure.invoke::<i32>("Any", vec![])), Err(HubError::Unauthorized));
    advance(120_000);
    let another = connect(&SECURE);
    settle();
    assert_eq!(another.state(), ConnectionState::Disconnected);
    assert_eq!(secure_fake.negotiations(), 1);
    assert_eq!(public_fake.sockets_opened(), 1);

    session_started();
    settle();
    assert_eq!(secure.state(), ConnectionState::Connected);
    assert_eq!(secure_fake.negotiations(), 2);
    assert_eq!(reconnects.get(), 1);
}

#[test]
fn session_ended_fails_pending_invocations() {
    static HUB: HubDescriptor = HubDescriptor { name: "Pending", path: "/hubs/pending", requires_sign_in: true };
    let _reset = Reset;
    let fake = FakeHub::install(&HUB);
    fake.never_answer("Slow");
    let connection = connect(&HUB);
    settle();
    let outcome = Rc::new(RefCell::new(None));
    let sink = outcome.clone();
    let caller = connection.clone();
    runtime::spawn(async move {
        *sink.borrow_mut() = Some(caller.invoke::<i32>("Slow", vec![]).await);
    });
    settle();
    session_ended();
    settle();
    assert_eq!(*outcome.borrow(), Some(Err(HubError::Unauthorized)));
}

// ---- Session cookies changing while a hub connects --------------------------------------------

#[test]
fn a_hub_does_not_negotiate_while_a_state_changing_request_is_in_flight() {
    static HUB: HubDescriptor = HubDescriptor { name: "Clock", path: "/hubs/settle-clock", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    // A sign-out on its way to the server: its answer will clear the session cookies.
    let sign_out = crate::api::client::StateChangeInFlight::begin();
    let connection = connect(&HUB);
    settle();
    assert_eq!(connection.state(), ConnectionState::Connecting);
    assert_eq!(fake.negotiations(), 0);
    drop(sign_out);
    settle();
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(fake.negotiations(), 1);
    assert_eq!(fake.refused_sockets(), 0);
}

#[test]
fn a_token_negotiated_before_the_cookies_changed_is_thrown_away_and_negotiated_again() {
    static HUB: HubDescriptor = HubDescriptor { name: "Clock", path: "/hubs/changed-clock", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    // The sign-out is sent and answered while the first negotiate request is on its way.
    let first = Cell::new(true);
    fake.on_negotiate(move || {
        if first.replace(false) {
            drop(crate::api::client::StateChangeInFlight::begin());
        }
    });
    let connection = connect(&HUB);
    settle();
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(fake.negotiations(), 2);
    assert_eq!(fake.sockets_opened(), 1);
    assert_eq!(fake.refused_sockets(), 0);
}

#[test]
fn a_request_sent_while_negotiating_is_waited_for_before_negotiating_again() {
    static HUB: HubDescriptor = HubDescriptor { name: "Clock", path: "/hubs/waiting-clock", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let in_flight = Rc::new(RefCell::new(None));
    let holder = in_flight.clone();
    let first = Cell::new(true);
    fake.on_negotiate(move || {
        if first.replace(false) {
            *holder.borrow_mut() = Some(crate::api::client::StateChangeInFlight::begin());
        }
    });
    let connection = connect(&HUB);
    settle();
    assert_eq!(connection.state(), ConnectionState::Connecting);
    assert_eq!(fake.negotiations(), 1);
    assert_eq!(fake.sockets_opened(), 0);
    in_flight.borrow_mut().take();
    settle();
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(fake.negotiations(), 2);
    assert_eq!(fake.sockets_opened(), 1);
    assert_eq!(fake.refused_sockets(), 0);
}

#[test]
fn a_request_that_never_finishes_holds_a_connection_back_for_ten_seconds_only() {
    static HUB: HubDescriptor = HubDescriptor { name: "Clock", path: "/hubs/stuck-clock", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let _stuck = crate::api::client::StateChangeInFlight::begin();
    let connection = connect(&HUB);
    advance(9_999);
    assert_eq!(fake.negotiations(), 0);
    advance(1);
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(fake.negotiations(), 1);
}

#[test]
fn the_fake_refuses_a_socket_opened_after_the_cookies_changed() {
    static HUB: HubDescriptor = HubDescriptor { name: "Clock", path: "/hubs/fake-rule", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let mut queue_events = Vec::new();
    // Drive the transport directly, as an old client that ignored the cookies would.
    let transport = testing::transport_for(HUB.path);
    let _ = block_on(transport.negotiate("/hubs/fake-rule/negotiate?negotiateVersion=1", &[]));
    drop(crate::api::client::StateChangeInFlight::begin());
    let events = Rc::new(transport::EventQueue::default());
    let _socket = transport.open("/hubs/fake-rule?id=token-1", events.clone()).expect("the socket is created");
    let mut context = std::task::Context::from_waker(std::task::Waker::noop());
    while let std::task::Poll::Ready(event) = events.poll_next(&mut context) {
        queue_events.push(matches!(event, transport::TransportEvent::Closed));
    }
    assert_eq!(queue_events, vec![true]);
    assert_eq!(fake.refused_sockets(), 1);
}

// ---- Backoff, wake-up and keep-alive ---------------------------------------------------------

#[test]
fn reconnect_attempts_follow_the_backoff_schedule() {
    static HUB: HubDescriptor = HubDescriptor { name: "Backoff", path: "/hubs/backoff", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let connection = connect(&HUB);
    let (reconnects, _subscription) = count_reconnects(&connection);
    settle();
    fake.refuse_connections(true);
    fake.drop_connection();
    // The first retry is immediate.
    assert_eq!(connection.state(), ConnectionState::Reconnecting);
    assert_eq!(fake.negotiations(), 2);
    for (delay, attempts) in [(2_000, 3), (10_000, 4), (30_000, 5), (30_000, 6)] {
        advance(delay - 1);
        assert_eq!(fake.negotiations(), attempts - 1, "not before {delay} ms");
        advance(1);
        assert_eq!(fake.negotiations(), attempts, "after {delay} ms");
    }
    fake.refuse_connections(false);
    advance(30_000);
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(reconnects.get(), 1);
}

#[test]
fn wake_up_ends_the_backoff_wait() {
    static HUB: HubDescriptor = HubDescriptor { name: "Wake", path: "/hubs/wake", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let connection = connect(&HUB);
    settle();
    fake.refuse_connections(true);
    fake.drop_connection();
    advance(2_000);
    assert_eq!(fake.negotiations(), 3);
    fake.refuse_connections(false);
    wake_up();
    settle();
    assert_eq!(connection.state(), ConnectionState::Connected);
}

#[test]
fn the_client_pings_after_15_seconds_without_sending() {
    static HUB: HubDescriptor = HubDescriptor { name: "Ping", path: "/hubs/ping", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    let connection = connect(&HUB);
    settle();
    advance(14_999);
    assert_eq!(fake.pings_received(), 0);
    advance(1);
    assert_eq!(fake.pings_received(), 1);
    // Anything sent resets the ping timer.
    advance(10_000);
    connection.send("Hello", vec![]).expect("connected");
    advance(14_999);
    assert_eq!(fake.pings_received(), 1);
    advance(1);
    assert_eq!(fake.pings_received(), 2);
    // The fake answers each ping, so the connection stays up.
    advance(120_000);
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(fake.sockets_opened(), 1);
}

#[test]
fn a_server_silent_for_30_seconds_counts_as_lost() {
    static HUB: HubDescriptor = HubDescriptor { name: "Quiet", path: "/hubs/quiet", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.silence(true);
    let connection = connect(&HUB);
    let (reconnects, _subscription) = count_reconnects(&connection);
    settle();
    advance(29_999);
    assert_eq!(fake.sockets_opened(), 1);
    advance(1);
    assert_eq!(fake.sockets_opened(), 2);
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(reconnects.get(), 1);
}

// ---- The Dioxus runtime ---------------------------------------------------------------------

/// Written by handlers; a global signal, which panics when written outside a Dioxus runtime.
static SEEN: GlobalSignal<Vec<String>> = Signal::global(Vec::new);
static REFRESHES: GlobalSignal<u32> = Signal::global(|| 0);

fn app() -> Element {
    rsx! {}
}

#[test]
fn handlers_run_inside_the_runtime_they_were_registered_in() {
    static HUB: HubDescriptor = HubDescriptor { name: "InApp", path: "/hubs/in-app", requires_sign_in: false };
    let _reset = Reset;
    let dom = VirtualDom::new(app);
    let fake = FakeHub::install(&HUB);
    let (connection, subscriptions) = dom.in_runtime(|| {
        let connection = connect(&HUB);
        let subscriptions = [
            connection.on("Note", |args| SEEN.write().push(arg::<String>(args, 0).unwrap_or_default())),
            connection.on_state_changed(|state| SEEN.write().push(format!("{state:?}"))),
            connection.on_reconnected(|| SEEN.write().push("reconnected".to_string())),
        ];
        (connection, subscriptions)
    });
    // From here on the client runs outside the runtime, as its own tasks do in the browser.
    settle();
    fake.push("Note", vec![json!("hello")]);
    fake.drop_connection();
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(
        dom.in_runtime(|| SEEN.peek().clone()),
        vec!["Connected", "hello", "Reconnecting", "Connected", "reconnected"]
    );
    drop(subscriptions);
}

#[test]
fn the_refresh_hook_is_called_and_polled_inside_the_runtime() {
    static HUB: HubDescriptor = HubDescriptor { name: "Renew", path: "/hubs/renew", requires_sign_in: true };
    let _reset = Reset;
    let dom = VirtualDom::new(app);
    dom.in_runtime(|| {
        configure(SignalRConfig {
            refresh: Some(Rc::new(|| {
                *REFRESHES.write() += 1;
                Box::pin(async {
                    *REFRESHES.write() += 10;
                    true
                }) as RefreshFuture
            })),
        })
    });
    let fake = FakeHub::install(&HUB);
    fake.require_sign_in_failure(true);
    // Connected outside the runtime: the client uses the one it learned at `configure`.
    let connection = connect(&HUB);
    settle();
    assert_eq!(connection.state(), ConnectionState::Connected);
    assert_eq!(dom.in_runtime(|| *REFRESHES.peek()), 11);
}

#[test]
fn using_rest_refresh_supplies_a_refresh_hook() {
    assert!(SignalRConfig::using_rest_refresh().refresh.is_some());
    assert!(SignalRConfig::default().refresh.is_none());
}

#[test]
fn use_subscription_keeps_handlers_while_the_component_is_mounted() {
    static HUB: HubDescriptor = HubDescriptor { name: "Mounted", path: "/hubs/mounted", requires_sign_in: false };
    fn listener() -> Element {
        let connection = use_hub(&HUB);
        use_subscription(|| connection.on("Note", |args| SEEN.write().push(arg::<String>(args, 0).unwrap_or_default())));
        let latest = use_hub_message(&HUB, "Note", |args| arg::<String>(args, 0));
        let _ = latest.peek();
        rsx! {}
    }
    let _reset = Reset;
    let fake = FakeHub::install(&HUB);
    let mut dom = VirtualDom::new(listener);
    dom.rebuild_in_place();
    settle();
    fake.push("Note", vec![json!("one")]);
    fake.push("Note", vec![json!("two")]);
    assert_eq!(dom.in_runtime(|| SEEN.peek().clone()), vec!["one", "two"]);
    drop(dom);
    fake.push("Note", vec![json!("three")]);
    assert_eq!(fake.open_sockets(), 1);
    advance(2_000);
    assert_eq!(fake.open_sockets(), 0);
}

// ---- Helpers and errors ----------------------------------------------------------------------

#[test]
fn arg_and_to_arg_convert_arguments() {
    let args = vec![to_arg(&5), to_arg(&"text"), to_arg(&vec![1, 2])];
    assert_eq!(arg::<i32>(&args, 0), Some(5));
    assert_eq!(arg::<String>(&args, 1), Some("text".to_string()));
    assert_eq!(arg::<Vec<i32>>(&args, 2), Some(vec![1, 2]));
    assert_eq!(arg::<i32>(&args, 1), None);
    assert_eq!(arg::<i32>(&args, 3), None);
}

#[test]
fn server_error_text_unwraps_a_hub_exception() {
    let wrapped = "An unexpected error occurred invoking 'Estimate' on the server. HubException: Hours must be more than 0 and at most 24.";
    assert_eq!(server_error_text(wrapped), "Hours must be more than 0 and at most 24.");
}

#[test]
fn server_error_text_keeps_text_without_the_marker() {
    assert_eq!(server_error_text("Cannot divide by zero."), "Cannot divide by zero.");
    assert_eq!(server_error_text(""), "");
}

#[test]
fn server_error_text_splits_at_the_first_marker_only() {
    let twice = "Failed. HubException: Outer said HubException: inner.";
    assert_eq!(server_error_text(twice), "Outer said HubException: inner.");
}

#[test]
fn server_error_text_may_leave_nothing_after_the_marker() {
    assert_eq!(server_error_text("Failed on the server. HubException:   "), "");
    assert_eq!(HubError::Server(server_error_text("x HubException: ")).message(), "The server could not complete the request.");
}

#[test]
fn invocations_and_streams_unwrap_the_hub_exception_text() {
    static HUB: HubDescriptor = HubDescriptor { name: "Wrapped", path: "/hubs/wrapped", requires_sign_in: false };
    let fake = FakeHub::install(&HUB);
    fake.on_invoke("Check", |_| Err("An unexpected error occurred invoking 'Check' on the server. HubException: Too many.".to_string()));
    fake.on_stream_error("Feed", |_| Vec::new(), "An unexpected error occurred invoking 'Feed' on the server. HubException: Not yours.");
    let connection = connect(&HUB);
    assert_eq!(block_on(connection.invoke::<()>("Check", vec![])), Err(HubError::Server("Too many.".to_string())));
    let mut stream = connection.stream::<i32>("Feed", vec![]);
    assert_eq!(block_on(stream.next()), Some(Err(HubError::Server("Not yours.".to_string()))));
}

#[test]
fn errors_read_as_sentences() {
    assert_eq!(HubError::Server("The name is taken.".to_string()).message(), "The name is taken.");
    assert_eq!(HubError::Unauthorized.to_string(), HubError::Unauthorized.message());
    for error in [
        HubError::NotConnected,
        HubError::Unauthorized,
        HubError::Network,
        HubError::Server(String::new()),
        HubError::Decode,
        HubError::Cancelled,
        HubError::Closed,
    ] {
        assert!(error.message().ends_with('.'), "{error:?}");
    }
}
