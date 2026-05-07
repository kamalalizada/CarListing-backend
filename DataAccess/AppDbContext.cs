using Entry.Concrete;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Entry.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Car> Cars => Set<Car>();
        public DbSet<CarImage> CarImages => Set<CarImage>();
        public DbSet<CarFeature> CarFeatures => Set<CarFeature>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
                entity.Property(u => u.Email).HasMaxLength(100).IsRequired();
                entity.Property(u => u.Role).HasMaxLength(20).IsRequired();
            });

          
            modelBuilder.Entity<Car>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Title).HasMaxLength(200).IsRequired();
                entity.Property(c => c.Brand).HasMaxLength(50).IsRequired();
                entity.Property(c => c.Model).HasMaxLength(50).IsRequired();
                entity.Property(c => c.Price)
                      .HasPrecision(18, 2)
                      .IsRequired();

                entity.HasOne(c => c.User)
                      .WithMany()
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(c => c.CreatedAt);
            });

          
            modelBuilder.Entity<CarImage>(entity =>
            {
                entity.HasKey(i => i.Id);
                entity.Property(i => i.ImageUrl).HasMaxLength(500).IsRequired();
                entity.Property(i => i.ObjectKey).HasMaxLength(255);
                entity.HasOne(i => i.Car)
                      .WithMany(c => c.Images)
                      .HasForeignKey(i => i.CarId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(i => i.CarId);
            });

            
            modelBuilder.Entity<CarFeature>(entity =>
            {
                entity.HasKey(f => f.Id);
                entity.Property(f => f.Key).HasMaxLength(100).IsRequired();
                entity.Property(f => f.Value).HasMaxLength(200).IsRequired();

                entity.HasOne(f => f.Car)
                      .WithMany(c => c.Features)
                      .HasForeignKey(f => f.CarId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(f => f.CarId);
            });
        }
    }
}
