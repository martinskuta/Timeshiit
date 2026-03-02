using System.Diagnostics;

namespace TimeshiitCli.Util;

public static class Acli
{
    public const string AcliBinary = "acli.exe";
    
    public static Task<string> RunCommand(string command) => ProcessHelper.RunCommand(AcliBinary, command);
        
}