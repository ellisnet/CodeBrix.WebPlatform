using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>
/// A SignalR connection for tests, speaking the JSON hub protocol over the test server's
/// WebSocket, the way a browser does: an <c>Origin</c> header, the session cookies, no bearer
/// token. It goes through the real pipeline (origin check, authentication, the hub).
/// </summary>
public sealed class HubTestConnection : IAsyncDisposable
{
    private const char RecordSeparator = '\u001e';

    private readonly WebSocket _socket;
    private readonly CancellationTokenSource _closing = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _completions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<JsonElement>> _streamItems = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Channel<JsonElement[]>> _messages = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource<JsonElement> _handshake = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _reader;
    private int _nextId;

    private HubTestConnection(WebSocket socket)
    {
        _socket = socket;
        _reader = Task.Run(ReadLoopAsync);
    }

    /// <summary>
    /// Opens a connection to a hub and completes the protocol handshake: it returns only after the
    /// server has answered the handshake. SignalR answers it just BEFORE it adds the connection to
    /// the hub's connections, so a message sent to all connections at once may still miss this
    /// one; a hub method call made after this returns is always handled after the adding.
    /// </summary>
    /// <param name="factory">The application under test.</param>
    /// <param name="path">The hub's path, such as <c>/hubs/clock</c>.</param>
    /// <param name="signedInAs">The client whose session cookies are sent; null for a visitor.</param>
    /// <param name="origin">The <c>Origin</c> header; null to send none.</param>
    /// <returns>The open connection.</returns>
    /// <exception cref="HubTestException">The server refused the handshake, or closed the connection before answering it.</exception>
    public static async Task<HubTestConnection> ConnectAsync(WorkSiteApiFactory factory, string path, ApiClient signedInAs = null,
        string origin = "https://localhost")
    {
        var client = factory.Server.CreateWebSocketClient();
        var cookies = signedInAs?.CookieHeader(path);
        client.ConfigureRequest = request =>
        {
            if (origin != null)
            {
                request.Headers.Origin = origin;
            }

            if (cookies != null)
            {
                request.Headers.Cookie = cookies;
            }
        };
        var socket = await client.ConnectAsync(new Uri("wss://localhost" + path), TestContext.Current.CancellationToken);
        var connection = new HubTestConnection(socket);
        try
        {
            await connection.SendTextAsync("{\"protocol\":\"json\",\"version\":1}");
            var answer = await connection._handshake.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            if (answer.TryGetProperty("error", out var error))
            {
                throw new HubTestException(error.GetString());
            }
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return connection;
    }

    /// <summary>Calls a hub method and waits for its answer.</summary>
    /// <param name="target">The method name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The result (an undefined element when the method returns nothing).</returns>
    /// <exception cref="HubTestException">The method answered with an error.</exception>
    public async Task<JsonElement> InvokeAsync(string target, params object[] arguments)
    {
        var id = Interlocked.Increment(ref _nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _completions[id] = completion;
        await SendAsync(new { type = 1, invocationId = id, target, arguments });
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
    }

    /// <summary>Starts a streaming hub method.</summary>
    /// <param name="target">The method name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The stream's id, for <see cref="ItemsOf"/>, <see cref="CompletionOfAsync"/> and <see cref="CancelStreamAsync"/>.</returns>
    public async Task<string> StartStreamAsync(string target, params object[] arguments)
    {
        var id = Interlocked.Increment(ref _nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _completions[id] = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _streamItems[id] = new List<JsonElement>();
        await SendAsync(new { type = 4, invocationId = id, target, arguments });
        return id;
    }

    /// <summary>The items a stream has delivered so far.</summary>
    /// <param name="streamId">The stream.</param>
    /// <returns>A copy of the items.</returns>
    public IReadOnlyList<JsonElement> ItemsOf(string streamId)
    {
        var items = _streamItems[streamId];
        lock (items)
        {
            return items.ToList();
        }
    }

    /// <summary>Waits for a stream to end.</summary>
    /// <param name="streamId">The stream.</param>
    /// <returns>A task that completes when the stream completes, and throws when it ends in error.</returns>
    public Task CompletionOfAsync(string streamId)
        => _completions[streamId].Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);

    /// <summary>Cancels a stream, as dropping it does in the frontend.</summary>
    /// <param name="streamId">The stream.</param>
    /// <returns>A task that completes when the cancellation is sent.</returns>
    public Task CancelStreamAsync(string streamId) => SendAsync(new { type = 5, invocationId = streamId });

    /// <summary>Waits for the next message the hub sends with a given name.</summary>
    /// <param name="target">The message name, such as <c>Tick</c>.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <returns>The message's arguments.</returns>
    /// <exception cref="TimeoutException">No such message arrived in time.</exception>
    public async Task<JsonElement[]> NextAsync(string target, TimeSpan timeout)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            return await Channel(target).Reader.ReadAsync(limit.Token);
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"No '{target}' message arrived within {timeout.TotalSeconds:0.#} s.");
        }
    }

    /// <summary>True when no message with a given name arrives within a period.</summary>
    /// <param name="target">The message name.</param>
    /// <param name="period">How long to watch.</param>
    /// <returns>Whether the period passed in silence.</returns>
    public async Task<bool> StaysSilentAsync(string target, TimeSpan period)
    {
        try
        {
            await NextAsync(target, period);
            return false;
        }
        catch (TimeoutException)
        {
            return true;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _closing.CancelAsync();
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, limit.Token);
            }
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            //The server may already have closed its side.
        }

        try
        {
            await _reader.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception e) when (e is TimeoutException or OperationCanceledException)
        {
            //The reader ends with the socket.
        }

        _socket.Dispose();
        _closing.Dispose();
    }

    private Channel<JsonElement[]> Channel(string target)
        => _messages.GetOrAdd(target, _ => System.Threading.Channels.Channel.CreateUnbounded<JsonElement[]>());

    private Task SendAsync(object message) => SendTextAsync(JsonSerializer.Serialize(message, JsonSerializerOptions.Web));

    private Task SendTextAsync(string json)
        => _socket.SendAsync(Encoding.UTF8.GetBytes(json + RecordSeparator), WebSocketMessageType.Text, true,
            TestContext.Current.CancellationToken);

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[16 * 1024];
        var pending = new StringBuilder();
        try
        {
            while (!_closing.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(buffer, _closing.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                pending.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                var text = pending.ToString();
                int end;
                while ((end = text.IndexOf(RecordSeparator, StringComparison.Ordinal)) >= 0)
                {
                    Dispatch(text.Substring(0, end));
                    text = text.Substring(end + 1);
                }

                pending.Clear().Append(text);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException or ObjectDisposedException or InvalidOperationException)
        {
            //The connection is going away.
        }

        _handshake.TrySetException(new HubTestException("The connection closed before the handshake was answered."));
        foreach (var completion in _completions.Values)
        {
            completion.TrySetException(new HubTestException("The connection closed before the answer arrived."));
        }
    }

    /// <summary>
    /// SignalR wraps a <c>HubException</c>'s message as "An unexpected error occurred invoking
    /// 'X' on the server. HubException: the message". The frontend's SignalR client keeps what
    /// follows the marker, and so does this.
    /// </summary>
    internal static string Sentence(string error)
    {
        const string marker = " HubException: ";
        var at = error == null ? -1 : error.IndexOf(marker, StringComparison.Ordinal);
        return at < 0 ? error : error.Substring(at + marker.Length).Trim();
    }

    private void Dispatch(string json)
    {
        using var document = JsonDocument.Parse(json);
        var message = document.RootElement.Clone();
        if (!message.TryGetProperty("type", out var typeElement))
        {
            //The handshake answer: {} on success, {"error":"..."} on refusal.
            _handshake.TrySetResult(message);
            return;
        }

        var id = message.TryGetProperty("invocationId", out var idElement) ? idElement.GetString() : null;
        switch (typeElement.GetInt32())
        {
            case 1:
                var arguments = message.GetProperty("arguments").EnumerateArray().ToArray();
                Channel(message.GetProperty("target").GetString()).Writer.TryWrite(arguments);
                break;
            case 2:
                if (id != null && _streamItems.TryGetValue(id, out var items))
                {
                    lock (items)
                    {
                        items.Add(message.GetProperty("item"));
                    }
                }

                break;
            case 3:
                if (id != null && _completions.TryGetValue(id, out var completion))
                {
                    if (message.TryGetProperty("error", out var error))
                    {
                        completion.TrySetException(new HubTestException(Sentence(error.GetString())));
                    }
                    else
                    {
                        completion.TrySetResult(message.TryGetProperty("result", out var result) ? result : default);
                    }
                }

                break;
            default:
                //Pings (6) and close messages (7) need no answer here.
                break;
        }
    }
}
