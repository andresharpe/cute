namespace Cute.Services.CliCommandInfo;

public class CliCommandInfo
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public List<CliCommandInfo> SubCommands { get; private set; } = [];
    public List<CliOptionInfo> Options { get; private set; } = [];
    public List<CliOptionInfo> RemovedCommonOptions { get; private set; } = [];
    public List<CliOptionInfo> ConsolidatedOptions { get; private set; } = [];

    private CliCommandInfo()
    { }

    public class Builder
    {
        private readonly CliCommandInfo _command = new();

        public Builder WithName(string name)
        {
            _command.Name = name;
            return this;
        }

        public Builder WithDescription(string description)
        {
            _command.Description = description;
            return this;
        }

        public Builder AddOption(string shortName, string longName, string description)
        {
            _command.Options.Add(new CliOptionInfo
            {
                ShortName = shortName,
                LongName = longName,
                Description = description
            });
            return this;
        }

        public Builder AddSubCommand(CliCommandInfo subCommand)
        {
            _command.SubCommands.Add(subCommand);
            return this;
        }

        public CliCommandInfo Build()
        {
            return _command;
        }
    }

    public void ConsolidateOptions()
    {
        if (SubCommands.Count > 0)
        {
            // First, recursively consolidate options for all subcommands
            foreach (var subCommand in SubCommands)
            {
                subCommand.ConsolidateOptions();
            }

            // Find common options, considering both direct options and consolidated options from all subcommands
            var commonOptions = FindCommonOptions();

            // If common options exist, consolidate them
            if (commonOptions.Any())
            {
                foreach (var subCommand in SubCommands)
                {
                    // Remove common options from both Options and ConsolidatedOptions of subcommands
                    subCommand.Options.RemoveAll(opt => commonOptions.Contains(opt));
                    subCommand.ConsolidatedOptions.RemoveAll(opt => commonOptions.Contains(opt));
                }

                // Add the common options to this command's ConsolidatedOptions
                ConsolidatedOptions.AddRange(commonOptions);
            }
        }

        // Leaf nodes or commands with no subcommands will retain their own options
    }

    private List<CliOptionInfo> FindCommonOptions()
    {
        if (SubCommands.Count == 0)
        {
            return new List<CliOptionInfo>();
        }

        // Start by considering both Options and ConsolidatedOptions of the first subcommand
        var commonOptions = new HashSet<CliOptionInfo>(SubCommands[0].Options);
        commonOptions.UnionWith(SubCommands[0].ConsolidatedOptions);  // Include already consolidated options

        // Perform intersection with the Options and ConsolidatedOptions of each remaining subcommand
        foreach (var subCommand in SubCommands.Skip(1))
        {
            if (subCommand.Options.Count + subCommand.ConsolidatedOptions.Count == 0)
            {
                // mainly to deal with "version" command which has no options
                continue;
            }

            var subCommandOptions = new HashSet<CliOptionInfo>(subCommand.Options);
            subCommandOptions.UnionWith(subCommand.ConsolidatedOptions);  // Include already consolidated options

            commonOptions.IntersectWith(subCommandOptions);  // Intersect with accumulated common options
        }

        return commonOptions.ToList();
    }

    public List<CliOptionInfo> GetAllOptions()
    {
        var allOptions = new List<CliOptionInfo>();

        allOptions.AddRange(Options);

        allOptions.AddRange(ConsolidatedOptions);

        return allOptions;
    }

    public List<CliOptionInfo> GetNonCommonOptions()
    {
        return new List<CliOptionInfo>(Options);
    }
}