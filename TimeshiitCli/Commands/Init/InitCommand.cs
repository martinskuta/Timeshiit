namespace TimeshiitCli.Commands.Init;

public sealed class InitCommand(CommandFactory cmdFactory, DirectoryInfo outputFolder, int year) : CommandBase
{
    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            cmdFactory.CreateInitLeavesCommand(outputFolder, year).Execute(cancellationToken),
            cmdFactory.CreateInitJobsCommand(outputFolder).Execute(cancellationToken),
            cmdFactory.CreateInitDependenciesCommand().Execute(cancellationToken));

        Console.WriteLine("Initialization completed");
    }
}