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
        string file = settings.Source;
        string? restoreGraphOutputPath = settings.RestoreGraphOutputPath;

        FileInfo fileInfo = new(file);
        bool reuse_rg = fileInfo.Extension == ".rg";

        string outFile = !string.IsNullOrWhiteSpace(settings.Output) ?
            settings.Output :
            Path.Combine(new DirectoryInfo(".").FullName, $"{fileInfo.Name}.html");

        //Parameter not a RestoreGraph file, must [re]build
        if (!reuse_rg)
        {
            if (string.IsNullOrWhiteSpace(restoreGraphOutputPath))
                restoreGraphOutputPath = Path.Combine(new DirectoryInfo(".").FullName, $"{fileInfo.Name}.rg");

            if (!TryBuild(file, restoreGraphOutputPath))
                return;
        }

        SolutionGraph graph = ParseRestoreGraph(restoreGraphOutputPath, settings);
        string diagram = PrintMermaidDiagram(graph, settings.GraphDirectionInput);
        File.WriteAllText(outFile, diagram);

        outFile = new FileInfo(outFile).FullName;
        Console.WriteLine($"Done!");
        AnsiConsole.MarkupLine($"Graph written to: [blue]{outFile}[/]");
    }

    private static SolutionGraph ParseRestoreGraph(string path, ProcessCommand.Settings settings)
    {
        var jsonString = File.ReadAllText(path);

        var jsonDom = JsonSerializer.Deserialize<JsonObject>(jsonString)!;
        var restores = jsonDom["restore"]!.AsObject().Select(t => t.Key).ToArray();

        //TODO: add printDeps to settings
        bool printDeps = false;
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
                if (!printDeps) continue;
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

    private static string PrintMermaidDiagram(SolutionGraph graph, GraphDirection graphDirection)
    {
        StringBuilder sb = new($"flowchart {graphDirection}");

        foreach (var node in graph.nodes.Values)
        {
            foreach (var item in node.To)
            {
                var s = $"\n{node.Name} --> {item.Name}";
                sb.Append(s);
            }
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
