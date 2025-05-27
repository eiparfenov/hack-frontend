using Microsoft.EntityFrameworkCore;
using SampleServer.Models;

namespace SampleServer;

public class AppDbContext: DbContext
{
    public AppDbContext(DbContextOptions options):base(options){}
    public required DbSet<ReceivedDocument> ReceivedDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReceivedDocument>(builder =>
        {
            builder.ToTable("received_documents");
            builder.Property(e => e.Id).ValueGeneratedNever();
        });
    }
}