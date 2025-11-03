using APLabApp.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace APLabApp.Dal
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> dbContextOptions) : base(dbContextOptions)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Season> Seasons => Set<Season>();
        //public DbSet<Season> Seasons => Set<Season>();
        /* public DbSet<Session> Sessions => Set<Session>();
         public DbSet<Feedback> Feedbacks => Set<Feedback>();
         public DbSet<Grade> Grades => Set<Grade>();
         public DbSet<Lecture> Lectures => Set<Lecture>();*/


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Lowercase table names
            modelBuilder.Entity<User>().ToTable("users");
            /*modelBuilder.Entity<Session>().ToTable("sessions");
            modelBuilder.Entity<Feedback>().ToTable("feedbacks");
            modelBuilder.Entity<Grade>().ToTable("grades");
            modelBuilder.Entity<Lecture>().ToTable("lectures");

            // Relationships
            modelBuilder.Entity<User>()
                .HasMany(u => u.MentoredSessions)
                .WithOne(s => s.Mentor)
                .HasForeignKey(s => s.MentorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasOne(u => u.CurrentSession)
                .WithMany(s => s.Interns)
                .HasForeignKey(u => u.CurrentSessionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Sender)
                .WithMany(u => u.SentFeedbacks)
                .HasForeignKey(f => f.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Receiver)
                .WithMany(u => u.ReceivedFeedbacks)
                .HasForeignKey(f => f.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Grade>()
                .HasOne(g => g.Grader)
                .WithMany(u => u.GivenGrades)
                .HasForeignKey(g => g.GraderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Grade>()
                .HasOne(g => g.Receiver)
                .WithMany(u => u.ReceivedGrades)
                .HasForeignKey(g => g.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);*/
        }
    }
}
