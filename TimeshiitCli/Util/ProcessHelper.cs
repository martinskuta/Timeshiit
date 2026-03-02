using System.Diagnostics;

namespace TimeshiitCli.Util;

public static class ProcessHelper
{
    public static async Task<string> RunCommand(string binary, string command)
    {
        VerifyZohoCliIsPresent(binary);
        
        var processInfo = new ProcessStartInfo
        {
            FileName = binary,
            Arguments = $"{command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process == null)
        {
            ConsoleUtil.ShowErrorAndExit($"Failed to start {processInfo.FileName} process");
        }

        var output = await process!.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            ConsoleUtil.ShowErrorAndExit($"'{processInfo.FileName} {processInfo.Arguments}' command failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }

    private static void VerifyZohoCliIsPresent(string binary)
    {
        if(!File.Exists(binary)) ConsoleUtil.ShowErrorAndExit($"{binary} not found! Please make sure the binary exists.");
    }
}