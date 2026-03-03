using System.Globalization;
using System.Text.RegularExpressions;
using TimeshiitCli.Data;

namespace TimeshiitCli.Commands.Timelogs;

internal sealed class JiraFallbackRuleEngine(IReadOnlyList<JiraFallbackRule> rules)
{
    private static readonly HashSet<string> SupportedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "date",
        "clientName",
        "projectName",
        "taskName",
        "emailId",
        "jiraNo",
        "hours",
        "comment",
        "issueType"
    };

    private static readonly HashSet<string> SupportedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "empty",
        "notempty",
        "blank",
        "notblank",
        "equals",
        "contains",
        "startsWith",
        "endsWith",
        "regex"
    };

    public bool TryApply(TimesheetRow row, string? issueType)
    {
        var applied = false;
        foreach (var rule in rules)
        {
            if (!MatchesRule(rule, row, issueType)) continue;

            ApplySetAction(rule.Set, row);
            applied = true;
            if (rule.StopProcessing ?? true) break;
        }

        return applied;
    }

    public static List<string> Validate(JiraFallbackRuleSet ruleSet)
    {
        var errors = new List<string>();
        for (var i = 0; i < ruleSet.Rules.Count; i++)
        {
            var rule = ruleSet.Rules[i];
            var ruleLabel = string.IsNullOrWhiteSpace(rule.Id) ? $"rules[{i}]" : $"rule '{rule.Id}'";

            if (string.IsNullOrWhiteSpace(rule.Id))
                errors.Add($"{ruleLabel}: 'id' is required.");

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (rule.Set is null)
            {
                errors.Add($"{ruleLabel}: 'set' is required.");
            }
            else if (rule.Set.ClientName is null && rule.Set.ProjectName is null && rule.Set.TaskName is null)
            {
                errors.Add(
                    $"{ruleLabel}: 'set' must define at least one of 'clientName', 'projectName', 'taskName'.");
            }

            ValidateConditions(rule.WhenAll, $"{ruleLabel} whenAll", errors);
            ValidateConditions(rule.WhenAny, $"{ruleLabel} whenAny", errors);
        }

        return errors;
    }

    private static void ValidateConditions(IReadOnlyList<JiraFallbackCondition>? conditions, string path,
        List<string> errors)
    {
        if (conditions is null) return;

        for (var i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            var label = $"{path}[{i}]";

            if (string.IsNullOrWhiteSpace(condition.Column))
            {
                errors.Add($"{label}: 'column' is required.");
                continue;
            }

            if (!SupportedColumns.Contains(condition.Column))
                errors.Add($"{label}: unsupported column '{condition.Column}'.");

            if (string.IsNullOrWhiteSpace(condition.Operator))
            {
                errors.Add($"{label}: 'operator' is required.");
                continue;
            }

            if (!SupportedOperators.Contains(condition.Operator))
            {
                errors.Add($"{label}: unsupported operator '{condition.Operator}'.");
                continue;
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (condition.Value is null && !(condition.Operator.Equals("empty", StringComparison.OrdinalIgnoreCase) ||
                                             condition.Operator.Equals("blank", StringComparison.OrdinalIgnoreCase) ||
                                             condition.Operator.Equals("notEmpty",
                                                 StringComparison.OrdinalIgnoreCase) ||
                                             condition.Operator.Equals("notBlank", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"{label}: 'value' is required.");
                continue;
            }

            if (condition.Operator.Equals("regex", StringComparison.OrdinalIgnoreCase))
            {
                var regexOptions = RegexOptions.CultureInvariant;
                if (condition.IgnoreCase ?? true) regexOptions |= RegexOptions.IgnoreCase;

                try
                {
                    _ = new Regex(condition.Value, regexOptions, TimeSpan.FromSeconds(2));
                }
                catch (ArgumentException e)
                {
                    errors.Add($"{label}: invalid regex '{condition.Value}'. {e.Message}");
                }
            }
        }
    }

    private static bool MatchesRule(JiraFallbackRule rule, TimesheetRow row, string? issueType)
    {
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        var allConditions = rule.WhenAll ?? [];
        var allMatch = allConditions.All(condition => MatchesCondition(condition, row, issueType));
        if (!allMatch) return false;

        if (rule.WhenAny is null || rule.WhenAny.Count == 0) return true;
        return rule.WhenAny.Any(condition => MatchesCondition(condition, row, issueType));
    }

    private static bool MatchesCondition(JiraFallbackCondition condition, TimesheetRow row, string? issueType)
    {
        var actual = GetColumnValue(condition.Column, row, issueType);
        var comparison = condition.IgnoreCase ?? true
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return condition.Operator.ToLowerInvariant() switch
        {
            "empty" => string.IsNullOrEmpty(actual),
            "notempty" => !string.IsNullOrEmpty(actual),
            "blank" => string.IsNullOrWhiteSpace(actual),
            "notblank" => !string.IsNullOrWhiteSpace(actual),
            "equals" => string.Equals(actual, condition.Value, comparison),
            "contains" => actual.Contains(condition.Value, comparison),
            "startswith" => actual.StartsWith(condition.Value, comparison),
            "endswith" => actual.EndsWith(condition.Value, comparison),
            "regex" => Regex.IsMatch(actual, condition.Value, BuildRegexOptions(condition), TimeSpan.FromSeconds(2)),
            _ => false
        };
    }

    private static RegexOptions BuildRegexOptions(JiraFallbackCondition condition)
    {
        var options = RegexOptions.CultureInvariant;
        if (condition.IgnoreCase ?? true) options |= RegexOptions.IgnoreCase;
        return options;
    }

    private static string GetColumnValue(string column, TimesheetRow row, string? issueType) =>
        column.ToLowerInvariant() switch
        {
            "date" => row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "clientname" => row.ClientName,
            "projectname" => row.ProjectName,
            "taskname" => row.TaskName,
            "emailid" => row.EmailId,
            "jirano" => row.JiraNo,
            "hours" => row.Hours.ToString(CultureInfo.InvariantCulture),
            "comment" => row.Comment,
            "issuetype" => issueType ?? string.Empty,
            _ => string.Empty
        };

    private static void ApplySetAction(JiraFallbackSetAction setAction, TimesheetRow row)
    {
        if (setAction.ClientName is not null) row.ClientName = setAction.ClientName;
        if (setAction.ProjectName is not null) row.ProjectName = setAction.ProjectName;
        if (setAction.TaskName is not null) row.TaskName = setAction.TaskName;
    }
}