namespace TimeshiitCli.Util;

public static class ConsoleUtil
{
    public static void ShowErrorAndExit(string message)
    {
        Console.Error.WriteLine(message);
        Environment.Exit(1);
    }
}