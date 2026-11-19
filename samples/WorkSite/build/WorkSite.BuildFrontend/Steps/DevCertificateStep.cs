using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace WorkSite.BuildFrontend.Steps;

/// <summary>
/// Exports the machine's ASP.NET Core HTTPS development certificate
/// (<c>dotnet dev-certs https</c>) to a password-protected .pfx file outside the repository, for
/// the web container to serve HTTPS with. It never changes what the machine or its browsers
/// trust; trusting the certificate is the user's own step (<c>dotnet dev-certs https --trust</c>).
/// </summary>
public sealed class DevCertificateStep
{
    private readonly ToolRunner _runner;

    /// <summary>
    /// Creates the step.
    /// </summary>
    /// <param name="runner">Runs <c>dotnet dev-certs</c>.</param>
    public DevCertificateStep(ToolRunner runner)
    {
        _runner = runner;
    }

    /// <summary>
    /// Exports the development certificate with its private key.
    /// </summary>
    /// <param name="file">The .pfx file to write (its folder is created).</param>
    /// <param name="password">The password protecting the file.</param>
    /// <param name="cancellationToken">Stops the export.</param>
    /// <exception cref="BuilderException">The export failed (for example no development
    /// certificate exists yet).</exception>
    public async Task ExportAsync(string file, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
        if (File.Exists(file))
        {
            File.Delete(file);
        }

        var result = await _runner.RunAsync(
            "dotnet",
            new[] { "dev-certs", "https", "--export-path", file, "--password", password, "--format", "Pfx" },
            echo: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || !File.Exists(file))
        {
            throw new BuilderException(
                "The HTTPS development certificate could not be exported:" + Environment.NewLine + ToolRunner.Tail(result.Output, 10) + Environment.NewLine
                + "Create and trust one with: dotnet dev-certs https --trust");
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // The web container runs as an unprivileged user that is not this user, so the file
            // must be world-readable to be usable through the bind mount. The private key inside
            // is encrypted with the password.
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }
    }

    /// <summary>
    /// Reads an exported certificate file and returns what is needed to judge it.
    /// </summary>
    /// <param name="file">The .pfx file.</param>
    /// <param name="password">Its password.</param>
    /// <returns>The certificate's SHA-256 thumbprint (upper-case hex), subject and expiry.</returns>
    /// <exception cref="BuilderException">The file is missing, or the password is wrong.</exception>
    public static (string Thumbprint, string Subject, DateTime NotAfter) Inspect(string file, string password)
    {
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            throw new BuilderException($"The certificate file '{file}' does not exist. Run `WorkSite.BuildFrontend cert`.");
        }

        try
        {
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(file, password);
            return (certificate.GetCertHashString(HashAlgorithmName.SHA256), certificate.Subject, certificate.NotAfter);
        }
        catch (CryptographicException exception)
        {
            throw new BuilderException($"The certificate file '{file}' could not be opened with the password in .env. Run `WorkSite.BuildFrontend cert` to export it again.", exception);
        }
    }
}
