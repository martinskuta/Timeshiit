namespace TimeshiitCli.Data;

public class LeaveInfo
{
    public DateOnly Date { get; set; }

    public int Hours { get; set; }
    
    public string Type { get; set; } = string.Empty;
    
    public string Reason { get; set; } = string.Empty;
}
