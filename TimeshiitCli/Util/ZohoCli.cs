using System.Diagnostics;

namespace TimeshiitCli.Util;

public static class ZohoCli
{
    public const string ZohoCliBinary = "ZohoCLI.exe";

    public static Task<string> RunCommand(string command) =>
        ProcessHelper.RunCommand(ZohoCliBinary, command);
}