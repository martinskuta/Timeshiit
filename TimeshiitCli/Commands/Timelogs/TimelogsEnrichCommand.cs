using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using TimeshiitCli.Data;
using TimeshiitCli.Util;

namespace TimeshiitCli.Commands.Timelogs;

public sealed class TimelogsEnrichCommand(
    FileInfo timelogsCsvFilePath,
    FileInfo jobsFilePath,
    FileInfo rulesFilePath,
    string zohoDateFormat)
    : TimelogsCommandBase(timelogsCsvFilePath, zohoDateFormat)
{
    private readonly FileInfo _timelogsCsvFilePath = timelogsCsvFilePath;

    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"Loading timelogs '{_timelogsCsvFilePath}', jobs '{jobsFilePath}', and fallback rules '{rulesFilePath}'");

        var loadJobsTask = LoadJobs(jobsFilePath, cancellationToken);
        var buildRowsTask = BuildRows(cancellationToken);
        var loadFallbackRulesTask = LoadFallbackRules(rulesFilePath, cancellationToken);

        await Task.WhenAll(loadJobsTask, buildRowsTask, loadFallbackRulesTask);

        if (loadJobsTask.Result == null)
        {
            ConsoleUtil.ShowErrorAndExit($"Failed to load jobs from file {jobsFilePath.FullName}");
            return;
        }

        var jobMatcher = new JobMatcher(loadJobsTask.Result);
        var rows = buildRowsTask.Result;
        var fallbackRuleSet = loadFallbackRulesTask.Result;

        var fallbackRuleValidationErrors = JiraFallbackRuleEngine.Validate(fallbackRuleSet);
        if (fallbackRuleValidationErrors.Count > 0)
        {
            ConsoleUtil.ShowErrorAndExit(
                $"Fallback rule file '{rulesFilePath.FullName}' is invalid:{Environment.NewLine}{string.Join(Environment.NewLine, fallbackRuleValidationErrors.Select(e => $"- {e}"))}");
            return;
        }

        var fallbackRuleEngine = new JiraFallbackRuleEngine(fallbackRuleSet.Rules);

        var authStatusResult = await Acli.RunCommand("auth status");
        if (string.IsNullOrWhiteSpace(authStatusResult) ||
            authStatusResult.Contains("unauthorized", StringComparison.InvariantCultureIgnoreCase))
        {
            Console.WriteLine($"Initiating {Acli.AcliBinary} login procedure");
            var authLoginResult = await Acli.RunCommand("auth login");
            if (string.IsNullOrWhiteSpace(authLoginResult) ||
                !authLoginResult.Contains("Welcome, ", StringComparison.InvariantCultureIgnoreCase))
            {
                ConsoleUtil.ShowErrorAndExit($"Failed to authenticate {Acli.AcliBinary}");
                return;
            }
        }

        var cache =
            new ConcurrentDictionary<string, (string clientName, string projectName, string taskname, string issueType
                )>();

        Console.WriteLine("Enriching timelogs with JIRA information...");
        await Parallel.ForEachAsync(rows, cancellationToken,
            (row, token) => EnrichWithJiraInfo(row, jobMatcher, fallbackRuleEngine, cache, token));

        await SaveTimesheetFile(rows.AsParallel().AsOrdered(), cancellationToken);
        Console.WriteLine("Successfully finished enriching timelogs with JIRA information");
    }

    private static async ValueTask EnrichWithJiraInfo(TimesheetRow row, JobMatcher jobMatcher,
        JiraFallbackRuleEngine fallbackRuleEngine,
        ConcurrentDictionary<string, (string clientName, string projectName, string taskname, string issueType)> cache,
        CancellationToken cancellationToken)
    {
        var jiraNo = row.JiraNo.Trim();
        if (string.IsNullOrWhiteSpace(jiraNo) &&
            (string.IsNullOrWhiteSpace(row.TaskName) || row.TaskName == "ASK BOJAN"))
        {
            fallbackRuleEngine.TryApply(row, string.Empty);
            return;
        }

        var (projectName, taskName, issueType) = await GetZohoDataFromJiraRecursive(jiraNo, cache, cancellationToken);

        if (!string.IsNullOrWhiteSpace(projectName) && !string.IsNullOrWhiteSpace(taskName))
        {
            var job = jobMatcher.FindBest(projectName, taskName);

            row.ClientName = job.ClientName;
            row.ProjectName = job.ProjectName;
            row.TaskName = job.JobName;

            UpdateCache(cache, jiraNo, row.ClientName, row.ProjectName, row.TaskName, issueType ?? string.Empty);
            return;
        }

        if (!fallbackRuleEngine.TryApply(row, issueType)) SetAskBojan(row);

        UpdateCache(cache, jiraNo, row.ClientName, row.ProjectName, row.TaskName, issueType ?? string.Empty);
    }

    private static async Task<List<JobItem>?> LoadJobs(FileInfo jobsFilePath, CancellationToken cancellationToken)
    {
        await using var jobsJson = File.OpenRead(jobsFilePath.FullName);
        return await JsonSerializer.DeserializeAsync<List<JobItem>?>(jobsJson, GeneratedJsonContext.Default.ListJobItem,
            cancellationToken: cancellationToken);
    }

    private static async Task<JiraFallbackRuleSet> LoadFallbackRules(FileInfo rulesFilePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(rulesFilePath.FullName))
        {
            Console.WriteLine(
                $"Fallback rules file '{rulesFilePath.FullName}' not found. Continuing without external fallback rules.");
            return new JiraFallbackRuleSet();
        }

        await using var rulesJson = File.OpenRead(rulesFilePath.FullName);
        try
        {
            var ruleSet = await JsonSerializer.DeserializeAsync(rulesJson,
                GeneratedJsonContext.Default.JiraFallbackRuleSet,
                cancellationToken);
            if (ruleSet == null)
            {
                ConsoleUtil.ShowErrorAndExit($"Failed to load fallback rules from file {rulesFilePath.FullName}");
                return new JiraFallbackRuleSet();
            }

            return ruleSet;
        }
        catch (JsonException e)
        {
            ConsoleUtil.ShowErrorAndExit(
                $"Failed to parse fallback rules file '{rulesFilePath.FullName}': {e.Message}");
            return new JiraFallbackRuleSet();
        }
    }

    private static async Task<(string? projectName, string? taskName, string? issueType)> GetZohoDataFromJiraRecursive(
        string? jiraNo,
        ConcurrentDictionary<string, (string clientName, string projectName, string taskname, string issueType)> cache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jiraNo)) return (null, null, null);

        if (cache.TryGetValue(jiraNo, out var r)) return (r.projectName, r.taskname, r.issueType);

        var json = await Acli.RunCommand(
            $"jira workitem view {jiraNo} --json -f \"*all,customfield_13650,customfield_13651\"");
        using var jsonStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var doc = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        if (!root.TryGetProperty("fields", out var fields)) return (null, null, null);

        string? projectName = null;
        string? taskName = null;
        string? issueType = null;

        if (fields.TryGetProperty("customfield_13650", out var projField) &&
            projField.ValueKind == JsonValueKind.Array &&
            projField.GetArrayLength() > 0) projectName = projField[0].GetString();

        if (fields.TryGetProperty("customfield_13651", out var taskField) &&
            taskField.ValueKind == JsonValueKind.Array &&
            taskField.GetArrayLength() > 0) taskName = taskField[0].GetString();

        if (fields.TryGetProperty("issuetype", out var typeField) && typeField.TryGetProperty("name", out var nameProp))
            issueType = nameProp.GetString();

        if (!string.IsNullOrWhiteSpace(projectName) && !string.IsNullOrWhiteSpace(taskName))
            return (projectName, taskName, issueType);

        // Check parent
        if (fields.TryGetProperty("parent", out var parent) && parent.TryGetProperty("key", out var parentKeyProp))
        {
            var parentKey = parentKeyProp.GetString();
            var parentData = await GetZohoDataFromJiraRecursive(parentKey, cache, cancellationToken);

            // Use parent data if we didn't find it here
            projectName ??= parentData.projectName;
            taskName ??= parentData.taskName;
        }

        return (projectName, taskName, issueType);
    }

    private static void UpdateCache(
        ConcurrentDictionary<string, (string clientName, string projectName, string taskname, string issueType)> cache,
        string jiraNo, string clientName, string projectName, string taskName, string issueType)
    {
        //if any of the params is null return
        if (string.IsNullOrWhiteSpace(jiraNo)) return;
        if (string.IsNullOrWhiteSpace(clientName)) return;
        if (string.IsNullOrWhiteSpace(projectName)) return;
        if (string.IsNullOrWhiteSpace(issueType)) return;

        cache[jiraNo] = (clientName, projectName, taskName, issueType);
    }

    private static void SetAskBojan(TimesheetRow row)
    {
        row.ClientName = "ASK BOJAN";
        row.ProjectName = "ASK BOJAN";
        row.TaskName = "ASK BOJAN";
    }

    private static async
        Task<ConcurrentDictionary<string, (string clientName, string projectName, string taskname, string issueType)>>
        LoadJiraCache(string memoryFilePath, bool noCache, CancellationToken cancellationToken)
    {
        if (noCache || !File.Exists(memoryFilePath)) return [];

        var aCache =
            new ConcurrentDictionary<string, (string clientName, string projectName, string taskName, string issueType
                )>();

        await using var memFileStream = File.OpenRead(memoryFilePath);
        var memoryDict = await JsonSerializer
            .DeserializeAsync<Dictionary<string, Dictionary<string, Dictionary<string, List<JiraTicket>>>>>(
                memFileStream,
                GeneratedJsonContext.Default.DictionaryStringDictionaryStringDictionaryStringListJiraTicket,
                cancellationToken);
        if (memoryDict == null) return [];

        foreach (var (clientName, projects) in memoryDict)
        {
            foreach (var (projectName, tasks) in projects)
            {
                foreach (var (taskName, tickets) in tasks)
                {
                    foreach (var ticket in tickets)
                    {
                        aCache[ticket.JiraNo] = (clientName, projectName, taskName, ticket.IssueType);
                    }
                }
            }
        }

        return aCache;
    }

    private static async Task SaveJiraCache(
        ConcurrentDictionary<string, (string clientName, string projectName, string taskname, string issueType)> cache,
        string memFilePath,
        CancellationToken cancellationToken)
    {
        var memoryDict = new Dictionary<string, Dictionary<string, Dictionary<string, List<JiraTicket>>>>();

        foreach (var (ticket, val) in cache)
        {
            if (cancellationToken.IsCancellationRequested) return;

            if (memoryDict.TryGetValue(val.clientName, out var projects))
            {
                if (projects.TryGetValue(val.projectName, out var tasks))
                {
                    if (tasks.TryGetValue(val.taskname, out var tickets))
                    {
                        tickets.Add(new JiraTicket(ticket, val.issueType));
                    }
                    else
                    {
                        tasks.Add(val.taskname, [new JiraTicket(ticket, val.issueType)]);
                    }
                }
                else
                    projects.Add(val.projectName,
                        new Dictionary<string, List<JiraTicket>>
                            { { val.taskname, [new JiraTicket(ticket, val.issueType)] } });
            }
            else
                memoryDict.Add(val.clientName, new Dictionary<string, Dictionary<string, List<JiraTicket>>>
                {
                    {
                        val.projectName, new Dictionary<string, List<JiraTicket>>
                        {
                            {
                                val.taskname, [new JiraTicket(ticket, val.issueType)]
                            }
                        }
                    }
                });
        }

        await using var memFileStream = File.Create(memFilePath);
        await JsonSerializer.SerializeAsync(memFileStream, memoryDict,
            GeneratedJsonContext.Default.DictionaryStringDictionaryStringDictionaryStringListJiraTicket,
            cancellationToken);
    }
}