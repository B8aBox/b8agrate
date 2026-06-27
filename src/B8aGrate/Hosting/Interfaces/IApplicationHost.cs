namespace B8aGrate.Hosting.Interfaces;

public interface IApplicationHost
{
    Task<int> RunAsync(string[] args);
}