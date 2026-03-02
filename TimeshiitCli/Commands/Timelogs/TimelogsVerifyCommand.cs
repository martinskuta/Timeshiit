using TimeshiitCli.Util;

namespace TimeshiitCli.Commands.Timelogs;

public sealed class TimelogsVerifyCommand(FileInfo timelogsCsvFilePath, string zohoDateFormat)
    : TimelogsCommandBase(timelogsCsvFilePath, zohoDateFormat)
{
    private readonly FileInfo _timelogsCsvFilePath = timelogsCsvFilePath;

    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        var rowList = await BuildRows(cancellationToken);

        if (rowList.Count == 0)
        {
            ConsoleUtil.ShowErrorAndExit("The timelogs CSV file is empty");
            return;
        }

        var exclusionDates = await BuildExclusionDates(rowList, cancellationToken: cancellationToken);
        var dayHoursSum = rowList.GroupBy(row => row.Date).ToDictionary(x => x.Key, x => x.Sum(y => y.Hours));
        var fromDate = rowList.Min(x => x.Date);
        var toDate = rowList.Max(x => x.Date);

        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            var isHolidayOrLeave = exclusionDates.TryGetValue(date, out var leaveInfos);
            var expectedHours = isHolidayOrLeave ? 8 - Math.Abs(leaveInfos!.Sum(l => l.Hours)) : 8;

            if (isHolidayOrLeave) continue;

            if (!dayHoursSum.TryGetValue(date, out var hours) && !isHolidayOrLeave)
            {
                ConsoleUtil.ShowErrorAndExit($"No hours found for {date.Day}/{date.Month}/{date.Year}");
                continue;
            }

            if (hours < expectedHours)
            {
                ConsoleUtil.ShowErrorAndExit(
                    $"{date.Day}/{date.Month}/{date.Year} is expected to have logged {expectedHours} but has only {hours} logged");
                return;
            }

            if (hours > expectedHours)
            {
                ConsoleUtil.ShowErrorAndExit(
                    $"{date.Day}/{date.Month}/{date.Year} has more hours than expected - overtime not allowed. Expected {expectedHours} but has {hours} logged");
                return;
            }
        }

        Console.WriteLine($"'{_timelogsCsvFilePath.FullName}'  was successfully verified");
    }
}