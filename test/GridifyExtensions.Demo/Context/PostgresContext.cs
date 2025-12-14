using GridifyExtensions.Demo.Domain;
using Microsoft.EntityFrameworkCore;

namespace GridifyExtensions.Demo.Context;

public class PostgresContext(DbContextOptions<PostgresContext> options) : DbContext(options)
{
   public DbSet<Estate> Estates => Set<Estate>();
   public DbSet<Building> Buildings => Set<Building>();
   public DbSet<Partner> Partners => Set<Partner>();
   public DbSet<EstateOwnerAssignment> EstateOwnerAssignments => Set<EstateOwnerAssignment>();
   public DbSet<Tag> Tags => Set<Tag>();

   protected override void OnModelCreating(ModelBuilder b)
   {
      b.Entity<Partner>(e =>
      {
         e.Property(x => x.FullName)
          .HasMaxLength(200);
      });

      b.Entity<Building>(e =>
      {
         e.Property(x => x.Address)
          .HasMaxLength(300);
         e.HasOne(x => x.Partner)
          .WithMany()
          .HasForeignKey(x => x.PartnerId);
      });

      b.Entity<Estate>(e =>
      {
         e.Property(x => x.Comment)
          .HasMaxLength(500);

         e.HasOne(x => x.Building)
          .WithMany(x => x.Estates)
          .HasForeignKey(x => x.BuildingId);

         // N:M (skip navigation)
         e.HasMany(x => x.Tags)
          .WithMany(x => x.Estates)
          .UsingEntity(j => j.ToTable("estate_tags"));
      });

      b.Entity<EstateOwnerAssignment>(e =>
      {
         e.HasOne(x => x.Estate)
          .WithMany(x => x.EstateOwnerAssignments)
          .HasForeignKey(x => x.EstateId);
         e.HasOne(x => x.Partner)
          .WithMany()
          .HasForeignKey(x => x.PartnerId);

         // Helps the demo reproduce/avoid slow plans:
         e.HasIndex(x => new
         {
            x.EstateId,
            x.IsPrimary,
            x.EndDate,
            x.Deleted
         });
      });

      b.Entity<Tag>(e =>
      {
         e.Property(x => x.Name)
          .HasMaxLength(80);
         e.HasIndex(x => x.Name)
          .IsUnique();
      });
   }
}