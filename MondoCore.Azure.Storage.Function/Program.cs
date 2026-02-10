using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MondoCore.Azure.ApplicationInsights;
using MondoCore.MongoDB;
using MondoCore.Azure.Storage.Function;
using MondoCore.Data;
using MondoCore.Log;
using MondoCore.Common;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
        .AddSingleton<ILog>( (p)=> 
        {
            ILog log;

            // Use GetService to get the TelemetryConfiguration to share with the host
            var config = new TelemetryConfiguration { ConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING") };
            log = new ApplicationInsights(config);

            return log!;
        })
        .AddScoped<IDatabase>( p=>
        {
            return new MondoCore.MongoDB.MongoDB("functionaltests", "mongodb://localhost:27017/");
        })
        .AddScoped<IPersonRepository, PersonRepository>()
        .AddScoped<IBlobStore<Person>>( p=>
        {
            return new MondoCore.Azure.Storage.AzureAppendBlobStorage<Person>(Environment.GetEnvironmentVariable("StorageAccount")!, "functionaltest");
        })
        .AddScoped<IStorageTestService, StorageTestService>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService();
    //.ConfigureFunctionsApplicationInsights();

builder.Build().Run();
