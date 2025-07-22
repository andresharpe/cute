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

    [CommandOption("--no-publish")]
    [Description("Specifies whether to skip publish for modified entries")]
    public bool NoPublish { get; set; } = false;

    [CommandOption("--use-context")]
    [Description("Indicates whether to use context of the operation (eg: publish only entries modified by the command and not all the unpublished ones)")]
    public bool UseContext { get; set; } = false;
}