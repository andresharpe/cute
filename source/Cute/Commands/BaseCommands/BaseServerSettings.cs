using Cute.Commands.Login;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.BaseCommands;

public class BaseServerSettings : LoggedInSettings
{
    [CommandOption("-p|--port")]
    [Description("The port to listen on")]
    public int Port { get; set; }
}