using System;
using CodeBrix.WebPlatform.BackendMapper.TypeMapping;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.BackendMapper.Tests.TypeMapping;

public class RustTypeMapTests
{
    [Theory]
    [InlineData("System.String", "String", false)]
    [InlineData("System.Boolean", "bool", true)]
    [InlineData("System.Int32", "i32", true)]
    [InlineData("System.Int64", "i64", true)]
    [InlineData("System.Double", "f64", true)]
    [InlineData("System.Decimal", "f64", true)]
    [InlineData("System.Guid", "String", false)]
    [InlineData("System.DateTime", "String", false)]
    [InlineData("System.DateTimeOffset", "String", false)]
    [InlineData("System.Byte[]", "String", false)]
    public void the_defaults_cover_the_everyday_types(string clrName, string rustName, bool copy)
    {
        //Act
        var found = new RustTypeMap().TryGet(clrName, out var rust);

        //Assert
        found.Should().BeTrue();
        rust.Name.Should().Be(rustName);
        rust.IsCopy.Should().Be(copy);
    }

    [Fact]
    public void DateOnly_defaults_to_the_shared_crates_date()
    {
        //Act
        new RustTypeMap().TryGet("System.DateOnly", out var rust);

        //Assert
        rust.Name.Should().Be("Date");
        rust.Import.Should().Be("codebrix_webplatform::dates::Date");
        rust.IsCopy.Should().BeTrue();
    }

    [Fact]
    public void Map_replaces_a_default_and_adds_a_new_type()
    {
        //Arrange
        var map = new RustTypeMap();

        //Act
        map.Map<decimal>(new RustType("Decimal")).Map("MyApp.Money", new RustType("Money") { Import = "crate::money::Money" });

        //Assert
        map.TryGet("System.Decimal", out var number).Should().BeTrue();
        number.Name.Should().Be("Decimal");
        map.TryGet("MyApp.Money", out var money).Should().BeTrue();
        money.Import.Should().Be("crate::money::Money");
        map.ClrTypeNames.Should().Contain("MyApp.Money");
    }

    [Fact]
    public void TryGet_answers_false_for_an_unknown_or_null_name()
    {
        //Arrange
        var map = new RustTypeMap();

        //Assert
        map.TryGet("System.Object", out _).Should().BeFalse();
        map.TryGet(null, out var rust).Should().BeFalse();
        rust.Should().BeNull();
    }

    [Fact]
    public void Map_refuses_a_blank_name_or_a_null_type()
    {
        //Arrange
        var map = new RustTypeMap();
        Action blank = () => map.Map(" ", new RustType("X"));
        Action missing = () => map.Map("System.Decimal", null);
        Action unnamed = () => _ = new RustType(" ");

        //Assert
        blank.Should().Throw<ArgumentException>();
        missing.Should().Throw<ArgumentNullException>();
        unnamed.Should().Throw<ArgumentException>();
    }
}
