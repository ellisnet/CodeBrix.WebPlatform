using System;

namespace WorkSite.BuildFrontend;

/// <summary>
/// A step failed in a way the user can act on; the message says what to do.
/// </summary>
public sealed class BuilderException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">What failed and what to do about it.</param>
    public BuilderException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with its cause.
    /// </summary>
    /// <param name="message">What failed and what to do about it.</param>
    /// <param name="innerException">The cause.</param>
    public BuilderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
