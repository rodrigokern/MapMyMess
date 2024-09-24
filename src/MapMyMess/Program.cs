using System.ComponentModel;
using MapMyMess;
using Spectre.Console;
using Spectre.Console.Cli;
using static MapMyMess.Constants;

var app = new CommandApp<ProcessCommand>();

app.Run(args);

internal class ProcessCommand : Command<ProcessCommand.Settings>
{
    public class Settings : CommandSettings
    {
        public const string COLOR_HIGHLIGHT = "blue";
        public const string COLOR_DEFAULT_ENUM = "purple";

        [Description("A solution to process or a pre-processed RestoreGraph (.rg) file.")]
        [CommandArgument(0, "<Source>")]
        public required string Source { get; set; }

        [Description($"Path to html output with graph data. Defaults to current folder, named \"[{COLOR_HIGHLIGHT}]{{Source}}.html[/]\"")]
        [CommandOption("-o|--Output")]
        public string? Output { get; set; }

        [Description($"Path to output a processed RestoreGraph (.rg) file. Defaults to current folder, named \"[{COLOR_HIGHLIGHT}]{{Source}}.rg[/]\"")]
        [CommandOption("-r|--RestoreGraph")]
        public string? RestoreGraphOutputPath { get; set; }

        [Description($"Direction of the graph. Valid values are '[{COLOR_DEFAULT_ENUM}]LR[/]', 'TD', 'RL', 'BT'")]
        [CommandOption("-d|--GraphDirection")]
        public GraphDirection GraphDirectionInput { get; set; }

        [Description($"What to do with extra edges in the graph. Valid values are '[{COLOR_DEFAULT_ENUM}]Color[/]', 'Remove', 'None'")]
        [CommandOption("-a|--ReductionAction")]
        public ReductionAction ReductionActionInput { get; set; }

        [Description($"Graph mode. Valid values are '[{COLOR_DEFAULT_ENUM}]Projects[/]', 'Complete'")]
        [CommandOption("-m|--GraphMode")]
        public GraphMode GraphModeInput { get; set; }

        public override ValidationResult Validate()
        {
            if (!Enum.IsDefined(GraphDirectionInput))
            {
                string validNames = string.Join("', '", Enum.GetNames<GraphDirection>());
                var error = $"Failed to convert '{GraphDirectionInput}' to {nameof(GraphDirection)}. Valid values are '{validNames}'";
                return ValidationResult.Error(error);
            }

            if (!Enum.IsDefined(ReductionActionInput))
            {
                string validNames = string.Join("', '", Enum.GetNames<ReductionAction>());
                var error = $"Failed to convert '{ReductionActionInput}' to {nameof(ReductionAction)}. Valid values are '{validNames}'";
                return ValidationResult.Error(error);
            }

            if (!Enum.IsDefined(GraphModeInput))
            {
                string validNames = string.Join("', '", Enum.GetNames<GraphMode>());
                var error = $"Failed to convert '{GraphModeInput}' to {nameof(GraphMode)}. Valid values are '{validNames}'";
                return ValidationResult.Error(error);
            }

            return ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        ProcessCommandHandler.Process(settings);
        return 0;
    }
}
