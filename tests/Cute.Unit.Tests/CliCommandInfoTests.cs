using Cute.Services.CliCommandInfo;
using FluentAssertions;

namespace Cute.Unit.Tests;

public class CliCommandInfoTests
{
    [Fact]
    public void CliCommandInfoBuilder_WithName_SetsName()
    {
        var command = new CliCommandInfo.Builder()
            .WithName("test")
            .Build();

        command.Name.Should().Be("test");
    }

    [Fact]
    public void CliCommandInfoBuilder_FromXml_ParsesCorrectly()
    {
        var command = CliCommandInfoXmlParser.FromXml(
            File.ReadAllText("files/CliCommandInfo.xml"),
            "The cute cli is a command line interface for interacting with Contentful."
        );

        command.Description.Should().Be("The cute cli is a command line interface for interacting with Contentful.");
    }
}