using Cute.Commands.Login;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Content;

public abstract class ContentCommandSettings : LoggedInSettings
{
    [CommandOption("-c|--content-type-id <ID>")]
    [Description("The Contentful content type id.")]
    public string ContentTypeId { get; set; } = default!;

    [CommandOption("-l|--locale <CODE>")]
    [Description("The locale code (eg. 'en') to apply the command to. Default is all.")]
    public string[] Locales { get; set; } = default!;
}