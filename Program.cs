using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

string dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
string dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
string dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "user";
string dbPass = Environment.GetEnvironmentVariable("DB_PASS") ?? "password";
string dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "db";
string connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass};";

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();

    var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();
    var command = connection.CreateCommand();

    command.CommandText = @"
        SELECT EXISTS (
            SELECT FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'entries'
        );";
    var exists = (bool)await command.ExecuteScalarAsync();
    if (!exists)
    {
        command.CommandText = "CREATE TABLE public.\"entries\" (\"Id\" SERIAL PRIMARY KEY, \"Data\" TEXT NOT NULL);";
        await command.ExecuteNonQueryAsync();
    }
}

app.MapGet("/", async (AppDbContext dbContext) =>
{
    var randomData = Guid.NewGuid().ToString();
    dbContext.Entries.Add(new Entry { Data = randomData });
    await dbContext.SaveChangesAsync();

    var count = await dbContext.Entries.CountAsync();
    return Results.Ok(new { Message = "Entry added successfully with random data.", Data = randomData, Count = count });
});

app.MapGet("/status", () => Results.Ok("UP"));

app.Run();
