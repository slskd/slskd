using System;
using System.IO;
using slskd.Files;
using Xunit;

namespace slskd.Tests.Unit.Files;

public class FileExtensionsTests
{
    [Theory]
    [InlineData("0000", UnixFileMode.None)]
    [InlineData("0100", UnixFileMode.UserExecute)]
    [InlineData("0200", UnixFileMode.UserWrite)]
    [InlineData("0300", UnixFileMode.UserWrite | UnixFileMode.UserExecute)]
    [InlineData("0400", UnixFileMode.UserRead)]
    [InlineData("0500", UnixFileMode.UserRead | UnixFileMode.UserExecute)]
    [InlineData("0600", UnixFileMode.UserRead | UnixFileMode.UserWrite)]
    [InlineData("0700", UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute)]
    [InlineData("0010", UnixFileMode.GroupExecute)]
    [InlineData("0020", UnixFileMode.GroupWrite)]
    [InlineData("0030", UnixFileMode.GroupWrite | UnixFileMode.GroupExecute)]
    [InlineData("0040", UnixFileMode.GroupRead)]
    [InlineData("0050", UnixFileMode.GroupRead | UnixFileMode.GroupExecute)]
    [InlineData("0060", UnixFileMode.GroupRead | UnixFileMode.GroupWrite)]
    [InlineData("0070", UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute)]
    [InlineData("0001", UnixFileMode.OtherExecute)]
    [InlineData("0002", UnixFileMode.OtherWrite)]
    [InlineData("0003", UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)]
    [InlineData("0004", UnixFileMode.OtherRead)]
    [InlineData("0005", UnixFileMode.OtherRead | UnixFileMode.OtherExecute)]
    [InlineData("0006", UnixFileMode.OtherRead | UnixFileMode.OtherWrite)]
    [InlineData("0007", UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)]
    [InlineData("0644", UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead)]
    [InlineData("0777", UnixFileMode.UserExecute | UnixFileMode.UserWrite | UnixFileMode.UserRead | UnixFileMode.GroupExecute | UnixFileMode.GroupWrite | UnixFileMode.GroupRead | UnixFileMode.OtherExecute | UnixFileMode.OtherWrite | UnixFileMode.OtherRead)]
    [InlineData("1000", UnixFileMode.StickyBit)]
    [InlineData("2000", UnixFileMode.SetGroup)]
    [InlineData("3000", UnixFileMode.SetGroup | UnixFileMode.StickyBit)]
    [InlineData("4000", UnixFileMode.SetUser)]
    [InlineData("5000", UnixFileMode.SetUser | UnixFileMode.StickyBit)]
    [InlineData("6000", UnixFileMode.SetUser | UnixFileMode.SetGroup)]
    [InlineData("7000", UnixFileMode.SetUser | UnixFileMode.SetGroup | UnixFileMode.StickyBit)]
    [InlineData("7777", UnixFileMode.SetUser | UnixFileMode.SetGroup | UnixFileMode.StickyBit | UnixFileMode.UserExecute | UnixFileMode.UserWrite | UnixFileMode.UserRead | UnixFileMode.GroupExecute | UnixFileMode.GroupWrite | UnixFileMode.GroupRead | UnixFileMode.OtherExecute | UnixFileMode.OtherWrite | UnixFileMode.OtherRead)]
    public void ToUnixFileMode_Works_As_Expected(string permissions, UnixFileMode expected)
    {
        var result = permissions.ToUnixFileMode();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToUinixFileMode_Throws_ArgumentException_Given_Empty()
    {
        var ex = Record.Exception(() => "".ToUnixFileMode());

        Assert.NotNull(ex);
        Assert.IsType<ArgumentException>(ex);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("00")]
    [InlineData("00000")]
    [InlineData("foo bar baz")]
    public void ToUinixFileMode_Throws_ArgumentOutOfRangeException_Given_Bad_String(string str)
    {
        var ex = Record.Exception(() => str.ToUnixFileMode());

        Assert.NotNull(ex);
        Assert.IsType<ArgumentOutOfRangeException>(ex);
    }
}