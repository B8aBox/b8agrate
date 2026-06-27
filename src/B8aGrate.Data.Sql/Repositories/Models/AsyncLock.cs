namespace B8aGrate.Data.Sql.Repositories.Models;

internal sealed class AsyncLock(Func<Task> release) : IDisposable
{
    public void Dispose() => release().GetAwaiter().GetResult();
}