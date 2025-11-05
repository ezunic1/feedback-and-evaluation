using APLabApp.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace APLabApp.Dal
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> dbContextOptions) : base(dbContextOptions) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Season> Seasons => Set<Season>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<User>(e =>
            {
                e.ToTable("users");
                e.Property(x => x.FullName).HasMaxLength(200);
                e.Property(x => x.Email).HasMaxLength(200);
                e.HasIndex(x => x.Email).IsUnique();
                e.HasOne(x => x.Season)
                    .WithMany(s => s.Users)
                    .HasForeignKey(x => x.SeasonId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            b.Entity<Season>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100);
                e.HasOne(s => s.Mentor)
                    .WithMany()
                    .HasForeignKey(s => s.MentorId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
