using System;
using System.Linq;
using CodeBrix.WebPlatform.Core.Mapping;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Core.Tests.Mapping;

public class WireTypeAttributeTests
{
    [Fact]
    public void constructor_keeps_the_rust_type_and_leaves_the_rest_unset()
    {
        //Act
        var attribute = new WireTypeAttribute("Money");

        //Assert
        attribute.RustType.Should().Be("Money");
        attribute.Import.Should().BeNull();
        attribute.With.Should().BeNull();
    }

    [Fact]
    public void import_and_with_can_be_named()
    {
        //Act
        var attribute = new WireTypeAttribute("Money") { Import = "crate::money::Money", With = "crate::money::as_text" };

        //Assert
        attribute.Import.Should().Be("crate::money::Money");
        attribute.With.Should().Be("crate::money::as_text");
    }

    [Theory]
    [InlineData(typeof(WireRequiredAttribute), AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    [InlineData(typeof(WireOptionalAttribute), AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    [InlineData(typeof(WireTypeAttribute), AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    [InlineData(typeof(WireFileAttribute), AttributeTargets.Method)]
    [InlineData(typeof(WireIgnoreAttribute), AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
    [InlineData(typeof(WireHubAttribute), AttributeTargets.Class)]
    public void each_mapping_attribute_goes_only_where_the_mapper_reads_it(Type attributeType, AttributeTargets expected)
    {
        //Act
        var usage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false).Cast<AttributeUsageAttribute>().Single();

        //Assert
        usage.ValidOn.Should().Be(expected);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }
}
