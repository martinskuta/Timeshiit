namespace TimeshiitCli.Util;

public static class ZohoCli
{
    public const string ZohoCliBinary = "zcli.exe";

    public static Task<string> RunCommand(string command) =>
        ProcessHelper.RunCommand(ZohoCliBinary, command);
}