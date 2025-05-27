using System.Text.Json;

namespace SampleServer;

public interface IMlService
{
    Task<JsonDocument> PreformMlAsync(string filePath);
}

public class MockMlService(): IMlService
{
    public async Task<JsonDocument> PreformMlAsync(string filePath)
    {
        await Task.Delay(TimeSpan.FromSeconds(15));
        return JsonDocument.Parse("{}");
    }
}


public class MlService(HttpClient client) : IMlService
{
    public async Task<JsonDocument> PreformMlAsync(string filePath)
    {
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(fileBytes), "files", filePath.Split('/').Last());
        var httpResponse = await client.PostAsync("candidate_match", content);
        var doc = await httpResponse.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!;
    }
}