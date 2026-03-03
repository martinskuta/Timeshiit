using TimeshiitCli.Commands.Init;
using TimeshiitCli.Commands.Timelogs;
using TimeshiitCli.Commands.Verify;

namespace TimeshiitCli.Commands;

public class CommandFactory
{
    public InitCommand CreateInitCommand(DirectoryInfo outputFolder, int year) => new(this, outputFolder, year);

    public InitLeavesCommand CreateInitLeavesCommand(DirectoryInfo outputFolder, int year) => new(outputFolder, year);

    public InitJobsCommand CreateInitJobsCommand(DirectoryInfo outputFolder) => new(outputFolder);

    public InitDependenciesCommand CreateInitDependenciesCommand() => new();

    public InitFallbackRulesCommand CreateInitFallbackRulesCommand(DirectoryInfo outputFolder) => new(outputFolder);

    public TimelogsCleanupCommand CreateTimelogsCleanupCommand(FileInfo timelogsCsvFilePath, string zohoDateFormat,
        DateOnly? fromDate, DateOnly? toDate) => new(timelogsCsvFilePath, zohoDateFormat, fromDate, toDate);

    public TimelogsEnrichCommand CreateEnrichCommand(FileInfo timelogsCsvFilePath, FileInfo jobsFilePath,
        FileInfo rulesFilePath, string zohoDateFormat) =>
        new(timelogsCsvFilePath, jobsFilePath, rulesFilePath, zohoDateFormat);

    public TimelogsVerifyCommand CreateTimelogsVerifyCommand(FileInfo timelogsCsvFilePath, string zohoDateFormat) =>
        new TimelogsVerifyCommand(timelogsCsvFilePath, zohoDateFormat);

    public ValidateRuleSetCommand CreateValidateRuleSetCommand(FileInfo timelogsCsvFilePath) =>
        new(timelogsCsvFilePath);
}