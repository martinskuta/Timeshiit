using System.Reflection;

namespace TimeshiitCli.Commands.Init;

public sealed class InitFallbackRulesCommand(DirectoryInfo outputFolder) : CommandBase
{
    private const string ExampleResourceName = "TimeshiitCli.Content.jira_fallback_rules.example.json";
    private const string SchemaResourceName = "TimeshiitCli.Content.jira_fallback_rules.schema.json";

    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        await WriteEmbeddedResource(ExampleResourceName, "jira_fallback_rules.example.json", cancellationToken);
        await WriteEmbeddedResource(SchemaResourceName, "jira_fallback_rules.schema.json", cancellationToken);
    }

    private async Task WriteEmbeddedResource(string resourceName, string outputFileName, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        var outputPath = Path.Combine(outputFolder.FullName, outputFileName);
        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, cancellationToken);

        Console.WriteLine($"Created {outputFileName} at {outputPath}");
    }
}
