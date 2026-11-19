using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SilverAssertions;
using WorkSite.BuildFrontend.Steps;
using WorkSite.BuildFrontend.Tests.Helpers;
using Xunit;

namespace WorkSite.BuildFrontend.Tests.Steps;

public class DevCertificateStepTests
{
    private static string WriteCertificate(TempFolder folder, string password, out string thumbprint)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        var path = Path.Combine(folder.Path, "test.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
        thumbprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        return path;
    }

    [Fact]
    public void Inspect_reads_the_thumbprint_subject_and_expiry()
    {
        //Arrange
        using var folder = new TempFolder();
        var file = WriteCertificate(folder, "secret", out var expected);

        //Act
        var (thumbprint, subject, notAfter) = DevCertificateStep.Inspect(file, "secret");

        //Assert
        thumbprint.Should().Be(expected);
        subject.Should().Be("CN=localhost");
        notAfter.Should().BeAfter(DateTime.Now);
    }

    [Fact]
    public void Inspect_fails_clearly_for_a_wrong_password_or_a_missing_file()
    {
        //Arrange
        using var folder = new TempFolder();
        var file = WriteCertificate(folder, "secret", out _);
        Action wrongPassword = () => DevCertificateStep.Inspect(file, "not the password");
        Action missing = () => DevCertificateStep.Inspect(Path.Combine(folder.Path, "absent.pfx"), "secret");

        //Assert
        wrongPassword.Should().Throw<BuilderException>().WithMessage("*WorkSite.BuildFrontend cert*");
        missing.Should().Throw<BuilderException>().WithMessage("*does not exist*");
    }
}
