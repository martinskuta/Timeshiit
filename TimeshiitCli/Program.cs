using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using TimeshiitCli.Commands;
using TimeshiitCli.Util;

var commandFactory = new CommandFactory();
var rootCommand = new RootCommand("Martini's timesheet reporting utility! No more manual entry of time logs!")
{
    ConfigureInitCommand(),
    ConfigureTimelogsCommand(),
    ConfigureValidateCommand()
};

var parseResult = rootCommand.Parse(args);
await parseResult.InvokeAsync();

return;

Command ConfigureInitCommand()
{
    var initCommand = new Command("init", "Initialization commands");

    var outputFolderOption = new Option<DirectoryInfo>("--outputFolder", "-o");
    outputFolderOption.Description = "The output folder for the tool where to create the init files";
    outputFolderOption.DefaultValueFactory = _ => new DirectoryInfo(Directory.GetCurrentDirectory());
    initCommand.Options.Add(outputFolderOption);

    var yearOption = new Option<int>("--year", "-y");
    yearOption.Description = "Year to use to query for leaves and holidays";
    yearOption.DefaultValueFactory = _ => DateTime.Today.Year;
    initCommand.Options.Add(yearOption);

    var initLeavesCommand = new Command("leaves",
        $"Initializes leaves + holidays file for the tool to use for lookup of holidays. Requires {ZohoCli.ZohoCliBinary} binary");
    initLeavesCommand.Options.Add(yearOption);
    initLeavesCommand.Options.Add(outputFolderOption);
    initLeavesCommand.SetAction(ctx =>
    {
        var outputFolder = ctx.GetRequiredValue(outputFolderOption);
        var year = ctx.GetRequiredValue(yearOption);
        return commandFactory.CreateInitLeavesCommand(outputFolder, year).Execute();
    });
    initCommand.Add(initLeavesCommand);

    var initJobsCommand = new Command("jobs",
        "Initializes jobs for the tool to use for lookup of clientName, projectName and taskName");
    initJobsCommand.Options.Add(outputFolderOption);
    initJobsCommand.SetAction(ctx =>
    {
        var outputFolder = ctx.GetRequiredValue(outputFolderOption);
        return commandFactory.CreateInitJobsCommand(outputFolder).Execute();
    });
    initCommand.Add(initJobsCommand);

    var initDependenciesCommand = new Command("dependencies",
        "Downloads Atlassian CLI (acli.exe) and Zoho CLI (zcli.exe) into the current working directory");
    initDependenciesCommand.SetAction(_ => commandFactory.CreateInitDependenciesCommand().Execute());
    initCommand.Add(initDependenciesCommand);

    initCommand.SetAction(ctx =>
    {
        var outputFolder = ctx.GetRequiredValue(outputFolderOption);
        var year = ctx.GetRequiredValue(yearOption);
        return commandFactory.CreateInitCommand(outputFolder, year).Execute();
    });

    return initCommand;
}

