using Microsoft.EntityFrameworkCore;
using FileStoringService.Data; // пространство имён с классом StoredFile

public class FileStoringDbContext : DbContext
{
    public FileStoringDbContext(DbContextOptions<FileStoringDbContext> opt)
        : base(opt)
    {
    }

    // <-- вот это должно совпадать с именем в контроллере
    public DbSet<StoredFile> StoredFiles { get; set; }
}