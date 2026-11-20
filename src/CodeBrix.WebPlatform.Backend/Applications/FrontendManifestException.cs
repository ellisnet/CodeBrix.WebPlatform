using System;

namespace CodeBrix.WebPlatform.Backend.Applications;

/// <summary>
/// The frontend manifest in the web root cannot be used. The message names the file, says what
/// is wrong with it, and says that the file is build output that building the frontend writes
/// anew.
/// </summary>
public sealed class FrontendManifestException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="filePath">The manifest's path (or the name it was read under).</param>
    /// <param name="problem">What is wrong, as a sentence.</param>
    /// <param name="innerException">The exception that caused it, or null.</param>
    public FrontendManifestException(string filePath, string problem, Exception innerException = null)
        : base(Describe(filePath, problem), innerException)
    {
        FilePath = filePath;
        Problem = problem;
    }

    /// <summary>The manifest's path (or the name it was read under).</summary>
    public string FilePath { get; }

    /// <summary>What is wrong, without the rest of the message.</summary>
    public string Problem { get; }

    private static string Describe(string filePath, string problem)
        => $"The frontend manifest {filePath} cannot be used: {problem} "
           + "This file is build output that the frontend build writes into the web root beside the frontend applications: "
           + "build the frontend again (with the same release of the platform as this backend) to write it anew.";
}
