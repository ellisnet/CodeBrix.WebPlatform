using System;
using CodeBrix.WebPlatform.Core.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Core.Tests.Applications;

public class FrontendApplicationTests
{
    [Fact]
    public void the_default_application_is_named_app_and_lives_at_the_site_root()
    {
        //Act
        var application = FrontendApplication.Default;

        //Assert
        application.Name.Should().Be("app");
        application.IsDefault.Should().BeTrue();
        application.BasePath.Should().BeEmpty();
        application.Folder.Should().BeEmpty();
        application.ToString().Should().Be("app (site root)");
    }

    [Fact]
    public void Named_gives_an_application_served_below_its_name()
    {
        //Act
        var application = FrontendApplication.Named("sales-2");

        //Assert
        application.Name.Should().Be("sales-2");
        application.IsDefault.Should().BeFalse();
        application.BasePath.Should().Be("sales-2");
        application.Folder.Should().Be("sales-2");
        application.ToString().Should().Be("sales-2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FromName_gives_the_default_application_for_no_name(string name)
    {
        //Act
        var application = FrontendApplication.FromName(name);

        //Assert
        application.Should().BeSameAs(FrontendApplication.Default);
    }

    [Fact]
    public void FromName_gives_a_named_application_for_a_name()
    {
        //Act
        var application = FrontendApplication.FromName("admin");

        //Assert
        application.Should().Be(FrontendApplication.Named("admin"));
    }

    [Theory]
    [InlineData("sales")]
    [InlineData("a")]
    [InlineData("back-office")]
    [InlineData("v2")]
    [InlineData("apps")]
    [InlineData("api-docs")]
    public void DescribeNameProblem_accepts_a_good_name(string name)
    {
        //Act
        var problem = FrontendApplication.DescribeNameProblem(name);

        //Assert
        problem.Should().BeNull();
    }

    [Theory]
    [InlineData("", "cannot be empty")]
    [InlineData(null, "cannot be empty")]
    [InlineData("Sales", "capital letters")]
    [InlineData("API", "capital letters")]
    [InlineData("my_app", "lowercase letters (a-z), digits and hyphens")]
    [InlineData("my app", "lowercase letters (a-z), digits and hyphens")]
    [InlineData("café", "lowercase letters (a-z), digits and hyphens")]
    [InlineData("a/b", "lowercase letters (a-z), digits and hyphens")]
    [InlineData("2go", "must start with a letter")]
    [InlineData("-sales", "must start with a letter")]
    [InlineData("sales-", "must not end with a hyphen")]
    [InlineData("app", "belongs to the application that is given no name")]
    [InlineData("api", "taken by the platform")]
    [InlineData("assets", "taken by the platform")]
    [InlineData("a12345678901234567890123456789012345678901", "longer than 40")]
    public void DescribeNameProblem_says_what_is_wrong(string name, string expected)
    {
        //Act
        var problem = FrontendApplication.DescribeNameProblem(name);

        //Assert
        problem.Should().Contain(expected);
    }

    [Fact]
    public void DescribeNameProblem_suggests_the_lowercase_form_of_a_name_with_capitals()
    {
        //Act
        var problem = FrontendApplication.DescribeNameProblem("Sales");

        //Assert
        problem.Should().Contain("'sales'");
    }

    [Fact]
    public void Named_refuses_a_bad_name_with_the_reason()
    {
        //Act
        Action act = () => FrontendApplication.Named("Sales");

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*capital letters*");
    }

    [Fact]
    public void applications_are_equal_when_name_and_place_are_equal()
    {
        //Assert
        FrontendApplication.Named("sales").Should().Be(FrontendApplication.Named("sales"));
        FrontendApplication.Named("sales").GetHashCode().Should().Be(FrontendApplication.Named("sales").GetHashCode());
        FrontendApplication.Named("sales").Should().NotBe(FrontendApplication.Named("admin"));
        FrontendApplication.Named("sales").Should().NotBe(FrontendApplication.Default);
    }
}
