using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.WebPlatform.FrontendBuilder.Internal;

/// <summary>
/// Starts the external programs a frontend build drives (dotnet, dx, tailwindcss, wasm-opt),
/// captures their output and, when asked, passes it through to the caller's writer.
/// </summary>
internal sealed class ProcessRunner
{
    private readonly TextWriter _output;

    /// <summary>
    /// Creates a runner.
    /// </summary>
    /// <param name="output">Where program output and progress lines go; <c>null</c> for nowhere.</param>
    public ProcessRunner(TextWriter output)
    {
        _output = output ?? TextWriter.Null;
    }

    /// <summary>
    /// Runs a program to completion.
    /// </summary>
    /// <param name="fileName">The program (a name on PATH or a full path).</param>
    /// <param name="arguments">Its arguments, one per item.</param>
    /// <param name="workingDirectory">Its working directory, or <c>null</c> for the current one.</param>
    /// <param name="environment">Environment variables to set, or <c>null</c>.</param>
    /// <param name="echo">Pass the program's output through to the writer.</param>
    /// <param name="cancellationToken">Stops the program (the whole process tree) when cancelled.</param>
    /// <returns>The exit code and the captured output; exit code -1 with <c>NotFound</c> true
    /// when the program could not be started.</returns>
    public async Task<ProcessResult> RunAsync(
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
            return new ProcessResult { ExitCode = -1, NotFound = true, Output = exception.Message };
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
        return new ProcessResult { ExitCode = process.ExitCode, Output = captured.ToString() };
    }

    /// <summary>
    /// Writes a progress line.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Say(string message) => _output.WriteLine(message);
}
