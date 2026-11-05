using Azure.Data.Tables;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Search.Documents;
using DanceLibrary;
using m4dApi;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// The Application Insights SDK adds a rule that drops everything below Warning, which hid the
// per-query Search timings and key refreshes. Remove it so host.json's levels apply.
builder.Logging.Services.Configure<LoggerFilterOptions>(options =>
{
    var rule = options.Rules.FirstOrDefault(r =>
        r.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
    if (rule != null)
    {
        _ = options.Rules.Remove(rule);
    }
});

// SEARCH_INDEX is an index alias (songs-api-test / songs-api-prod), so an index version cutover
// is an alias repoint, not a redeploy. See architecture/plans/partner-api-standalone-host.md.
var configuration = builder.Configuration;
var endpoint = new Uri(configuration.GetValue<string>("SEARCH_ENDPOINT"));
var indexName = configuration.GetValue<string>("SEARCH_INDEX");
var credential = new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);

builder.Services.AddSingleton(new SearchClient(endpoint, indexName, credential));
builder.Services.AddSingleton(_ => Dances.Load(
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dances.json")),
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dancegroups.json"))));
builder.Services.AddSingleton<SongResolver>();

// Partner keys: hashes and client limits from the partner-keys vault, cached and refreshed every
// 5 minutes. Usage goes to a table in the function's own storage account.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IKeySource>(new VaultKeySource(
    new SecretClient(new Uri(configuration.GetValue<string>("PARTNER_KEYS_VAULT_URI")), credential)));
builder.Services.AddSingleton<PartnerKeyStore>();
builder.Services.AddHostedService<PartnerKeyRefresher>();
builder.Services.AddSingleton<IUsageTable>(new StorageUsageTable(new TableClient(
    new Uri($"https://{configuration.GetValue<string>("AzureWebJobsStorage:accountName")}.table.core.windows.net"),
    "usage", credential)));
builder.Services.AddSingleton<UsageMeter>();
builder.Services.AddHostedService<UsageFlusher>();
builder.Services.AddSingleton<ClientRateLimiter>();
builder.Services.AddSingleton(new PartnerDefaults(
    configuration.GetValue("PARTNER_DEFAULT_RATE", 20.0),
    configuration.GetValue("PARTNER_DEFAULT_BURST", 50),
    configuration.GetValue("PARTNER_DEFAULT_DAILY_CAP", 100_000L)));
builder.Services.AddSingleton<PartnerGate>();

builder.Build().Run();
