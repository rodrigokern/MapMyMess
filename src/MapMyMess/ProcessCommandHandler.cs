using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spectre.Console;
using static MapMyMess.Constants;

namespace MapMyMess;

internal class ProcessCommandHandler
{
    const string mermaidLocation = "https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs";

    internal static void Process(ProcessCommand.Settings settings)
    {
        FileInfo fileInfo = new(settings.Source);
        bool reuse_rg = fileInfo.Extension == ".rg";

        string outFile = !string.IsNullOrWhiteSpace(settings.Output) ?
            settings.Output :
            Path.Combine(new DirectoryInfo(".").FullName, $"{fileInfo.Name}.html");

        string restoreGraphOutputPath = !string.IsNullOrWhiteSpace(settings.RestoreGraphOutputPath) ?
            settings.RestoreGraphOutputPath :
            Path.Combine(new DirectoryInfo(".").FullName, $"{fileInfo.Name}.rg");

        //Parameter not a RestoreGraph file, must [re]build
        if (!reuse_rg)
        {
            if (string.IsNullOrWhiteSpace(restoreGraphOutputPath))
                restoreGraphOutputPath = Path.Combine(new DirectoryInfo(".").FullName, $"{fileInfo.Name}.rg");

            if (!TryBuild(settings.Source, restoreGraphOutputPath))
                return;
        }

        SolutionGraph graph = ParseRestoreGraph(restoreGraphOutputPath, settings.GraphModeInput);
        string diagram = PrintMermaidDiagram(graph, settings.GraphDirectionInput, settings.ReductionActionInput);
        File.WriteAllText(outFile, diagram);

        outFile = new FileInfo(outFile).FullName;
        Console.WriteLine($"Done!");
        AnsiConsole.MarkupLine($"Graph written to: [{ProcessCommand.Settings.COLOR_HIGHLIGHT}]{outFile}[/]");
    }

    private static SolutionGraph ParseRestoreGraph(string path, GraphMode GraphMode)
    {
        var jsonString = File.ReadAllText(path);

        var jsonDom = JsonSerializer.Deserialize<JsonObject>(jsonString)!;
        var restores = jsonDom["restore"]!.AsObject().Select(t => t.Key).ToArray();

        List<string> projects = [];
        List<string> dependencies = [];
        SolutionGraph graph = new();

        foreach (var key in restores)
        {
            var project = jsonDom["projects"]![key]!;
            try
            {
                #region Project References
                var refs = project["restore"]!["frameworks"]!.AsObject().First()!.Value!["projectReferences"]!.AsObject().Select(t => t.Key).ToArray();

                var currentProj = Key2Name(key);
                foreach (var proj in refs)
                {
                    var depProj = Key2Name(proj);
                    var output = $"{currentProj} --> {depProj}";
                    projects.Add(output);

                    graph.AddEdge(currentProj, depProj);
                }
                #endregion

                //TODO: only print reused?
                //TODO: put deps in a box? (subgroup?)
                #region Project Dependencies
                if (GraphMode != GraphMode.Complete) continue;
                //TODO: fix dependencies (add to graph)
                JsonNode? deps = project["frameworks"]!.AsObject().First()!.Value!["dependencies"];
                if (deps == null) continue;

                var depsNames = deps.AsObject().Select(t => t.Key).ToArray();

                foreach (var dep in depsNames)
                {
                    var version = deps[dep]!["version"];
                    var output = $"{currentProj} -->|\"{version}\"| {dep}";
                    dependencies.Add(output);
                }
                #endregion
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        return graph;
    }

    private static bool TryBuild(string file, string pathOutput)
    {
        try
        {
            ProcessStartInfo psi = new("dotnet");
            psi.Arguments = $"msbuild \"{file}\" /t:GenerateRestoreGraphFile /p:RestoreGraphOutputPath=\"{pathOutput}\"";
            Process? process = System.Diagnostics.Process.Start(psi);

            if (process is null)
            {
                Console.WriteLine($"Failed to create process");
                return false;
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Console.WriteLine($"Halting execution, build ExitCode not 0. ExitCode: {process.ExitCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Build failed: {ex.Message}");
            return false;
        }

        return true;
    }

    private static string PrintMermaidDiagram(SolutionGraph graph, GraphDirection graphDirection, ReductionAction reductionAction)
    {
        StringBuilder sb = new($"flowchart {graphDirection}\n");
        List<int> toColor = [];
        int counter = 0;

        foreach (var node in graph.nodes.Values)
        {
            foreach (var parent in node.From.ToArray())
            {
                bool multiple = SolutionGraph.ParentIsAncestor(parent.Name, node);
                switch (reductionAction)
                {
                    case ReductionAction.None:
                        sb.AppendLine($"{parent.Name} --> {node.Name}");
                        break;
                    case ReductionAction.Color:
                        sb.AppendLine($"{parent.Name} --> {node.Name}");
                        if (multiple)
                            toColor.Add(counter);
                        break;
                    case ReductionAction.Remove:
                        if (!multiple)
                            sb.AppendLine($"{parent.Name} --> {node.Name}");
                        break;
                }
                counter++;
            }
        }

        if (toColor.Count > 0)
        {
            var coloredLines = string.Join(',', toColor);
            if (reductionAction != ReductionAction.Remove)
                sb.AppendLine($"linkStyle {coloredLines} stroke:#f00,stroke-width:2px;");
        }

        string diagram = sb.ToString();

        return @$"
<!DOCTYPE html>
<html lang=""en"">
  <body>
    <pre class=""mermaid"">
{diagram}
    </pre>
    <script type=""module"">
      import mermaid from '{mermaidLocation}';
    </script>
  </body>
</html>";
    }

    private static string Key2Name(string key)
    {
        var fi = new FileInfo(key);
        var name = fi.Name[..^fi.Extension.Length];
        return name;
    }
}
