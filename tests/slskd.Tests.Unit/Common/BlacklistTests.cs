using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace slskd.Tests.Unit.Common;

public class BlacklistTests
{
    [Theory]
    [InlineData("Data/Blacklist/cidr.txt", BlacklistFormat.CIDR)]
    [InlineData("Data/Blacklist/dat.txt", BlacklistFormat.DAT)]
    [InlineData("Data/Blacklist/p2p.txt", BlacklistFormat.P2P)]
    public async Task DetectFormat_Detects(string filename, BlacklistFormat expected)
    {
        var result = await Blacklist.DetectFormat(filename);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Data/Blacklist/junk.txt", typeof(FormatException))]
    [InlineData("Data/Blacklist/empty.txt", typeof(FormatException))]
    [InlineData("Data/does.not.exist.txt", typeof(IOException))]
    public async Task DetectFormat_Throws(string filename, Type exType)
    {
        var ex = await Record.ExceptionAsync(() => Blacklist.DetectFormat(filename));

        Assert.NotNull(ex);
        Assert.IsAssignableFrom(exType, ex);
    }

    [Theory]
    [InlineData("Data/Blacklist/junk.txt", BlacklistFormat.CIDR, typeof(FormatException))]
    [InlineData("Data/does.not.exist.txt", BlacklistFormat.DAT, typeof(IOException))]
    public async Task Load_throws(string filename, BlacklistFormat format, Type exType)
    {
        var bl = new Blacklist();
        var ex = await Record.ExceptionAsync(() => bl.Load(filename, format));

        Assert.NotNull(ex);
        Assert.IsAssignableFrom(exType, ex);
    }

    [Theory]
    [InlineData("Data/Blacklist/cidr.txt", BlacklistFormat.CIDR)]
    [InlineData("Data/Blacklist/dat.txt", BlacklistFormat.DAT)]
    [InlineData("Data/Blacklist/p2p.txt", BlacklistFormat.P2P)]
    public async Task Load_Loads(string filename, BlacklistFormat format)
    {
        var bl = new Blacklist();
        var ex = await Record.ExceptionAsync(() => bl.Load(filename, format));

        Assert.Null(ex);

        // the test files are assumed to all contain 5 entries
        Assert.Equal(5, bl.Count);
    }

    [Theory]
    [InlineData("Data/Blacklist/cidr.txt")]
    [InlineData("Data/Blacklist/dat.txt")]
    [InlineData("Data/Blacklist/p2p.txt")]
    public async Task Load_AutoDetects_And_Loads(string filename)
    {
        var bl = new Blacklist();
        var ex = await Record.ExceptionAsync(() => bl.Load(filename));

        Assert.Null(ex);

        // the test files are assumed to all contain 5 entries
        Assert.Equal(5, bl.Count);
    }
}