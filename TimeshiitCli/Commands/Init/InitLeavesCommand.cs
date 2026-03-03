using TimeshiitCli.Util;
using TimeshiitCli.Util.Extensions;

namespace TimeshiitCli.Commands.Init;

public sealed class InitLeavesCommand(DirectoryInfo folderOutput, int year) : CommandBase
{
    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        await ZohoCli.RunCommand("auth logout");
        Console.WriteLine("Initializing Zoho login procedure...");
        await ZohoCli.RunCommand("auth login");

        var fromDate = new DateOnly(year, 1, 1);
        var toDate = new DateOnly(year, 12, 31);
        const string dateFormat = "yyyy-MM-dd";

        var leaveInfosJson =
            await ZohoCli.RunCommand(
                $"leave get all --fromDate {fromDate.ToStringInvariant(dateFormat)}  --toDate {toDate.ToStringInvariant(dateFormat)} --dateFormat {dateFormat}");

        var resultFilePath = Path.Combine(folderOutput.FullName, $"leaves_{year}.json");
        await File.WriteAllTextAsync(resultFilePath, leaveInfosJson, cancellationToken);
        Console.WriteLine($"Created leaves file at {resultFilePath}");
    }
}