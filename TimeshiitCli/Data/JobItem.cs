namespace TimeshiitCli.Data;

using System.Text.Json.Serialization;

public record JobItem(
    [property: JsonPropertyName("jobName")]
    string JobName,
    [property: JsonPropertyName("jobId")] 
    string JobId,
    [property: JsonPropertyName("jobStatus")]
    string? JobStatus,
    [property: JsonPropertyName("clientId")]
    string ClientId,
    [property: JsonPropertyName("clientName")]
    string ClientName,
    [property: JsonPropertyName("jobColor")]
    string? JobColor,
    [property: JsonPropertyName("jobBillableStatus")]
    string? JobBillableStatus,
    [property: JsonPropertyName("projectName")]
    string ProjectName,
    [property: JsonPropertyName("projectId")]
    string ProjectId,
    [property: JsonPropertyName("isJobWorkItemsExist")]
    bool IsJobWorkItemsExist,
    [property: JsonPropertyName("iscompleted")]
    int IsCompleted
);