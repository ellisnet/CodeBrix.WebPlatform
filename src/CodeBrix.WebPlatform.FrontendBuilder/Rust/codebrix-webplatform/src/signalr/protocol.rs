// Derived from signalr-client 0.3.2 (https://github.com/danielleiszen/rust_signalr_client),
// MIT License, Copyright (c) 2025 Dániel Leiszen. Modified.

//! The SignalR JSON hub protocol (version 1) and the negotiate exchange: framing with the 0x1E
//! record separator, the handshake, the hub messages (types 1-7) and the URLs of a connection.
//! Pure functions only; nothing here touches the network or the clock.

use serde::{Deserialize, Serialize};
use serde_json::Value;

/// Ends every JSON record on the wire.
pub(crate) const RECORD_SEPARATOR: char = '\u{1e}';

pub(crate) const INVOCATION: u8 = 1;
pub(crate) const STREAM_ITEM: u8 = 2;
pub(crate) const COMPLETION: u8 = 3;
pub(crate) const STREAM_INVOCATION: u8 = 4;
pub(crate) const CANCEL_INVOCATION: u8 = 5;
pub(crate) const PING: u8 = 6;
pub(crate) const CLOSE: u8 = 7;

/// One message serialised as JSON and terminated by the record separator.
fn frame<T: Serialize>(message: &T) -> String {
    let mut text = serde_json::to_string(message).unwrap_or_default();
    text.push(RECORD_SEPARATOR);
    text
}

// ---- Handshake -------------------------------------------------------------------------------

#[derive(Serialize)]
struct HandshakeRequest<'a> {
    protocol: &'a str,
    version: u8,
}

/// The first record the client sends: the JSON protocol, version 1.
pub(crate) fn handshake_request() -> String {
    frame(&HandshakeRequest { protocol: "json", version: 1 })
}

#[derive(Deserialize)]
struct HandshakeResponse {
    #[serde(default)]
    error: Option<String>,
    #[serde(default, rename = "type")]
    message_type: Option<u64>,
}

/// Reads the first record the server sends: `{}` on success, `{"error": "..."}` on refusal.
pub(crate) fn parse_handshake_response(record: &str) -> Result<(), String> {
    match serde_json::from_str::<HandshakeResponse>(record) {
        Ok(HandshakeResponse { error: Some(error), .. }) => Err(error),
        Ok(HandshakeResponse { message_type: Some(_), .. }) => {
            Err("Expected a handshake response from the server.".to_string())
        }
        Ok(_) => Ok(()),
        Err(_) => Err("The handshake response could not be read.".to_string()),
    }
}

// ---- Messages the client sends ---------------------------------------------------------------

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct InvocationMessage<'a> {
    #[serde(rename = "type")]
    message_type: u8,
    #[serde(skip_serializing_if = "Option::is_none")]
    invocation_id: Option<&'a str>,
    target: &'a str,
    arguments: &'a [Value],
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct IdMessage<'a> {
    #[serde(rename = "type")]
    message_type: u8,
    invocation_id: &'a str,
}

#[derive(Serialize)]
struct TypeMessage {
    #[serde(rename = "type")]
    message_type: u8,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct CompletionMessage<'a> {
    #[serde(rename = "type")]
    message_type: u8,
    invocation_id: &'a str,
    #[serde(skip_serializing_if = "Option::is_none")]
    result: Option<&'a Value>,
    #[serde(skip_serializing_if = "Option::is_none")]
    error: Option<&'a str>,
}

/// An Invocation (type 1). With an id the server answers with a Completion; without one it is
/// fire-and-forget.
pub(crate) fn invocation(invocation_id: Option<&str>, target: &str, arguments: &[Value]) -> String {
    frame(&InvocationMessage { message_type: INVOCATION, invocation_id, target, arguments })
}

/// A StreamInvocation (type 4): the server answers with StreamItems and one Completion.
pub(crate) fn stream_invocation(invocation_id: &str, target: &str, arguments: &[Value]) -> String {
    frame(&InvocationMessage {
        message_type: STREAM_INVOCATION,
        invocation_id: Some(invocation_id),
        target,
        arguments,
    })
}

/// A CancelInvocation (type 5): stops a stream the client no longer reads.
pub(crate) fn cancel_invocation(invocation_id: &str) -> String {
    frame(&IdMessage { message_type: CANCEL_INVOCATION, invocation_id })
}

/// A Ping (type 6).
pub(crate) fn ping() -> String {
    frame(&TypeMessage { message_type: PING })
}

/// A Completion (type 3) the client sends to answer a server-to-client invocation that carries
/// an id.
pub(crate) fn completion(invocation_id: &str, result: Option<&Value>, error: Option<&str>) -> String {
    frame(&CompletionMessage { message_type: COMPLETION, invocation_id, result, error })
}

