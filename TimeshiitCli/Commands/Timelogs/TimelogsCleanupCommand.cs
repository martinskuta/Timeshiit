using System.Globalization;

namespace TimeshiitCli.Commands.Timelogs;

public sealed class TimelogsCleanupCommand(
    FileInfo timelogsCsvFilePath,
    string zohoDateFormat,
    DateOnly? fromDate,
    DateOnly? toDate) : TimelogsCommandBase(timelogsCsvFilePath, zohoDateFormat)
{
    //this is what the AIs were generating for dates
    private static readonly string[] DateFormatSlops =
    [
        "dd/MM/yyyy", "M/d/yy", "d.M.yyyy", "dd.MMM.yy", "dd.MMMM.yy", "dd-MMM-yy", "dd-MMMM-yy", "dd-MMM-yyyy",
        "dd-MMMM-yyyy"
    ];

    private readonly FileInfo _timelogsCsvFilePath = timelogsCsvFilePath;
    private readonly string _zohoDateFormat = zohoDateFormat;

    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        Console.WriteLine($"Starting cleanup of {_timelogsCsvFilePath.Name}. Parsing csv file...");

        var rows = await BuildRows(cancellationToken);
        var exclusionDates = await BuildExclusionDates(rows, fromDate, toDate, cancellationToken);

        Console.WriteLine("Removing entries for staurday, sundays, leaves and holidays");
        //remove rows for holidays and leaves
        var filtered = rows.AsParallel().Where(row =>
        {
            var date = row.Date;
            if (date < fromDate || date > toDate) return false;

            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
            return !exclusionDates.TryGetValue(date, out var leaveInfos) || leaveInfos.Sum(l => l.Hours) < 8;
        }).OrderBy(row => row.Date);

        await SaveTimesheetFile(filtered, cancellationToken);

        Console.WriteLine("Cleanup finished.");
    }

    protected override DateOnly ParseDate(ReadOnlySpan<char> date)
    {
        //Trying to handle AI slop generated dates
        return DateOnly.ParseExact(date, DateFormatSlops, CultureInfo.InvariantCulture);
    }
}