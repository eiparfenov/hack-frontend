using Hangfire;
using Microsoft.EntityFrameworkCore;
using SampleServer.Models;

namespace SampleServer;

public interface ISendToMlJob
{
    [DisableConcurrentExecution(120)]
    Task SendToMlAsync(Guid documentId);
}

[DisableConcurrentExecution(120)]
public class SendToMlJob(AppDbContext db, IMlService mlService) : ISendToMlJob
{
    [DisableConcurrentExecution(120)]
    public async Task SendToMlAsync(Guid documentId)
    {
        var doc = await db.ReceivedDocuments.SingleAsync(d => d.Id == documentId);
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", doc.Title.ToString());
        doc.ProcessingState = ReceivedDocument.State.Processing;
        await db.SaveChangesAsync();
        
        var jsonDocument = await mlService.PreformMlAsync(filePath);
        doc.MlResponse = jsonDocument;
        doc.ProcessingState = ReceivedDocument.State.Completed;
        await db.SaveChangesAsync();
    }
}
