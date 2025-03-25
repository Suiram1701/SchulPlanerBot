using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using SchulPlanerBot.Business.Models;

namespace SchulPlanerBot.Business.Database;

public class BotDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Guild> Guilds => Set<Guild>();

    public DbSet<Homework> Homeworks => Set<Homework>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Guild>(builder =>
        {
            builder.Property(g => g.Id).IsRequired();
            builder.Property(g => g.ChannelId);
            builder.Property(g => g.NotificationsEnabled).HasDefaultValue(false);
            builder.Property(g => g.StartNotifications);
            builder.Property(g => g.BetweenNotifications);

            builder.HasKey(g => g.Id);
            builder.HasMany<Homework>()
                .WithOne()
                .HasPrincipalKey(h => h.Id)
                .HasForeignKey(h => h.OwnerId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity<Homework>(builder =>
        {
            builder.Property(h => h.Id).ValueGeneratedOnAdd().IsRequired();
            builder.Property(h => h.OwnerId).IsRequired();
            builder.Property(h => h.Due).IsRequired();
            builder.Property(h => h.Subject);
            builder.Property(h => h.Title).IsRequired();
            builder.Property(h => h.Details);
            builder.Property(h => h.CreatedAt).ValueGeneratedOnAdd().IsRequired();
            builder.Property(h => h.CreatedBy).IsRequired();

            builder.HasKey(h => h.Id);
        });

        modelBuilder.AddQuartz(options => options.UsePostgreSql());
    }
}
