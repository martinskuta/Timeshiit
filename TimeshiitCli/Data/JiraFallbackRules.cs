using System.Text.Json.Serialization;

namespace TimeshiitCli.Data;

public sealed class JiraFallbackRuleSet
{
    [JsonPropertyName("version")] public int Version { get; init; } = 1;

    [JsonPropertyName("rules")] public List<JiraFallbackRule> Rules { get; init; } = [];
}

public sealed class JiraFallbackRule
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;

    [JsonPropertyName("description")] public string? Description { get; init; }

    [JsonPropertyName("whenAll")] public List<JiraFallbackCondition> WhenAll { get; init; } = [];

    [JsonPropertyName("whenAny")] public List<JiraFallbackCondition>? WhenAny { get; init; }

    [JsonPropertyName("set")] public JiraFallbackSetAction Set { get; init; } = new();

    [JsonPropertyName("stopProcessing")] public bool? StopProcessing { get; init; }
}

public sealed class JiraFallbackCondition
{
    [JsonPropertyName("column")] public string Column { get; init; } = string.Empty;

    [JsonPropertyName("operator")] public string Operator { get; init; } = string.Empty;

    [JsonPropertyName("value")] public string Value { get; init; } = string.Empty;

    [JsonPropertyName("ignoreCase")] public bool? IgnoreCase { get; init; }
}

public sealed class JiraFallbackSetAction
{
    [JsonPropertyName("clientName")] public string? ClientName { get; init; }

    [JsonPropertyName("projectName")] public string? ProjectName { get; init; }

    [JsonPropertyName("taskName")] public string? TaskName { get; init; }
}