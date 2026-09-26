using System.Linq;
using Xunit;

namespace slskd.Tests.Unit.Core;

public class OptionsSchemaTests
{
    private static OptionsSchemaNode Find(params string[] path)
    {
        var node = OptionsSchema.Root;

        foreach (var name in path)
        {
            node = node.Properties?.SingleOrDefault(child => child.Name == name);
            Assert.NotNull(node);
        }

        return node;
    }

    [Fact]
    public void Uses_Api_Names_For_Nested_Options()
    {
        var node = Find("soulseek", "listenPort");

        Assert.Equal("integer", node.Type);
        Assert.Equal(50300, node.Default);
    }

    [Fact]
    public void Omits_Command_Line_Only_And_Obsolete_Options()
    {
        var names = OptionsSchema.Root.Properties.Select(child => child.Name).ToList();

        Assert.DoesNotContain("appDirectory", names);
        Assert.DoesNotContain("configurationFile", names);
        Assert.DoesNotContain("showVersion", names);
        Assert.DoesNotContain("global", names);
        Assert.DoesNotContain("groups", names);
    }

    [Fact]
    public void Describes_Enum_Values_In_Lowercase()
    {
        var node = Find("shares", "cache", "storageMode");

        Assert.Equal("string", node.Type);
        Assert.Contains("memory", node.Values);
        Assert.Contains("disk", node.Values);
    }

    [Fact]
    public void Marks_Secrets_And_Hides_Their_Defaults()
    {
        var key = Find("web", "authentication", "jwt", "key");

        Assert.True(key.Secret);
        Assert.Null(key.Default);
    }

    [Fact]
    public void Inherits_Restart_Requirement_From_Parent()
    {
        // directories is marked as requiring a restart as a whole
        Assert.True(Find("directories", "downloads").RequiresRestart);
    }

    [Fact]
    public void Describes_Dictionaries_With_Their_Item_Schema()
    {
        var webhooks = Find("integrations", "webhooks");

        Assert.Equal("dictionary", webhooks.Type);
        Assert.Contains(webhooks.Items.Properties, child => child.Name == "call");
    }

    [Fact]
    public void Includes_Range_Limits()
    {
        var node = Find("soulseek", "listenPort");

        Assert.Equal(1024, node.Minimum);
        Assert.Equal(65535, node.Maximum);
    }
}