Command ConfigureTimelogsCommand()
{
    var dateFormat = "dd-MMM-yy";
    var timelogsCommand = new Command("timelogs",
        "Commands enrich/cleanup/verify/prepare the generated timelogs.csv file for ZOHO importing");

    var timelogsCsvFilePathOption = new Option<FileInfo>("--timesheetCsv", "-csv");
    timelogsCsvFilePathOption.Description = "The timesheet.csv file to use";
    timelogsCsvFilePathOption.Required = true;
    timelogsCsvFilePathOption.DefaultValueFactory =
        _ => new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "timesheet.csv"));

    var zohoOrganizationDateFormatOption = new Option<string>("--zoho-date-format", "-df");
    zohoOrganizationDateFormatOption.Description =
        "The zoho-date-format to use to 'fix' dates. This needs to match Zoho organization Locale & Display Format in Settings > Manage Accounts > Organization Setup > Organization Policy";
    zohoOrganizationDateFormatOption.DefaultValueFactory = _ => dateFormat;
    zohoOrganizationDateFormatOption.Required = true;

    var fromDateOption = new Option<DateOnly?>("--fromDate", "-f");
    fromDateOption.Description =
        $"'From' date (inclusive). Optional param to remove entries that are out of range. Default format is {dateFormat}, otherwise will use value of --zoho-date-format for parsing this argument";
    fromDateOption.CustomParser =
        result => ParseDate(result, result.GetRequiredValue(zohoOrganizationDateFormatOption));

    var toDateOption = new Option<DateOnly?>("--fromDate", "-f");
    toDateOption.Description =
        $"'To' date (inclusive). Optional param to remove entries that are out of range. Default format is {dateFormat}, otherwise will use value of --zoho-date-format for parsing this argument";
    toDateOption.CustomParser =
        result => ParseDate(result, result.GetRequiredValue(zohoOrganizationDateFormatOption));

    var cleanupCommand = new Command("cleanup",
        "Cleans up the timesheet.csv file. Removes entries for holidays and leaves and unifies date formats for ZOHO importing");
    cleanupCommand.Options.Add(timelogsCsvFilePathOption);
    cleanupCommand.Options.Add(zohoOrganizationDateFormatOption);
    cleanupCommand.Options.Add(fromDateOption);
    cleanupCommand.Options.Add(toDateOption);
    cleanupCommand.SetAction(ctx =>
    {
        var timesheetCsvFilePath = ctx.GetRequiredValue(timelogsCsvFilePathOption);
        var zohoDateFormat = ctx.GetRequiredValue(zohoOrganizationDateFormatOption);
        var fromDate = ctx.GetValue(fromDateOption);
        var toDate = ctx.GetValue(toDateOption);

        return commandFactory.CreateTimelogsCleanupCommand(timesheetCsvFilePath, zohoDateFormat, fromDate, toDate)
            .Execute();
    });

    timelogsCommand.Add(cleanupCommand);

    var jobsFilePathOption = new Option<FileInfo>("--jobs", "-j");
    jobsFilePathOption.Description = "The jobs.json file containing all zoho jobs";
    jobsFilePathOption.Required = true;
    jobsFilePathOption.DefaultValueFactory =
        _ => new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "jobs.json"));

    var rulesFilePathOption = new Option<FileInfo>("--rules", "-r");
    rulesFilePathOption.Description =
        "Fallback Jira mapping rules file used only by enrich when Jira fields cannot resolve project/task";
    rulesFilePathOption.DefaultValueFactory =
        _ => new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "jira_fallback_rules.json"));

    var enrichCommand = new Command("enrich",
        "Enriches the timesheet.csv file with client/project/job information for zoho using information from JIRA. Requires Atlassian CLI (acli.exe binary)");
    enrichCommand.Options.Add(timelogsCsvFilePathOption);
    enrichCommand.Options.Add(jobsFilePathOption);
    enrichCommand.Options.Add(rulesFilePathOption);
    enrichCommand.Options.Add(zohoOrganizationDateFormatOption);
    enrichCommand.SetAction(ctx =>
    {
        var timesheetCsvFilePath = ctx.GetRequiredValue(timelogsCsvFilePathOption);
        var jobsFilePath = ctx.GetRequiredValue(jobsFilePathOption);
        var rulesFilePath = ctx.GetRequiredValue(rulesFilePathOption);
        var zohoDateFormat = ctx.GetRequiredValue(zohoOrganizationDateFormatOption);

        return commandFactory.CreateEnrichCommand(timesheetCsvFilePath, jobsFilePath, rulesFilePath, zohoDateFormat)
            .Execute();
    });
    timelogsCommand.Add(enrichCommand);

    var verifyCommand = new Command("verify",
        "Verify that there is no overtime, no time reported on holidays and leaves, no weekend hours, and that 8hours a day and 40hours a week is reported in the timesheet.csv file");
    verifyCommand.Options.Add(timelogsCsvFilePathOption);
    verifyCommand.Options.Add(zohoOrganizationDateFormatOption);
    verifyCommand.SetAction(ctx =>
    {
        var timesheetCsvFilePath = ctx.GetRequiredValue(timelogsCsvFilePathOption);
        var zohoDateFormat = ctx.GetRequiredValue(zohoOrganizationDateFormatOption);

        return commandFactory.CreateTimelogsVerifyCommand(timesheetCsvFilePath, zohoDateFormat).Execute();
    });

    timelogsCommand.Add(verifyCommand);

    return timelogsCommand;

    DateOnly? ParseDate(ArgumentResult argResult, string zohoDateFormat)
    {
        if (argResult.Tokens.Count != 1)
        {
            argResult.AddError("Expected only one argument for date");
            return null;
        }

        if (DateOnly.TryParseExact(argResult.Tokens[0].Value, "d.M.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return date;
        }

        if (DateOnly.TryParseExact(argResult.Tokens[0].Value, zohoDateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date))
        {
            return date;
        }

        argResult.AddError("Date arg could not be parsed. Use d.M.yyyy format");
        return null;
    }
}

Command ConfigureValidateCommand()
{
    var verifyCommand = new Command("validate", "Set of helper validation commands");

    var rulesFilePathOption = new Option<FileInfo>("--rules", "-r");
    rulesFilePathOption.Description = "Fallback Jira mapping rules file to verify";
    rulesFilePathOption.DefaultValueFactory =
        _ => new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "jira_fallback_rules.json"));

    var verifyRulesCommand = new Command("rules", "Verify fallback rules file");
    verifyRulesCommand.Options.Add(rulesFilePathOption);
    verifyCommand.SetAction(ctx =>
    {
        var rulesFilePath = ctx.GetRequiredValue(rulesFilePathOption);

        return commandFactory.CreateValidateRuleSetCommand(rulesFilePath).Execute();
    });

    verifyCommand.Add(verifyRulesCommand);

    return verifyCommand;
}