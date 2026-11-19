using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WorkSite.BuildFrontend;

/// <summary>
/// Starts the external programs WorkSite's own steps drive (dotnet, docker) and passes their
/// output through. The frontend build itself runs through CodeBrix.WebPlatform.FrontendBuilder.
/// </summary>
public sealed class ToolRunner
{
    private readonly TextWriter _output;

    /// <summary>
    /// Creates a runner.
    /// </summary>
    /// <param name="output">Where program output and progress lines go.</param>
    public ToolRunner(TextWriter output)
    {
        _output = output ?? TextWriter.Null;
    }

    /// <summary>Where program output and progress lines go.</summary>
    public TextWriter Output => _output;

    /// <summary>
    /// Runs a program to completion.
    /// </summary>
    /// <param name="fileName">The program (a name on PATH or a full path).</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="workingDirectory">Its working directory, or <c>null</c>.</param>
    /// <param name="environment">Environment variables to set, or <c>null</c>.</param>
    /// <param name="echo">Pass the program's output through to <see cref="Output"/>.</param>
    /// <param name="cancellationToken">Stops the program (the whole process tree) when cancelled.</param>
    /// <returns>The exit code and the captured output; exit code -1 with <c>NotFound</c> true
    /// when the program could not be started.</returns>
    public async Task<ToolResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory = null,
        IReadOnlyDictionary<string, string> environment = null,
        bool echo = true,
        CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        if (environment != null)
        {
            foreach (var pair in environment)
            {
                start.Environment[pair.Key] = pair.Value;
            }
        }

        var captured = new StringBuilder();
        var gate = new object();
        void Handle(string line)
        {
            if (line == null)
            {
                return;
            }

            lock (gate)
            {
                captured.Append(line).Append('\n');
                if (echo)
                {
                    _output.WriteLine("    " + line);
                }
            }
        }

        using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => Handle(e.Data);
        process.ErrorDataReceived += (_, e) => Handle(e.Data);
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or FileNotFoundException)
        {
            return new ToolResult { ExitCode = -1, NotFound = true, Output = exception.Message };
        }

        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            throw;
        }

        process.WaitForExit();
        return new ToolResult { ExitCode = process.ExitCode, Output = captured.ToString() };
    }

    /// <summary>
    /// Writes a progress line.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Say(string message) => _output.WriteLine(message);

    /// <summary>
    /// The last <paramref name="lines"/> non-blank lines of a program's output, indented and cut
    /// at 400 characters, for an error message.
    /// </summary>
    /// <param name="text">The output; <c>null</c> is treated as empty.</param>
    /// <param name="lines">How many lines to keep.</param>
    internal static string Tail(string text, int lines)
    {
        var all = (text ?? string.Empty).Split('\n').Where(l => l.Trim().Length > 0).Select(l => l.Length > 400 ? l.Substring(0, 400) + " ..." : l).ToList();
        return string.Join(Environment.NewLine, all.Skip(Math.Max(0, all.Count - lines)).Select(l => "    " + l));
    }
}
