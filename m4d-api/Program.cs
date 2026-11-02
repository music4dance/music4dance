using Azure.Identity;
using Azure.Search.Documents;
using DanceLibrary;
using m4dApi;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// SEARCH_INDEX is an index alias (songs-api-test / songs-api-prod), so an index version cutover
// is an alias repoint, not a redeploy. See architecture/plans/partner-api-standalone-host.md.
var endpoint = new Uri(builder.Configuration.GetValue<string>("SEARCH_ENDPOINT"));
var indexName = builder.Configuration.GetValue<string>("SEARCH_INDEX");

builder.Services.AddSingleton(
    new SearchClient(endpoint, indexName, new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)));
builder.Services.AddSingleton(_ => Dances.Load(
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dances.json")),
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dancegroups.json"))));
builder.Services.AddSingleton<SongResolver>();

builder.Build().Run();
