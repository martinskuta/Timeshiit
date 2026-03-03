using TimeshiitCli.Util;

namespace TimeshiitCli.Commands.Init;

public sealed class InitDependenciesCommand : CommandBase
{
    private const string AcliDownloadUrl = "https://acli.atlassian.com/windows/latest/acli_windows_amd64/acli.exe";

    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        Console.WriteLine($"Downloading Atlassian CLI from {AcliDownloadUrl}...");

        using var httpClient = new HttpClient();
        using var response =
            await httpClient.GetAsync(AcliDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), Acli.AcliBinary);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await responseStream.CopyToAsync(fileStream, cancellationToken);

        Console.WriteLine($"Downloaded {Acli.AcliBinary} to {outputPath}");
    }
}