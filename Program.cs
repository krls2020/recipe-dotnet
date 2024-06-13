using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Přidání logování
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

string dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
string dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
string dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "user";
string dbPass = Environment.GetEnvironmentVariable("DB_PASS") ?? "password";
string dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "db";
string connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass};";

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

// Vytvoření logovací služby
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();

// Ukázkové logování všech úrovní
logger.LogTrace("This is a trace message");
logger.LogDebug("This is a debug message");
logger.LogInformation("This is an info message");
logger.LogWarning("This is a warning message");
logger.LogError("This is an error message");
logger.LogCritical("This is a critical message");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    logger.LogInformation("Checking database connection...");

    try
    {
        dbContext.Database.EnsureCreated();
        logger.LogInformation("Database ensured to be created.");

        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        logger.LogInformation("Database connection opened.");

        var command = connection.CreateCommand();

        command.CommandText = @"
            SELECT EXISTS (
                SELECT FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'entries'
            );";
        var exists = (bool)await command.ExecuteScalarAsync();
        logger.LogDebug("Check if 'entries' table exists: {exists}", exists);

        if (!exists)
        {
            command.CommandText = "CREATE TABLE public.\"entries\" (\"Id\" SERIAL PRIMARY KEY, \"Data\" TEXT NOT NULL);";
            await command.ExecuteNonQueryAsync();
            logger.LogInformation("Table 'entries' created.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while setting up the database.");
    }
}

app.MapGet("/", async (AppDbContext dbContext, ILogger<Program> logger) =>
{
    logger.LogInformation("Handling request for '/' endpoint.");

    try
    {
        var randomData = Guid.NewGuid().ToString();
        dbContext.Entries.Add(new Entry { Data = randomData });
        await dbContext.SaveChangesAsync();

        var count = await dbContext.Entries.CountAsync();
        logger.LogDebug("New entry added. Total count: {count}", count);

        return Results.Ok(new { Message = "Entry added successfully with random data.", Data = randomData, Count = count });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while handling the request.");
        return Results.Problem("An error occurred while processing your request.");
    }
});

app.MapGet("/status", (ILogger<Program> logger) =>
{
    logger.LogInformation("Handling request for '/status' endpoint.");
    return Results.Ok(new { status = "UP" });
});

app.Run();
