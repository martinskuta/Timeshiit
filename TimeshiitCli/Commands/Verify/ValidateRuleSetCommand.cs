using System.Text.Json;
using TimeshiitCli.Commands.Timelogs;
using TimeshiitCli.Data;
using TimeshiitCli.Util;

namespace TimeshiitCli.Commands.Verify;

public sealed class ValidateRuleSetCommand(FileInfo rulesFilePath) : CommandBase
{
    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        if (!File.Exists(rulesFilePath.FullName))
        {
            ConsoleUtil.ShowErrorAndExit(
                $"Fallback rules file '{rulesFilePath.FullName}' not found.");
            return;
        }

        await using var rulesJson = File.OpenRead(rulesFilePath.FullName);
        try
        {
            var ruleSet = await JsonSerializer.DeserializeAsync(rulesJson,
                GeneratedJsonContext.Default.JiraFallbackRuleSet,
                cancellationToken);
            if (ruleSet == null)
            {
                ConsoleUtil.ShowErrorAndExit(
                    $"Failed to load rules from file {rulesFilePath.FullName}. The rules deserialized to null.");
                return;
            }

            var fallbackRuleValidationErrors = JiraFallbackRuleEngine.Validate(ruleSet);
            if (fallbackRuleValidationErrors.Count > 0)
            {
                ConsoleUtil.ShowErrorAndExit(
                    $"Rule file '{rulesFilePath.FullName}' is invalid:{Environment.NewLine}{string.Join(Environment.NewLine, fallbackRuleValidationErrors.Select(e => $"- {e}"))}");
            }
        }
        catch (JsonException e)
        {
            ConsoleUtil.ShowErrorAndExit(
                $"Failed to parse rules file '{rulesFilePath.FullName}': {e.Message}");
        }
    }
}