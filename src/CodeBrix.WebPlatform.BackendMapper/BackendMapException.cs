using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeBrix.WebPlatform.BackendMapper;

/// <summary>
/// Thrown when a backend cannot be mapped. It carries every problem that was found, not just the
/// first, so that all of them can be fixed in one pass.
/// </summary>
public sealed class BackendMapException : Exception
{
    /// <summary>Creates the exception from the problems that were found.</summary>
    /// <param name="problems">Every problem, in the order it was found.</param>
    public BackendMapException(IEnumerable<MappingProblem> problems)
        : this(problems == null ? new List<MappingProblem>() : problems.ToList())
    {
    }

    /// <summary>Creates the exception for a failure that is not about one member of the
    /// backend, such as an assembly that cannot be read.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The underlying failure, if any.</param>
    public BackendMapException(string message, Exception innerException = null)
        : base(message, innerException)
    {
        Problems = Array.Empty<MappingProblem>();
    }

    private BackendMapException(List<MappingProblem> problems)
        : base(Describe(problems))
    {
        Problems = problems;
    }

    /// <summary>Every problem that was found. Empty when the failure was not about a member of
    /// the backend.</summary>
    public IReadOnlyList<MappingProblem> Problems { get; }

    private static string Describe(List<MappingProblem> problems)
    {
        var text = new StringBuilder();
        text.Append(problems.Count == 1
            ? "The backend cannot be mapped: 1 problem."
            : $"The backend cannot be mapped: {problems.Count} problems.");
        foreach (var problem in problems)
        {
            text.Append(Environment.NewLine).Append("  - ").Append(problem);
        }

        return text.ToString();
    }
}
