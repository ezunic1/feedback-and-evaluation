using APLabApp.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace APLabApp.Dal
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> dbContextOptions) : base(dbContextOptions) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Season> Seasons => Set<Season>();
        public DbSet<Feedback> Feedbacks => Set<Feedback>();
        public DbSet<Grade> Grades => Set<Grade>();
        public DbSet<DeleteRequest> DeleteRequests => Set<DeleteRequest>();

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

            b.Entity<Feedback>(e =>
            {
                e.ToTable("feedbacks");

                e.Property(x => x.Comment)
                    .IsRequired()
                    .HasMaxLength(2000);

                e.Property(x => x.CreatedAtUtc)
                    .IsRequired();

                e.HasOne(x => x.Season)
                    .WithMany()
                    .HasForeignKey(x => x.SeasonId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.SenderUser)
                    .WithMany()
                    .HasForeignKey(x => x.SenderUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ReceiverUser)
                    .WithMany()
                    .HasForeignKey(x => x.ReceiverUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => new { x.SeasonId, x.ReceiverUserId });
            });

            b.Entity<Grade>(e =>
            {
                e.ToTable("grades");

                e.HasKey(x => x.FeedbackId);

                e.Property(x => x.CareerSkills).IsRequired();
                e.Property(x => x.Communication).IsRequired();
                e.Property(x => x.Collaboration).IsRequired();

                e.HasOne(x => x.Feedback)
                    .WithOne(f => f.Grade)
                    .HasForeignKey<Grade>(x => x.FeedbackId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<DeleteRequest>(e =>
            {
                e.ToTable("delete_requests");

                e.Property(x => x.Reason)
                    .IsRequired()
                    .HasMaxLength(2000);

                e.Property(x => x.CreatedAtUtc)
                    .IsRequired();

                e.HasOne(x => x.Feedback)
                    .WithMany()
                    .HasForeignKey(x => x.FeedbackId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.SenderUser)
                    .WithMany()
                    .HasForeignKey(x => x.SenderUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
