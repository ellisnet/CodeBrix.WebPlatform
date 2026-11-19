using System;
using SilverAssertions;
using WorkSite.DataAccess.Schema;
using Xunit;

namespace WorkSite.DataAccess.Tests.Schema;

public class MigrationScriptTests
{
    [Fact]
    public void LoadEmbedded_returns_the_scripts_in_version_order()
    {
        //Act
        var scripts = MigrationScript.LoadEmbedded();

        //Assert
        scripts.Should().NotBeEmpty();
        scripts[0].Version.Should().Be(1);
        scripts[0].Name.Should().Be("initial");
        scripts[0].Sql.Should().Contain("CREATE TABLE users");
        scripts.Should().BeInAscendingOrder(s => s.Version);
    }

    [Fact]
    public void ParseVersion_reads_the_leading_number()
        => MigrationScript.ParseVersion("0012_add_things.sql").Should().Be(12);

    [Fact]
    public void ParseName_drops_the_version_and_extension()
        => MigrationScript.ParseName("0012_add_things.sql").Should().Be("add_things");

    [Fact]
    public void ParseVersion_rejects_a_name_without_a_version()
    {
        //Act
        Action act = () => MigrationScript.ParseVersion("initial.sql");

        //Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_rejects_a_version_below_one()
    {
        //Act
        Action act = () => _ = new MigrationScript(0, "zero", "SELECT 1");

        //Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
