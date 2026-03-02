namespace TimeshiitCli.Commands;
public abstract class CommandBase
{
    public Task Execute(CancellationToken cancellationToken = default)
    {
        try
        {
            return ExecuteInternal(cancellationToken);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"⛔ Unexpected error happened: {e}");
            Environment.Exit(1);
            return Task.CompletedTask;
        }
    }
    
    protected abstract Task ExecuteInternal(CancellationToken cancellationToken);
}
