using System.Globalization;
using System.Text.Json;
using nietras.SeparatedValues;
using TimeshiitCli.Data;
using TimeshiitCli.Util;
using TimeshiitCli.Util.Extensions;

namespace TimeshiitCli.Commands.Timelogs;

public abstract class TimelogsCommandBase(FileInfo timelogsCsvFilePath, string zohoDateFormat) : CommandBase
{
    private readonly string[] _expectedCsvHeaders =
    [
        //Date,"Client Name","Project Name","Task Name","Email ID","JIRA No.",Hour(s),Comment
        "Date",
        "\"Client Name\"",
        "\"Project Name\"",
        "\"Task Name\"",
        "\"Email ID\"",
        "\"JIRA No.\"",
        "Hour(s)",
        "Comment"
    ];

    private void VerifyTimelogsFileExists()
    {
        if (File.Exists(timelogsCsvFilePath.FullName)) return;

        ConsoleUtil.ShowErrorAndExit($"The csv file '{timelogsCsvFilePath}' does not exist");
    }

    protected async Task<List<TimesheetRow>> BuildRows(CancellationToken cancellationToken = default)
    {
        VerifyTimelogsFileExists();

        using var reader = new Sep(',')
            .Reader(o => o with
            {
                HasHeader = true,
                Trim = SepTrim.None,
                DisableColCountCheck = true
            })
            .FromFile(timelogsCsvFilePath.FullName);

        VerifyHeaders(reader);
        return reader.ParallelEnumerate(ParseRow).AsParallel().AsOrdered().ToList();
    }

    protected async Task SaveTimesheetFile(ParallelQuery<TimesheetRow> rows, CancellationToken cancellationToken)
    {
        File.Delete(timelogsCsvFilePath.FullName);

        await using var writer = new Sep(',').Writer(o => o with { WriteHeader = true })
            .ToFile(timelogsCsvFilePath.FullName);
        writer.Header.Add(_expectedCsvHeaders);

        foreach (var row in rows)
        {
            using var rowWriter = writer.NewRow(cancellationToken);
            rowWriter[0].Set(row.Date.ToStringInvariant(zohoDateFormat));
            rowWriter[1].Set(row.ClientName);
            rowWriter[2].Set(row.ProjectName);
            rowWriter[3].Set(row.TaskName);
            rowWriter[4].Set(row.EmailId);
            rowWriter[5].Set(row.JiraNo);
            rowWriter[6].Format(row.Hours);
            rowWriter[7].Set(row.Comment);
        }
    }

    private TimesheetRow ParseRow(SepReader.Row row)
    {
        if (row.ColCount < _expectedCsvHeaders.Length)
        {
            ConsoleUtil.ShowErrorAndExit(
                $"Invalid number of columns in row {row.RowIndex}, expected {_expectedCsvHeaders.Length} got fewer");
        }

        //Greedy consume the rest of columns.. it might be that there was a comma in the description forming the comment
        var comment = row.ColCount > _expectedCsvHeaders.Length ? row[7..].JoinToString(",") : row[7].ToString();

        return new TimesheetRow(
            ParseDate(row[0].Span),
            row[1].ToString(),
            row[2].ToString(),
            row[3].ToString(),
            row[4].ToString(),
            row[5].ToString(),
            double.Parse(row[6].Span, CultureInfo.InvariantCulture),
            comment);
    }

    protected virtual DateOnly ParseDate(ReadOnlySpan<char> date) =>
        DateOnly.ParseExact(date, zohoDateFormat, CultureInfo.InvariantCulture);

    private void VerifyHeaders(SepReader reader)
    {
        if (!reader.HasHeader) ConsoleUtil.ShowErrorAndExit("No header line found");

        var header = reader.Header;
        if (header.ColNames.Count != _expectedCsvHeaders.Length)
            ConsoleUtil.ShowErrorAndExit(
                $"Invalid number of columns, expected {_expectedCsvHeaders.Length} got {header.ColNames.Count}");

        for (var i = 0; i < _expectedCsvHeaders.Length; i++)
        {
            var expectedHeader = _expectedCsvHeaders[i];
            if (!header.TryIndexOf(expectedHeader, out var actualIndex))
            {
                ConsoleUtil.ShowErrorAndExit($"Invalid csv. Column {expectedHeader} not found");
                return;
            }

            if (i != actualIndex)
            {
                ConsoleUtil.ShowErrorAndExit($"Invalid csv. Column {expectedHeader} in wrong position");
                return;
            }
        }
    }

    protected async Task<Dictionary<DateOnly, List<LeaveInfo>>> BuildExclusionDates(List<TimesheetRow> rows,
        DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default)
    {
        var fromYear = fromDate?.Year ?? (rows.Count > 0 ? rows.Min(x => x.Date).Year : null);
        var toYear = toDate?.Year ?? (rows.Count > 0 ? rows.Max(x => x.Date).Year : null);

        if (fromYear is null || toYear is null)
        {
            Console.WriteLine("From and To dates could not be determined. Skipping cleanup of leaves and holidays");
            return new Dictionary<DateOnly, List<LeaveInfo>>();
        }

        var exclusionDates = new Dictionary<DateOnly, List<LeaveInfo>>();

        for (var year = fromYear; year <= toYear; year++)
        {
            if (timelogsCsvFilePath.DirectoryName == null) continue;

            var leavesFilePath = Path.Combine(timelogsCsvFilePath.DirectoryName, $"leaves_{year}.json");
            if (!File.Exists(leavesFilePath))
            {
                Console.WriteLine(
                    $"File {leavesFilePath} not found. Skipping. Use `init leaves' to create it to filter out holidays and leaves");
                continue;
            }

            await using var leaveFile = File.OpenRead(leavesFilePath);
            var leaveInfos = await JsonSerializer.DeserializeAsync<List<LeaveInfo>>(leaveFile,
                GeneratedJsonContext.Default.ListLeaveInfo, cancellationToken);

            foreach (var leaveInfo in leaveInfos!)
            {
                if (exclusionDates.TryGetValue(leaveInfo.Date, out var leaves))
                    leaves.Add(leaveInfo);
                else
                    exclusionDates.Add(leaveInfo.Date, [leaveInfo]);
            }
        }

        return exclusionDates;
    }
}