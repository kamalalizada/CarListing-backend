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
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

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
                entity.Property(c => c.Mileage).IsRequired();
                entity.Property(c => c.FuelType).HasMaxLength(30).IsRequired();
                entity.Property(c => c.BodyType).HasMaxLength(30).IsRequired();
                entity.Property(c => c.Transmission).HasMaxLength(30).IsRequired();
                entity.Property(c => c.Engine).HasMaxLength(50).IsRequired();
                entity.Property(c => c.City).HasMaxLength(50).IsRequired();
                entity.Property(c => c.ContactPhone).HasMaxLength(20).IsRequired();
                entity.Property(c => c.Color).HasMaxLength(50);
                entity.Property(c => c.Description).HasMaxLength(2000);

                entity.HasOne(c => c.User)
                      .WithMany()
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(c => c.CreatedAt);
                entity.HasIndex(c => c.Brand);
                entity.HasIndex(c => c.Model);
                entity.HasIndex(c => c.City);
                entity.HasIndex(c => c.FuelType);
                entity.HasIndex(c => c.BodyType);
                entity.HasIndex(c => c.Transmission);
                entity.HasIndex(c => c.Price);
                entity.HasIndex(c => c.Year);
                entity.HasIndex(c => c.Mileage);

                // GET /api/cars/mine: a user's non-deleted cars, newest first.
                entity.HasIndex(c => new { c.UserId, c.DeletedAt, c.CreatedAt });
            });

            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();

                // Two requests racing to use the same token: the UPDATE includes
                // "UsedAt IS NULL", so the second one fails instead of resetting
                // the password a second time.
                entity.Property(t => t.UsedAt).IsConcurrencyToken();

                entity.HasOne(t => t.User)
                      .WithMany()
                      .HasForeignKey(t => t.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Lookup on reset, and the per-user hourly count for rate limiting.
                entity.HasIndex(t => new { t.UserId, t.TokenHash });
                entity.HasIndex(t => new { t.UserId, t.CreatedAt });
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
