using System;

namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>
/// A frontend build step failed in a way the developer can act on. The message says what failed
/// and what to do about it; a program's output, shortened, is part of it where that helps.
/// </summary>
public sealed class FrontendBuildException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">What failed and what to do about it.</param>
    public FrontendBuildException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with its cause.
    /// </summary>
    /// <param name="message">What failed and what to do about it.</param>
    /// <param name="innerException">The cause, for example the
    /// <c>BackendMapException</c> that lists every problem in the backend's contract.</param>
    public FrontendBuildException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
