using System.Text.Json.Serialization;

namespace TimeshiitCli.Data;

[JsonSerializable(typeof(List<LeaveInfo>))]
[JsonSerializable(typeof(List<JobItem>))]
[JsonSerializable(typeof(JiraFallbackRuleSet))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, Dictionary<string, List<JiraTicket>>>>))]
public partial class GeneratedJsonContext : JsonSerializerContext;