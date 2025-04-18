using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();
var app = builder.Build();
app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.MapPost("file", async ([FromForm] IFormFile files) =>
{
    using var fileStream = files.OpenReadStream();
    using var streamReader = new StreamReader(fileStream);
    var fileContent = await streamReader.ReadToEndAsync();
    await Task.Delay(TimeSpan.FromSeconds(1));
    return new
    {
        vacancy = "АНАЛИТИК ДАННЫХ (DATA SCIENTIST, ML ENGINEER)",
        percentage = 65,
        explaining = "ОбъяснениеОбъясне ниеОбъяснение ОбъяснениеОбъяснен иеОбъяснениеОбъяс нениеОбъяснениеОбъясн ениеОбъяснени е",
        recommendations = "Рекомендации Рекомендации Рекомендации Рекомендации Рекомендации Рекомендации Рекомендации Рекомендации Рекомендации Рекомендации "
    };
}).DisableAntiforgery();
app.MapGet("/", () => "Hello World!");

app.Run();