// ---- Messages the client receives ------------------------------------------------------------

/// A message from the server, reduced to what the client acts on.
#[derive(Clone, Debug, PartialEq)]
pub(crate) enum HubMessage {
    /// The server calls a client method. With an id it expects a Completion back.
    Invocation { invocation_id: Option<String>, target: String, arguments: Vec<Value> },
    StreamItem { invocation_id: String, item: Value },
    /// `result` is `Null` when the method returns nothing.
    Completion { invocation_id: String, result: Value, error: Option<String> },
    Ping,
    /// The server is closing the connection.
    Close,
    /// A message type this client does not use (for example Ack or Sequence).
    Other,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct RawMessage {
    #[serde(rename = "type")]
    message_type: u64,
    #[serde(default)]
    invocation_id: Option<String>,
    #[serde(default)]
    target: Option<String>,
    #[serde(default)]
    arguments: Option<Vec<Value>>,
    #[serde(default)]
    item: Option<Value>,
    #[serde(default)]
    result: Option<Value>,
    #[serde(default)]
    error: Option<String>,
}

/// Decodes one record. `None` when it is not a well-formed hub message.
pub(crate) fn parse_message(record: &str) -> Option<HubMessage> {
    let raw: RawMessage = serde_json::from_str(record).ok()?;
    let message = match raw.message_type {
        t if t == u64::from(INVOCATION) => HubMessage::Invocation {
            invocation_id: raw.invocation_id,
            target: raw.target?,
            arguments: raw.arguments.unwrap_or_default(),
        },
        t if t == u64::from(STREAM_ITEM) => HubMessage::StreamItem {
            invocation_id: raw.invocation_id?,
            item: raw.item.unwrap_or(Value::Null),
        },
        t if t == u64::from(COMPLETION) => HubMessage::Completion {
            invocation_id: raw.invocation_id?,
            result: raw.result.unwrap_or(Value::Null),
            error: raw.error,
        },
        t if t == u64::from(PING) => HubMessage::Ping,
        t if t == u64::from(CLOSE) => HubMessage::Close,
        _ => HubMessage::Other,
    };
    Some(message)
}

/// Collects WebSocket text frames and hands out complete records. A frame may carry several
/// records; a record split across frames waits here for the rest.
#[derive(Default)]
pub(crate) struct RecordBuffer {
    partial: String,
}

impl RecordBuffer {
    pub(crate) fn push(&mut self, text: &str) -> Vec<String> {
        self.partial.push_str(text);
        let Some(last) = self.partial.rfind(RECORD_SEPARATOR) else {
            return Vec::new();
        };
        let rest = self.partial.split_off(last + RECORD_SEPARATOR.len_utf8());
        let complete = std::mem::replace(&mut self.partial, rest);
        complete
            .split(RECORD_SEPARATOR)
            .filter(|record| !record.trim().is_empty())
            .map(str::to_string)
            .collect()
    }
}

// ---- Negotiate -------------------------------------------------------------------------------

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct NegotiateResponse {
    #[serde(default)]
    negotiate_version: u32,
    #[serde(default)]
    connection_id: Option<String>,
    #[serde(default)]
    connection_token: Option<String>,
    #[serde(default)]
    available_transports: Vec<TransportSpec>,
    #[serde(default)]
    error: Option<String>,
    #[serde(default)]
    url: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct TransportSpec {
    transport: String,
    #[serde(default)]
    transfer_formats: Vec<String>,
}

/// Why a negotiate answer cannot be used.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(crate) enum NegotiateError {
    /// The server answered with an `error`.
    Refused,
    /// The server sent the client elsewhere (`url`); not supported.
    Redirected,
    /// WebSockets with text frames is not offered.
    NoWebSockets,
    /// The answer could not be read.
    Malformed,
}

/// Reads the negotiate answer and returns the value for `?id=`: the connection token
/// (negotiate version 1) or the connection id (version 0).
pub(crate) fn parse_negotiate(body: &str) -> Result<String, NegotiateError> {
    let response: NegotiateResponse =
        serde_json::from_str(body).map_err(|_| NegotiateError::Malformed)?;
    if response.error.is_some() {
        return Err(NegotiateError::Refused);
    }
    if response.url.is_some() {
        return Err(NegotiateError::Redirected);
    }
    let websockets = response.available_transports.iter().any(|spec| {
        spec.transport == "WebSockets" && spec.transfer_formats.iter().any(|format| format == "Text")
    });
    if !websockets {
        return Err(NegotiateError::NoWebSockets);
    }
    let id = if response.negotiate_version >= 1 {
        response.connection_token
    } else {
        response.connection_id
    };
    id.filter(|id| !id.is_empty()).ok_or(NegotiateError::Malformed)
}

/// `{path}/negotiate?negotiateVersion=1`, keeping any query the hub path already has.
pub(crate) fn negotiate_url(path: &str) -> String {
    let (base, query) = match path.split_once('?') {
        Some((base, query)) => (base, Some(query)),
        None => (path, None),
    };
    let base = base.trim_end_matches('/');
    match query {
        Some(query) if !query.is_empty() => format!("{base}/negotiate?{query}&negotiateVersion=1"),
        _ => format!("{base}/negotiate?negotiateVersion=1"),
    }
}

/// `{path}?id={token}` (or `&id=` when the path has a query).
pub(crate) fn socket_url(path: &str, token: &str) -> String {
    let separator = if path.contains('?') { '&' } else { '?' };
    format!("{path}{separator}id={}", percent_encode(token))
}

/// The WebSocket origin for a page origin: `wss://` for `https://`, `ws://` for `http://`. Only
/// the browser transport opens sockets (the fake hub needs no origin).
#[cfg(any(target_arch = "wasm32", test))]
pub(crate) fn websocket_origin(origin: &str) -> String {
    if let Some(rest) = origin.strip_prefix("https://") {
        format!("wss://{rest}")
    } else if let Some(rest) = origin.strip_prefix("http://") {
        format!("ws://{rest}")
    } else {
        origin.to_string()
    }
}

/// Percent-encodes everything except the unreserved characters of RFC 3986.
fn percent_encode(text: &str) -> String {
    let mut encoded = String::with_capacity(text.len());
    for byte in text.bytes() {
        if byte.is_ascii_alphanumeric() || matches!(byte, b'-' | b'.' | b'_' | b'~') {
            encoded.push(char::from(byte));
        } else {
            encoded.push_str(&format!("%{byte:02X}"));
        }
    }
    encoded
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn unframe(text: &str) -> Value {
        assert!(text.ends_with(RECORD_SEPARATOR), "a record ends with the separator: {text:?}");
        serde_json::from_str(text.trim_end_matches(RECORD_SEPARATOR)).expect("valid JSON")
    }

    #[test]
    fn records_are_split_on_the_record_separator() {
        let mut buffer = RecordBuffer::default();
        let records = buffer.push("{\"type\":6}\u{1e}{\"type\":1,\"target\":\"a\"}\u{1e}");
        assert_eq!(records, vec!["{\"type\":6}".to_string(), "{\"type\":1,\"target\":\"a\"}".to_string()]);
    }

    #[test]
    fn a_partial_record_waits_for_the_rest() {
        let mut buffer = RecordBuffer::default();
        assert!(buffer.push("{\"type\":").is_empty());
        assert_eq!(buffer.push("6}\u{1e}{\"ty"), vec!["{\"type\":6}".to_string()]);
        assert_eq!(buffer.push("pe\":7}\u{1e}"), vec!["{\"type\":7}".to_string()]);
    }

    #[test]
    fn empty_records_are_skipped() {
        let mut buffer = RecordBuffer::default();
        assert_eq!(buffer.push("\u{1e}\u{1e}{}\u{1e}"), vec!["{}".to_string()]);
    }

    #[test]
    fn handshake_request_asks_for_json_version_1() {
        assert_eq!(unframe(&handshake_request()), json!({"protocol": "json", "version": 1}));
    }

    #[test]
    fn handshake_response_accepts_an_empty_object_and_reports_errors() {
        assert_eq!(parse_handshake_response("{}"), Ok(()));
        assert_eq!(
            parse_handshake_response(r#"{"error":"Requested protocol 'x' is not available."}"#),
            Err("Requested protocol 'x' is not available.".to_string())
        );
        assert!(parse_handshake_response(r#"{"type":6}"#).is_err());
        assert!(parse_handshake_response("not json").is_err());
    }

    #[test]
    fn invocation_is_encoded_with_and_without_an_id() {
        let args = [json!(1), json!("two")];
        assert_eq!(
            unframe(&invocation(Some("7"), "Add", &args)),
            json!({"type": 1, "invocationId": "7", "target": "Add", "arguments": [1, "two"]})
        );
        assert_eq!(
            unframe(&invocation(None, "Notify", &[])),
            json!({"type": 1, "target": "Notify", "arguments": []})
        );
    }

    #[test]
    fn stream_cancel_ping_and_completion_are_encoded() {
        assert_eq!(
            unframe(&stream_invocation("3", "Ticks", &[json!(5)])),
            json!({"type": 4, "invocationId": "3", "target": "Ticks", "arguments": [5]})
        );
        assert_eq!(unframe(&cancel_invocation("3")), json!({"type": 5, "invocationId": "3"}));
        assert_eq!(unframe(&ping()), json!({"type": 6}));
        assert_eq!(
            unframe(&completion("9", Some(&Value::Null), None)),
            json!({"type": 3, "invocationId": "9", "result": null})
        );
        assert_eq!(
            unframe(&completion("9", None, Some("no"))),
            json!({"type": 3, "invocationId": "9", "error": "no"})
        );
    }

    #[test]
    fn incoming_messages_are_decoded() {
        assert_eq!(
            parse_message(r#"{"type":1,"target":"Tick","arguments":[1,{"a":2}]}"#),
            Some(HubMessage::Invocation {
                invocation_id: None,
                target: "Tick".to_string(),
                arguments: vec![json!(1), json!({"a": 2})],
            })
        );
        assert_eq!(
            parse_message(r#"{"type":2,"invocationId":"4","item":"x"}"#),
            Some(HubMessage::StreamItem { invocation_id: "4".to_string(), item: json!("x") })
        );
        assert_eq!(
            parse_message(r#"{"type":3,"invocationId":"4","result":42}"#),
            Some(HubMessage::Completion { invocation_id: "4".to_string(), result: json!(42), error: None })
        );
        assert_eq!(
            parse_message(r#"{"type":3,"invocationId":"4"}"#),
            Some(HubMessage::Completion { invocation_id: "4".to_string(), result: Value::Null, error: None })
        );
        assert_eq!(
            parse_message(r#"{"type":3,"invocationId":"4","error":"boom"}"#),
            Some(HubMessage::Completion {
                invocation_id: "4".to_string(),
                result: Value::Null,
                error: Some("boom".to_string()),
            })
        );
        assert_eq!(parse_message(r#"{"type":6}"#), Some(HubMessage::Ping));
        assert_eq!(parse_message(r#"{"type":7,"error":"bye","allowReconnect":true}"#), Some(HubMessage::Close));
        assert_eq!(parse_message(r#"{"type":8,"sequenceId":1}"#), Some(HubMessage::Other));
    }

    #[test]
    fn malformed_messages_are_rejected() {
        assert_eq!(parse_message("{"), None);
        assert_eq!(parse_message(r#"{"target":"x"}"#), None);
        assert_eq!(parse_message(r#"{"type":1}"#), None);
        assert_eq!(parse_message(r#"{"type":3,"result":1}"#), None);
    }

    #[test]
    fn negotiate_version_1_uses_the_connection_token() {
        let body = r#"{"negotiateVersion":1,"connectionId":"id-1","connectionToken":"tok-1",
            "availableTransports":[{"transport":"WebSockets","transferFormats":["Text","Binary"]}]}"#;
        assert_eq!(parse_negotiate(body), Ok("tok-1".to_string()));
    }

    #[test]
    fn negotiate_version_0_uses_the_connection_id() {
        let body = r#"{"connectionId":"id-0",
            "availableTransports":[{"transport":"WebSockets","transferFormats":["Text"]}]}"#;
        assert_eq!(parse_negotiate(body), Ok("id-0".to_string()));
    }

    #[test]
    fn negotiate_answers_that_cannot_be_used_are_rejected() {
        let no_websockets = r#"{"negotiateVersion":1,"connectionToken":"t",
            "availableTransports":[{"transport":"LongPolling","transferFormats":["Text"]}]}"#;
        assert_eq!(parse_negotiate(no_websockets), Err(NegotiateError::NoWebSockets));
        let binary_only = r#"{"negotiateVersion":1,"connectionToken":"t",
            "availableTransports":[{"transport":"WebSockets","transferFormats":["Binary"]}]}"#;
        assert_eq!(parse_negotiate(binary_only), Err(NegotiateError::NoWebSockets));
        assert_eq!(parse_negotiate(r#"{"error":"Negotiation failed."}"#), Err(NegotiateError::Refused));
        assert_eq!(
            parse_negotiate(r#"{"url":"https://elsewhere","accessToken":"x"}"#),
            Err(NegotiateError::Redirected)
        );
        assert_eq!(parse_negotiate("<html>"), Err(NegotiateError::Malformed));
    }

    #[test]
    fn urls_are_built_from_the_hub_path() {
        assert_eq!(negotiate_url("/hubs/time"), "/hubs/time/negotiate?negotiateVersion=1");
        assert_eq!(negotiate_url("/hubs/time/"), "/hubs/time/negotiate?negotiateVersion=1");
        assert_eq!(negotiate_url("/hubs/time?x=1"), "/hubs/time/negotiate?x=1&negotiateVersion=1");
        assert_eq!(socket_url("/hubs/time", "a+b/c="), "/hubs/time?id=a%2Bb%2Fc%3D");
        assert_eq!(socket_url("/hubs/time?x=1", "t-_.~"), "/hubs/time?x=1&id=t-_.~");
    }

    #[test]
    fn websocket_origin_follows_the_page_scheme() {
        assert_eq!(websocket_origin("https://app.example"), "wss://app.example");
        assert_eq!(websocket_origin("http://localhost:8080"), "ws://localhost:8080");
    }
}
