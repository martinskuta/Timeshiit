using TimeshiitCli.Util;

namespace TimeshiitCli.Commands.Init;

public sealed class InitDependenciesCommand : CommandBase
{
    private const string AcliDownloadUrl = "https://acli.atlassian.com/windows/latest/acli_windows_amd64/acli.exe";
    private const string ZcliDownloadUrl = "https://github.com/martinskuta/zohocli/releases/latest/download/zcli.exe";

    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        await DownloadBinary(httpClient, AcliDownloadUrl, Acli.AcliBinary, cancellationToken);
        await DownloadBinary(httpClient, ZcliDownloadUrl, ZohoCli.ZohoCliBinary, cancellationToken);
        Console.WriteLine("All dependencies downloaded successfully");
    }

    private static async Task DownloadBinary(HttpClient httpClient, string downloadUrl, string binaryName,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Downloading {binaryName} from {downloadUrl}...");

        using var response =
            await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), binaryName);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await responseStream.CopyToAsync(fileStream, cancellationToken);

        Console.WriteLine($"Downloaded {binaryName} to {outputPath}");
    }
}