namespace TimeshiitCli.Commands.Init;

public sealed class InitCommand(CommandFactory cmdFactory, DirectoryInfo outputFolder, int year) : CommandBase
{
    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        await cmdFactory.CreateInitLeavesCommand(outputFolder, year).Execute(cancellationToken);
        await cmdFactory.CreateInitJobsCommand(outputFolder).Execute(cancellationToken);

        Console.WriteLine("Initialization completed");
    }
}