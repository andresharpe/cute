using Microsoft.EntityFrameworkCore;

namespace Buttercup.Core.Services.Implementation;

public class ButtercupDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ContentTypeEntity> ContentTypes { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ContentTypeEntity and other entities
        modelBuilder.Entity<ContentTypeEntity>(entity =>
        {
            entity.ToTable("ContentTypes");
            entity.HasKey(e => e.Id);
            // Add other configurations
        });

        // Configure other entity mappings
    }
}