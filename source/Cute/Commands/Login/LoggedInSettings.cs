using Cute.Commands.BaseCommands;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Login;

public class LoggedInSettings : CommonSettings
{
    [CommandOption("--management-token <TOKEN>")]
    [Description("Your Contentful management API token. See [italic LightSkyBlue3]https://www.contentful.com/developers/docs/references/authentication/[/]")]
    public string? ManagementToken { get; set; }

    [CommandOption("--delivery-token <TOKEN>")]
    [Description("Your Contentful Content Delivery API token. See [italic LightSkyBlue3]https://www.contentful.com/developers/docs/references/authentication/[/]")]
    public string? ContentDeliveryToken { get; set; }

    [CommandOption("--preview-token <TOKEN>")]
    [Description("Your Contentful Content Preview API token. See [italic LightSkyBlue3]https://www.contentful.com/developers/docs/references/authentication/[/]")]
    public string? ContentPreviewToken { get; set; }

    [CommandOption("-s|--space-id <ID>")]
    [Description("The Contentful space identifier. See [italic LightSkyBlue3]https://www.contentful.com/help/spaces-and-organizations/[/]")]
    public string? SpaceId { get; set; }

    [CommandOption("-e|--environment-id <ID>")]
    [Description("The Contentful environment identifier. See [italic LightSkyBlue3]https://www.contentful.com/developers/docs/concepts/multiple-environments/[/]")]
    public string? EnvironmentId { get; set; }
}