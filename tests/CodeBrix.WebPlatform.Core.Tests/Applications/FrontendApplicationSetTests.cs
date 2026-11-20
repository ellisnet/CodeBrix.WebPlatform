using System;
using System.Linq;
using CodeBrix.WebPlatform.Core.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Core.Tests.Applications;

public class FrontendApplicationSetTests
{
    [Fact]
    public void SingleDefault_is_one_application_at_the_site_root()
    {
        //Act
        var set = FrontendApplicationSet.SingleDefault;

        //Assert
        set.Should().HaveCount(1);
        set.Default.Should().BeSameAs(FrontendApplication.Default);
        set.Named.Should().BeEmpty();
    }

    [Fact]
    public void FromNames_keeps_the_order_and_knows_the_default_and_the_named_ones()
    {
        //Act
        var set = FrontendApplicationSet.FromNames(new[] { null, "sales", "admin" });

        //Assert
        set.Select(a => a.Name).Should().Equal("app", "sales", "admin");
        set[0].IsDefault.Should().BeTrue();
        set.Default.Name.Should().Be("app");
        set.Named.Select(a => a.Name).Should().Equal("sales", "admin");
    }

    [Fact]
    public void a_set_in_which_every_application_has_a_name_has_no_default()
    {
        //Act
        var set = FrontendApplicationSet.FromNames(new[] { "sales", "admin" });

        //Assert
        set.Default.Should().BeNull();
        set.Named.Should().HaveCount(2);
    }

    [Fact]
    public void a_set_refuses_two_applications_without_a_name()
    {
        //Act
        Action act = () => FrontendApplicationSet.FromNames(new[] { null, "sales", "" });

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*More than one frontend application has no name*");
    }

    [Fact]
    public void a_set_refuses_a_name_used_twice()
    {
        //Act
        Action act = () => FrontendApplicationSet.FromNames(new[] { "sales", "admin", "sales" });

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*'sales' is used more than once*");
    }

    [Fact]
    public void a_set_refuses_to_be_empty()
    {
        //Act
        Action act = () => new FrontendApplicationSet(Array.Empty<FrontendApplication>());

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*at least one*");
    }

    [Fact]
    public void a_set_refuses_null_and_a_null_application()
    {
        //Act
        Action none = () => new FrontendApplicationSet(null);
        Action hole = () => new FrontendApplicationSet(new[] { FrontendApplication.Default, null });

        //Assert
        none.Should().Throw<ArgumentNullException>();
        hole.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromNames_passes_on_what_is_wrong_with_a_name()
    {
        //Act
        Action act = () => FrontendApplicationSet.FromNames(new[] { "Sales" });

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*capital letters*");
    }

    [Theory]
    [InlineData(null, "app")]
    [InlineData("", "app")]
    [InlineData("app", "app")]
    [InlineData("admin", "admin")]
    public void Find_finds_an_application_by_name_and_the_default_one_for_no_name(string name, string expected)
    {
        //Arrange
        var set = FrontendApplicationSet.FromNames(new[] { null, "admin" });

        //Act
        var found = set.Find(name);

        //Assert
        found.Name.Should().Be(expected);
    }

    [Fact]
    public void Find_gives_null_for_a_name_the_set_does_not_have()
    {
        //Arrange
        var set = FrontendApplicationSet.FromNames(new[] { "sales" });

        //Assert
        set.Find("admin").Should().BeNull();
        set.Find(null).Should().BeNull();
    }
}
