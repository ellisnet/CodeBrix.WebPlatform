using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CodeBrix.WebPlatform.Backend.Tests.Helpers;

/// <summary>Keeps every message logged under the library's category, for the tests to read.</summary>
public sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries.ToArray();

    public IEnumerable<string> Messages(LogLevel level) => Entries.Where(e => e.Level == level).Select(e => e.Message);

    public ILogger CreateLogger(string categoryName)
        => categoryName == PlatformBackendApplicationExtensions.LogCategory ? new ListLogger(this) : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class ListLogger : ILogger
    {
        private readonly ListLoggerProvider _owner;

        public ListLogger(ListLoggerProvider owner)
        {
            _owner = owner;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => _owner._entries.Enqueue((logLevel, formatter(state, exception)));
    }

    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
        }
    }
}
