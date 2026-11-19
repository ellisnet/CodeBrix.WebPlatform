using System;
using System.IO;
using CodeBrix.WebPlatform.LibraryChecker.Patching;
using CodeBrix.WebPlatform.LibraryChecker.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.LibraryChecker.Tests.Patching;

public class UnifiedDiffTests
{
    private const string Modify = "diff -ruN a/src/lib.rs b/src/lib.rs\n--- a/src/lib.rs\n+++ b/src/lib.rs\n@@ -1,4 +1,5 @@\n one\n-two\n+TWO\n+two and a half\n three\n four\n";

    [Fact]
    public void Apply_changes_the_lines_a_hunk_names()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("src/lib.rs", "one\ntwo\nthree\nfour\nfive\n");

        //Act
        var touched = UnifiedDiff.Apply(folder.Path, Modify);

        //Assert
        folder.Read("src/lib.rs").Should().Be("one\nTWO\ntwo and a half\nthree\nfour\nfive\n");
        touched.Should().ContainSingle().Which.Should().Be("src/lib.rs");
    }

    [Fact]
    public void Apply_applies_several_hunks_and_several_files_in_order()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("a.txt", "1\n2\n3\n4\n5\n6\n7\n8\n");
        folder.Write("b.txt", "x\ny\n");
        var patch = "--- a/a.txt\n+++ b/a.txt\n@@ -1,2 +1,2 @@\n-1\n+one\n 2\n@@ -7,2 +7,3 @@\n 7\n 8\n+9\n"
            + "--- a/b.txt\n+++ b/b.txt\n@@ -2 +2 @@\n-y\n+why\n";

        //Act
        var touched = UnifiedDiff.Apply(folder.Path, patch);

        //Assert
        folder.Read("a.txt").Should().Be("one\n2\n3\n4\n5\n6\n7\n8\n9\n");
        folder.Read("b.txt").Should().Be("x\nwhy\n");
        touched.Should().ContainInOrder("a.txt", "b.txt");
    }

    [Fact]
    public void Apply_creates_a_file_from_a_zero_range_and_deletes_a_file_left_empty()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("old.rs", "gone\nalso gone\n");
        var patch = "--- a/new/file.rs\t1970-01-01\n+++ b/new/file.rs\t2026-01-01\n@@ -0,0 +1,2 @@\n+first\n+second\n"
            + "--- a/old.rs\n+++ b/old.rs\n@@ -1,2 +0,0 @@\n-gone\n-also gone\n";

        //Act
        UnifiedDiff.Apply(folder.Path, patch);

        //Assert
        folder.Read("new/file.rs").Should().Be("first\nsecond\n");
        folder.Exists("old.rs").Should().BeFalse();
    }

    private const string HelloSha256 = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

    [Fact]
    public void Apply_deletes_a_file_named_by_a_delete_file_line_when_its_checksum_matches()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("assets/blob.bin", "hello");
        folder.Write("a.txt", "one\n");
        var patch = "delete-file sha256:" + HelloSha256 + " a/assets/blob.bin\n"
            + "--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-one\n+two\n";

        //Act
        var touched = UnifiedDiff.Apply(folder.Path, patch);

        //Assert
        folder.Exists("assets/blob.bin").Should().BeFalse();
        folder.Read("a.txt").Should().Be("two\n");
        touched.Should().ContainInOrder("assets/blob.bin", "a.txt");
        UnifiedDiff.ListFiles(patch).Should().ContainInOrder("assets/blob.bin", "a.txt");
    }

    [Fact]
    public void Apply_refuses_to_delete_a_file_that_is_missing_or_is_not_the_one_named()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("other.bin", "not hello");
        Action missing = () => UnifiedDiff.Apply(folder.Path, "delete-file sha256:" + HelloSha256 + " a/blob.bin\n");
        Action different = () => UnifiedDiff.Apply(folder.Path, "delete-file sha256:" + HelloSha256 + " a/other.bin\n");

        //Assert
        missing.Should().Throw<InvalidDataException>().WithMessage("*does not exist*");
        different.Should().Throw<InvalidDataException>().WithMessage("*not the one the patch names*");
        folder.Exists("other.bin").Should().BeTrue();
    }

    [Theory]
    [InlineData("delete-file a/blob.bin\n")]
    [InlineData("delete-file sha256:1234 a/blob.bin\n")]
    [InlineData("delete-file md5:2cf24dba5fb0a30e26e83b2ac5b9e29e a/blob.bin\n")]
    [InlineData("delete-file sha256:2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824 blob.bin\n")]
    public void Apply_rejects_a_malformed_delete_file_line(string patch)
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("blob.bin", "hello");
        Action act = () => UnifiedDiff.Apply(folder.Path, patch);

        //Assert
        act.Should().Throw<InvalidDataException>();
        folder.Exists("blob.bin").Should().BeTrue();
    }

    [Fact]
    public void Apply_keeps_and_changes_a_missing_final_newline()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("keep.txt", "a\nb");
        folder.Write("add.txt", "a\nb");
        var keep = "--- a/keep.txt\n+++ b/keep.txt\n@@ -1,2 +1,2 @@\n-a\n+A\n b\n\\ No newline at end of file\n";
        var add = "--- a/add.txt\n+++ b/add.txt\n@@ -1,2 +1,2 @@\n a\n-b\n\\ No newline at end of file\n+B\n";

        //Act
        UnifiedDiff.Apply(folder.Path, keep);
        UnifiedDiff.Apply(folder.Path, add);

        //Assert
        folder.Read("keep.txt").Should().Be("A\nb");
        folder.Read("add.txt").Should().Be("a\nB\n");
    }

    [Fact]
    public void Apply_refuses_a_hunk_that_does_not_match_exactly()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("src/lib.rs", "one\nTWO ALREADY\nthree\nfour\n");
        Action act = () => UnifiedDiff.Apply(folder.Path, Modify);

        //Assert
        act.Should().Throw<InvalidDataException>().WithMessage("*src/lib.rs*line 2*");
        folder.Read("src/lib.rs").Should().Be("one\nTWO ALREADY\nthree\nfour\n");
    }

    [Fact]
    public void Apply_refuses_to_change_a_missing_file_or_create_an_existing_one()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("exists.rs", "x\n");
        Action missing = () => UnifiedDiff.Apply(folder.Path, Modify);
        Action existing = () => UnifiedDiff.Apply(folder.Path, "--- a/exists.rs\n+++ b/exists.rs\n@@ -0,0 +1 @@\n+y\n");

        //Assert
        missing.Should().Throw<InvalidDataException>().WithMessage("*does not exist*");
        existing.Should().Throw<InvalidDataException>().WithMessage("*already exists*");
    }

    [Fact]
    public void Apply_rejects_a_malformed_patch()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("a.txt", "1\n");
        Action truncated = () => UnifiedDiff.Apply(folder.Path, "--- a/a.txt\n+++ b/a.txt\n@@ -1,2 +1,2 @@\n 1\n");
        Action noHunk = () => UnifiedDiff.Apply(folder.Path, "--- a/a.txt\n+++ b/a.txt\n");
        Action badHeader = () => UnifiedDiff.Apply(folder.Path, "--- a/a.txt\n+++ b/a.txt\n@@ nonsense @@\n");

        //Assert
        truncated.Should().Throw<InvalidDataException>();
        noHunk.Should().Throw<InvalidDataException>();
        badHeader.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ListFiles_names_the_files_without_touching_anything()
        => UnifiedDiff.ListFiles(Modify).Should().ContainSingle().Which.Should().Be("src/lib.rs");
}
