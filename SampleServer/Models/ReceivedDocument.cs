using System.Text.Json;

namespace SampleServer.Models;

public class ReceivedDocument
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public JsonDocument? MlResponse { get; set; }
    public State ProcessingState { get; set; }
    public DateTimeOffset ReceivedTime { get; set; }
    public enum State
    {
        Undefined,
        Pending,
        Processing,
        Completed,
    }
}