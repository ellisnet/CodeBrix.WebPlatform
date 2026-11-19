namespace CodeBrix.WebPlatform.BackendMapper;

/// <summary>
/// One thing about a backend that stops its client code from being written: something the
/// contract does not declare, or something the mapper does not support.
/// </summary>
public sealed class MappingProblem
{
    /// <summary>Creates a problem.</summary>
    /// <param name="location">Where the problem is, such as <c>ClientsController.Update</c>,
    /// <c>ClockHub.Tick</c> or <c>TimeEntry.Description</c>.</param>
    /// <param name="message">What is missing or unsupported, and what to do about it.</param>
    public MappingProblem(string location, string message)
    {
        Location = location;
        Message = message;
    }

    /// <summary>Where the problem is: the controller, hub or contract class and its member.</summary>
    public string Location { get; }

    /// <summary>What is missing or unsupported, and what to do about it.</summary>
    public string Message { get; }

    /// <summary>The location and the message on one line.</summary>
    /// <returns><c>Location: Message</c>.</returns>
    public override string ToString() => $"{Location}: {Message}";
}
