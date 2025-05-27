using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SampleServer;
using SampleServer.Models;

var builder = WebApplication.CreateBuilder(args);
await CreateDatabase(builder.Configuration.GetConnectionString("PostgresDb")!);
builder.Services.AddHttpClient<IMlService, MlService>(opts =>
{
    opts.BaseAddress = new Uri(builder.Configuration.GetConnectionString("MlService")!);
});
builder.Services.AddScoped<ISendToMlJob, SendToMlJob>();
builder.Services.AddDbContext<AppDbContext>(opts =>
{
    opts.UseNpgsql(builder.Configuration.GetConnectionString("PostgresDb"));
    opts.UseSnakeCaseNamingConvention();
});
builder.Services.AddHangfireServer();
builder.Services.AddHostedService<MigrateDb<AppDbContext>>();
builder.Services.AddHangfire(opts =>
{
    opts.UsePostgreSqlStorage(sqlOpts =>
        sqlOpts.UseNpgsqlConnection(builder.Configuration.GetConnectionString("PostgresDb")));
});
var dir = Directory.CreateTempSubdirectory().FullName;
builder.Services.AddCors();
var app = builder.Build();
app.UseCors(opts => opts.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
var api = app.MapGroup("api");
api.MapPost("uploadFile", async ([FromForm] IFormFile file, [FromServices] AppDbContext db, [FromServices] IBackgroundJobClient backgroundJobClient) =>
{
    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Seek(0, SeekOrigin.Begin);
    
    var fileId = UUIDNext.Uuid.NewSequential();
    var filePath = Path.Combine(dir, $"{file.FileName}");
    await File.WriteAllBytesAsync(filePath, stream.ToArray());

    var doc = new ReceivedDocument()
    {
        Id = fileId,
        ProcessingState = ReceivedDocument.State.Pending,
        Title = file.FileName,
        ReceivedTime = DateTimeOffset.UtcNow,
    };
    db.ReceivedDocuments.Add(doc);
    await db.SaveChangesAsync();
    backgroundJobClient.Enqueue<ISendToMlJob>(j => j.SendToMlAsync(fileId));
    return Results.Ok();
}).DisableAntiforgery();
api.MapPost("uploadText", async ([FromBody] UploadTextRequest request, [FromServices] AppDbContext db,
    [FromServices] IBackgroundJobClient backgroundJobClient) =>
{
    var fileId = UUIDNext.Uuid.NewSequential();
    var filePath = Path.Combine(dir, "uploads", $"{request.Title}.txt");
    await File.WriteAllTextAsync(filePath, request.Text);
    var doc = new ReceivedDocument()
    {
        Id = fileId,
        ProcessingState = ReceivedDocument.State.Pending,
        Title = $"{request.Title}.txt",
        ReceivedTime = DateTimeOffset.UtcNow,
    };
    db.ReceivedDocuments.Add(doc);
    await db.SaveChangesAsync();
    backgroundJobClient.Enqueue<ISendToMlJob>(j => j.SendToMlAsync(fileId));
    return Results.Json(new {Id = doc.Id});
});
api.MapGet("listFiles", async (AppDbContext db) =>
{
    var files = await db.ReceivedDocuments
        .OrderByDescending(f => f.ReceivedTime)
        .Select(e => new
        {
            e.Title,
            State = e.ProcessingState,
            DateTime = e.ReceivedTime,
            ModelResponse = e.MlResponse
        })
        .ToArrayAsync();
    return Results.Json(new { files = files });
});
api.MapPost("uploadFiles", async (HttpContext ctx, [FromServices] AppDbContext db, [FromServices] IBackgroundJobClient backgroundJobClient) =>
{
    List<Guid> fileIds = [];
    foreach (var file in ctx.Request.Form.Files)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Seek(0, SeekOrigin.Begin);
        
        var fileId = UUIDNext.Uuid.NewSequential();
        var filePath = Path.Combine(dir, $"{file.FileName}");
        await File.WriteAllBytesAsync(filePath, stream.ToArray());

        var doc = new ReceivedDocument()
        {
            Id = fileId,
            ProcessingState = ReceivedDocument.State.Pending,
            Title = file.FileName,
            ReceivedTime = DateTimeOffset.UtcNow,
        };
        db.ReceivedDocuments.Add(doc);
        fileIds.Add(doc.Id);
    }
    fileIds.ForEach(fileId => backgroundJobClient.Enqueue<ISendToMlJob>(j => j.SendToMlAsync(fileId)));
    await db.SaveChangesAsync();
    return Results.Json(new {Ids = fileIds.ToArray()});
}).DisableAntiforgery();
api.MapPost("uploadByLink", async ([FromBody] UploadLinkRequest request, [FromServices] AppDbContext db, [FromServices] IBackgroundJobClient backgroundJobClient) =>
{
    try
    {
        var client = new HttpClient();
        var files = await client.GetAsync(request.Url);
        var fileTitle = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd-hh-mm-ss",CultureInfo.InvariantCulture);
        switch (files.Content.Headers.ContentType?.ToString())
        {
            case "text/plain":
                fileTitle += ".txt";
                break;
            case "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
                fileTitle += ".docx";
                break;
            case "application/pdf":
                fileTitle += ".pdf";
                break;
            default:
                return Results.BadRequest("Unsupported file type");
        }
        await using var fileStream = File.OpenWrite(Path.Combine(dir, fileTitle));
        await files.Content.CopyToAsync(fileStream);
        
        var fileId = UUIDNext.Uuid.NewSequential();
        var doc = new ReceivedDocument()
        {
            Id = fileId,
            ProcessingState = ReceivedDocument.State.Pending,
            Title = fileTitle,
            ReceivedTime = DateTimeOffset.UtcNow,
        };
        db.ReceivedDocuments.Add(doc);
        await db.SaveChangesAsync();
        backgroundJobClient.Enqueue<ISendToMlJob>(j => j.SendToMlAsync(fileId));
        return Results.Json(new {Id = doc.Id});
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
        return Results.BadRequest("Cannot download file");
    }
});
app.Run();

async Task CreateDatabase(string connectionString)
{
    var hostSt = Regex.Match(connectionString, "Host=(?<target>[^;]*);").Groups[0].Value;
    var portSt = Regex.Match(connectionString, "Port=(?<target>[^;]*);").Groups[0].Value;
    var usernameSt = Regex.Match(connectionString, "Username=(?<target>[^;]*);").Groups[0].Value;
    var passwordSt = Regex.Match(connectionString, "Password=(?<target>[^;]*);").Groups[0].Value;
    var database = Regex.Match(connectionString, "Database=(?<target>[^;]*);").Groups["target"].Value;
    await using var connection = new NpgsqlConnection($"{hostSt}{portSt}{usernameSt}{passwordSt}");
    var command = new NpgsqlCommand($"CREATE DATABASE {database};", connection);
    await connection.OpenAsync();
    try
    {
        await command.ExecuteNonQueryAsync();
    }
    catch (NpgsqlException e)
    {
        if (e.ErrorCode != -2147467259) // База уже создана
        {
            throw;
        }
    }
    finally
    {
        await connection.CloseAsync();
    }
}

public class UploadTextRequest
{
    public required string Text { get; set; }
    public required string Title { get; set; }
}

public class UploadLinkRequest
{
    public required string Url { get; set; }
}

