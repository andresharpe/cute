using Cute.Lib.Enums;
using Cute.TypeConverters;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.BaseCommands;

public class CommonSettings : CommandSettings
{
    [CommandOption("--log-output")]
    [Description("Outputs logs to the console instead of the standard messages.")]
    public bool LogOutput { get; set; } = false;

    [CommandOption("--no-banner")]
    [Description("Do not display the startup banner or the copyright message.")]
    public bool NoBanner { get; set; } = false;

    [CommandOption("--verbosity <LEVEL>")]
    [Description(@"Sets the output verbosity level. Allowed values are (q)uiet, (m)inimal, (n)ormal, (de)tailed and (di)agnostic.")]
    [TypeConverter(typeof(PartialStringToEnumConverter<Verbosity>))]
    public Verbosity Verbosity { get; set; } = Verbosity.Normal;
}