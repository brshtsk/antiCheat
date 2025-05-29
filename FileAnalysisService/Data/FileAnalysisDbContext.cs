using FileAnalysisService.Results;
using Microsoft.EntityFrameworkCore;

namespace FileAnalysisService.Data;

public class FileAnalysisDbContext : DbContext
{
    public FileAnalysisDbContext(DbContextOptions<FileAnalysisDbContext> options)
        : base(options)
    {
    }

    public DbSet<AnalysisResult> AnalysisResults { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AnalysisResult>()
            .HasIndex(ar => ar.FileId);
        modelBuilder.Entity<AnalysisResult>()
            .Property(ar => ar.Status)
            .HasConversion<string>();
    }
}