using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.WebPlatform.FrontendBuilder.Sizes;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Sizes;

public class WasmModuleReaderTests
{
    [Fact]
    public void Read_gives_the_sections_the_bodies_and_the_names_by_function_index()
    {
        //Arrange
        var bytes = WasmFiles.Module(2, new[] { 3, 200, 5 }, dataBytes: 40, names: new Dictionary<int, string> { [0] = "imported", [2] = "first", [4] = "third" });

        //Act
        var info = WasmModuleReader.Read(bytes);

        //Assert
        info.ImportedFunctionCount.Should().Be(2);
        info.BodySizes.Should().Equal(3, 200, 5);
        info.CodeBytes.Should().Be(1 + (1 + 3) + (2 + 200) + (1 + 5));
        info.DataBytes.Should().Be(40);
        info.HasNameSection.Should().BeTrue();
        info.FunctionNames.Should().HaveCount(3);
        info.FunctionNames[2].Should().Be("first");
        info.FunctionNames[4].Should().Be("third");
    }

    [Fact]
    public void Read_of_a_file_without_names_has_no_name_section()
    {
        //Act
        var info = WasmModuleReader.Read(WasmFiles.Module(0, new[] { 4 }));

        //Assert
        info.HasNameSection.Should().BeFalse();
        info.FunctionNames.Should().BeEmpty();
        info.DataBytes.Should().Be(0);
        info.BodySizes.Should().Equal(4);
    }

    [Fact]
    public void Read_refuses_a_file_that_is_not_webassembly()
    {
        //Act
        Action act = () => WasmModuleReader.Read(new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01, 0x00 });

        //Assert
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Read_refuses_a_file_whose_section_runs_past_its_end()
    {
        //Arrange
        var bytes = WasmFiles.Module(0, new[] { 4 });
        Array.Resize(ref bytes, 14);

        //Act
        Action act = () => WasmModuleReader.Read(bytes);

        //Assert
        act.Should().Throw<InvalidDataException>();
    }
}
