using Microsoft.EntityFrameworkCore;

namespace RadioFloorController.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<FloorGrantEntity> FloorGrants => Set<FloorGrantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FloorGrantEntity>(entity =>
        {
            entity.ToTable("FloorGrants");
            entity.HasKey(e => e.GroupId);
            entity.Property(e => e.GroupId).IsRequired();
        });
    }
}
