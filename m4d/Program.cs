using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;

using m4d.Areas.Identity;
using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.Configuration;
using m4d.Utilities;
using m4d.ViewModels;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;
using Microsoft.Data.SqlClient;

using Newtonsoft.Json.Serialization;
using System.Reflection;
using Vite.AspNetCore;

using AzureSearchExtensions = Microsoft.Extensions.Azure.SearchClientBuilderExtensions;

// TODO: Figure out how to add a design time factory for the context https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli#from-a-design-time-factory
//  Or maybe implement https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli#from-application-services
//  Think that's working, but now have to figure out what is going on with UsageSummary and the aspnet fields that changed length

var startupTimer = System.Diagnostics.Stopwatch.StartNew();
var processStart = System.Diagnostics.Process.GetCurrentProcess().StartTime;
var timeSinceProcessStart = DateTime.Now - processStart;
var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Length;

Console.WriteLine($"[Process started {timeSinceProcessStart.TotalSeconds:F2}s ago]");
Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Entering Main");
Console.WriteLine($"[Loaded assemblies at Main entry: {loadedAssemblies}]");

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Created Builder");

// Smoke test mode - bypasses all Azure service configuration for container diagnostics
var smokeTestMode = builder.Configuration.GetValue<bool>("SMOKE_TEST_MODE");

Console.WriteLine($"SMOKE_TEST_MODE = {smokeTestMode}");

if (smokeTestMode)
{
    Console.WriteLine("⚠️  SMOKE TEST MODE ENABLED - Running minimal configuration");
    Console.WriteLine("Note: Azure automatically configures port binding via PORT/WEBSITES_PORT environment variables");

    var smokeApp = builder.Build();

    smokeApp.MapGet("/", () => Results.Content(
        $"""
        <!DOCTYPE html>
        <html>
        <head>
            <title>m4d Smoke Test</title>
            <meta charset="utf-8">
        </head>
        <body style="font-family: monospace; padding: 40px; background: #f0f0f0;">
            <h1 style="color: #28a745;">[OK] Container is Running</h1>
            <p><strong>Environment:</strong> {builder.Environment.EnvironmentName}</p>
            <p><strong>Time:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
            <p><strong>Mode:</strong> Smoke Test (bypassing Azure services)</p>
            <hr>
            <p><em>If you see this, the container and .NET runtime are working correctly.</em></p>
        </body>
        </html>
        """, "text/html"));

    smokeApp.MapGet("/health", () => Results.Json(new
    {
        status = "healthy",
        mode = "smoke-test",
        environment = builder.Environment.EnvironmentName,
        timestamp = DateTime.UtcNow
    }));

    Console.WriteLine("✓ Smoke test app configured, starting...");
    await smokeApp.RunAsync();
    return;
}

Console.WriteLine("Proceeding with normal startup - Finding connection string");

// Prioritize Service Connector environment variable (Azure), fall back to appsettings.json (local development)
var connectionString = builder.Configuration["AZURE_SQL_CONNECTIONSTRING"]
    ?? builder.Configuration.GetConnectionString("DanceMusicContextConnection");

if (!string.IsNullOrEmpty(connectionString))
{
    if (!string.IsNullOrEmpty(builder.Configuration["AZURE_SQL_CONNECTIONSTRING"]))
    {
        Console.WriteLine($"[Database] Using AZURE_SQL_CONNECTIONSTRING from Service Connector");
    }
    else
    {
        Console.WriteLine($"[Database] Using DanceMusicContextConnection from appsettings.json");
    }
}
else
{
    Console.WriteLine("[Database] WARNING: No connection string found - database will be unavailable");
    Console.WriteLine("[Database] Expected 'AZURE_SQL_CONNECTIONSTRING' (Azure Service Connector), 'DanceMusicContextConnection' (local), or PROD_DB + ProdConnectionString (user secrets)");
}

// When PROD_DB is set in Development, override connection string from ProdConnectionString
// (stored in user secrets via: dotnet user-secrets set ProdConnectionString "<connection-string>")
var useProdDb = builder.Configuration.GetValue<bool>("PROD_DB") && builder.Environment.IsDevelopment();
if (useProdDb)
{
    var prodConnectionString = builder.Configuration["ProdConnectionString"];
    if (!string.IsNullOrEmpty(prodConnectionString))
    {
        connectionString = prodConnectionString;
        Console.WriteLine("[Database] PROD_DB mode: Using ProdConnectionString from user secrets");
    }
    else
    {
        Console.WriteLine("[Database] WARNING: PROD_DB is set but ProdConnectionString is not configured");
        Console.WriteLine("[Database] Set it via: dotnet user-secrets set ProdConnectionString \"Server=<server>.database.windows.net,1433;Initial Catalog=<db>;Authentication=Active Directory Interactive;Encrypt=True;TrustServerCertificate=False\"");
    }
}
else if (builder.Configuration.GetValue<bool>("PROD_DB") && !builder.Environment.IsDevelopment())
{
    Console.WriteLine("[Database] WARNING: PROD_DB is set but ignored outside Development environment");
}

// When TEST_DB is set in Development, override connection string from TestConnectionString
// (stored in user secrets via: dotnet user-secrets set TestConnectionString "<connection-string>")
var testDbRequested = builder.Configuration.GetValue<bool>("TEST_DB") && builder.Environment.IsDevelopment();
var testConnectionString = builder.Configuration["TestConnectionString"];
var useTestDb = testDbRequested && !string.IsNullOrEmpty(testConnectionString);
if (useTestDb)
{
    connectionString = testConnectionString;
    Console.WriteLine("[Database] TEST_DB mode: Using TestConnectionString from user secrets");
}
else if (testDbRequested)
{
    Console.WriteLine("[Database] WARNING: TEST_DB is set but TestConnectionString is not configured");
    Console.WriteLine("[Database] TEST_DB mode has been disabled; using the normal connection string instead");
    Console.WriteLine("[Database] Set it via: dotnet user-secrets set TestConnectionString \"Server=<server>.database.windows.net,1433;Initial Catalog=<db>;Authentication=Active Directory Interactive;Encrypt=True;TrustServerCertificate=False\"");
}
else if (builder.Configuration.GetValue<bool>("TEST_DB") && !builder.Environment.IsDevelopment())
{
    Console.WriteLine("[Database] WARNING: TEST_DB is set but ignored outside Development environment");
}

builder.AddM4dApplication(connectionString);

Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Building application...");
WebApplication app;
try
{
    app = builder.Build();
    Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Application built successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: Application failed to build: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine("This may indicate a critical service configuration issue that prevents dependency injection.");
    Console.WriteLine("Common causes:");
    Console.WriteLine("  - Invalid Azure Search endpoint configuration");
    Console.WriteLine("  - Missing required service dependencies");
    Console.WriteLine("  - Service registration conflicts");
    Console.WriteLine();
    Console.WriteLine("To resolve:");
    Console.WriteLine("  1. Check appsettings.Development.json for invalid endpoints");
    Console.WriteLine("  2. Verify all required configuration values are present");
    Console.WriteLine("  3. Check the startup health report above for failed services");
    Console.WriteLine();
    Console.WriteLine("Full exception details:");
    Console.WriteLine(ex);
    throw; // Re-throw to stop the application
}

await app.UseM4dPipeline(connectionString, useProdDb, useTestDb);

app.Run();
