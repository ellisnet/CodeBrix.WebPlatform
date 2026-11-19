using System.Linq;
using SilverAssertions;
using Xunit;

namespace WorkSite.BuildFrontend.Tests;

public class EnvFileTests
{
    private const string Example = "# comment\nWORKSITE_DB_PASSWORD=\n\nWORKSITE_JWT_SIGNING_KEY=\nWORKSITE_CERT_FILE=\nWORKSITE_CERT_PASSWORD=\nWORKSITE_HTTPS_PORT=8443\nWORKSITE_DEMO_ENABLED=true\nWORKSITE_DEMO_USERNAME=demo\nWORKSITE_DEMO_PASSWORD=\n";

    [Fact]
    public void Parse_reads_key_value_lines_and_skips_comments_and_blanks()
    {
        //Act
        var values = EnvFile.Parse("# a comment\n\nA=1\r\n B = two words \nC=\"quoted value\"\nD='single'\nnot a pair\n=novalue\nE=a=b\n");

        //Assert
        values["A"].Should().Be("1");
        values["B"].Should().Be("two words");
        values["C"].Should().Be("quoted value");
        values["D"].Should().Be("single");
        values["E"].Should().Be("a=b");
        values.Should().HaveCount(5);
        EnvFile.Parse(null).Should().BeEmpty();
    }

    [Fact]
    public void CreateFromExample_fills_every_empty_secret_and_keeps_the_rest()
    {
        //Act
        var text = EnvFile.CreateFromExample(Example, "/home/someone/.aspnet/https/worksite-dev.pfx");
        var values = EnvFile.Parse(text);

        //Assert
        text.Should().StartWith("# comment\n");
        values["WORKSITE_DB_PASSWORD"].Should().HaveLength(24);
        values["WORKSITE_JWT_SIGNING_KEY"].Should().HaveLength(48);
        values["WORKSITE_CERT_PASSWORD"].Should().HaveLength(24);
        values["WORKSITE_DEMO_PASSWORD"].Should().HaveLength(12);
        values["WORKSITE_CERT_FILE"].Should().Be("/home/someone/.aspnet/https/worksite-dev.pfx");
        values["WORKSITE_HTTPS_PORT"].Should().Be("8443");
        values["WORKSITE_DEMO_USERNAME"].Should().Be("demo");
        EnvFile.MissingKeys(values).Should().BeEmpty();
    }

    [Fact]
    public void CreateFromExample_makes_different_secrets_every_time()
    {
        //Act
        var first = EnvFile.Parse(EnvFile.CreateFromExample(Example, "x"));
        var second = EnvFile.Parse(EnvFile.CreateFromExample(Example, "x"));

        //Assert
        first["WORKSITE_JWT_SIGNING_KEY"].Should().NotBe(second["WORKSITE_JWT_SIGNING_KEY"]);
        first["WORKSITE_DB_PASSWORD"].Should().NotBe(first["WORKSITE_CERT_PASSWORD"]);
    }

    [Fact]
    public void RandomSecret_uses_only_letters_and_digits()
    {
        //Act
        var secret = EnvFile.RandomSecret(200);

        //Assert
        secret.Should().HaveLength(200);
        secret.All(char.IsAsciiLetterOrDigit).Should().BeTrue();
    }

    [Fact]
    public void MissingKeys_names_required_settings_that_are_absent_or_empty()
    {
        //Arrange
        var values = EnvFile.Parse("WORKSITE_DB_PASSWORD=x\nWORKSITE_JWT_SIGNING_KEY=\nWORKSITE_CERT_FILE=/a.pfx\n");

        //Assert
        EnvFile.MissingKeys(values).Should().BeEquivalentTo(new[] { "WORKSITE_JWT_SIGNING_KEY", "WORKSITE_CERT_PASSWORD" });
    }

    [Fact]
    public void Load_gives_nothing_for_a_missing_file()
        => EnvFile.Load("/no/such/folder/.env").Should().BeEmpty();
}
