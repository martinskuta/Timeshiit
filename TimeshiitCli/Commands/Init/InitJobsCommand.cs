namespace TimeshiitCli.Commands.Init;

public sealed class InitJobsCommand(DirectoryInfo outputFolder) : CommandBase
{
    protected override Task ExecuteInternal(CancellationToken cancellationToken)
    {
        //Currently no-op as I didn't find a solid way to get all the jobs out of zoho
        return Task.CompletedTask;
    }
}