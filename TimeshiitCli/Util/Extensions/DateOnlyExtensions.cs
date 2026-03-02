using System.Globalization;

namespace TimeshiitCli.Util.Extensions;

public static class DateOnlyExtensions
{
    extension(DateOnly date)
    {
        public string ToStringInvariant(string format)
        {
            return date.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}