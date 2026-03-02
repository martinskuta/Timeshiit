namespace TimeshiitCli.Commands.Timelogs;

public class TimesheetRow(
    DateOnly date,
    string clientName,
    string projectName,
    string taskName,
    string emailId,
    string jiraNo,
    double hours,
    string comment)
{
    public DateOnly Date { get; init; } = date;
    public string ClientName { get; set; } = clientName;
    public string ProjectName { get; set; } = projectName;
    public string TaskName { get; set; } = taskName;
    public string EmailId { get; init; } = emailId;
    public string JiraNo { get; init; } = jiraNo;
    public double Hours { get; init; } = hours;
    public string Comment { get; init; } = comment;
}