using B8aGrate.Hosting;
using B8aGrate.Hosting.Interfaces;
using B8aGrate.Infrastructure.Configuration.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddMapster();
    builder.Services.AddMediatR();
    builder.Services.AddDataRepositories();
    builder.Services.AddDataServices();
    builder.Services.AddResultRendering();

    builder.Services.AddScoped<IApplicationHost, ApplicationHost>();

    using var host = builder.Build();
    using var scope = host.Services.CreateScope();

    var app = scope.ServiceProvider.GetRequiredService<IApplicationHost>();

    return await app.RunAsync(args);
}
catch (Exception exception)
{
    if (args.Contains("--debug", StringComparer.OrdinalIgnoreCase))
        Console.Error.WriteLine(exception);
    else
        Console.Error.WriteLine($"ERROR: {exception.Message}");

    return 1;
}