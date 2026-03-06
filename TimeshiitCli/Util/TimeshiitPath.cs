namespace TimeshiitCli.Util;

public static class TimeshiitPath
{
    public static string ResolvePath(string path) =>
        File.Exists(path) ? path : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

    public static FileInfo ResolveFilePath(string fileName) => new(ResolvePath(fileName));
}