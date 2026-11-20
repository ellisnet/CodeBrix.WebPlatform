using System;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.Core.Crates;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Core.Tests.Crates;

public class CrateBundlesTests : IDisposable
{
    public CrateBundlesTests()
    {
        CrateBundles.Clear();
    }

    public void Dispose()
    {
        CrateBundles.Clear();
    }

    [Fact]
    public void nothing_is_registered_until_a_program_registers_it()
    {
        //Assert
        CrateBundles.Registered.Should().BeEmpty();
    }

    [Fact]
    public void Register_keeps_the_bundles_in_the_order_they_were_registered()
    {
        //Act
        CrateBundles.Register(Bundle("second-letter-b", "P.One", "1"));
        CrateBundles.Register(Bundle("a-first-letter", "P.Two", "1"));

        //Assert
        CrateBundles.Registered.Select(b => b.Name).Should().Equal("second-letter-b", "a-first-letter");
    }

    [Fact]
    public void Register_does_nothing_when_the_same_bundle_is_registered_again()
    {
        //Act
        CrateBundles.Register(Bundle("graphics", "P.One", "1"));
        CrateBundles.Register(Bundle("graphics", "P.One", "1"));

        //Assert
        CrateBundles.Registered.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("P.Other", "1")]
    [InlineData("P.One", "2")]
    public void Register_refuses_a_second_bundle_of_the_same_name_from_elsewhere(string package, string version)
    {
        //Arrange
        CrateBundles.Register(Bundle("graphics", "P.One", "1"));

        //Act
        Action act = () => CrateBundles.Register(Bundle("graphics", package, version));

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"*'graphics'*P.One 1*{package} {version}*");
        CrateBundles.Registered.Should().HaveCount(1);
    }

    [Fact]
    public void Register_refuses_null()
    {
        //Act
        Action act = () => CrateBundles.Register(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Registered_is_a_copy()
    {
        //Arrange
        CrateBundles.Register(Bundle("one", "P.One", "1"));
        var before = CrateBundles.Registered;

        //Act
        CrateBundles.Register(Bundle("two", "P.Two", "1"));

        //Assert
        before.Should().HaveCount(1);
        CrateBundles.Registered.Should().HaveCount(2);
    }

    private static CrateBundle Bundle(string name, string package, string version)
        => new CrateBundle(name, package, version, () => Stream.Null);
}
