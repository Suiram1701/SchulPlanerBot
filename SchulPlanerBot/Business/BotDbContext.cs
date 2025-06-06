﻿using Microsoft.EntityFrameworkCore;
using SchulPlanerBot.Business.Models;

namespace SchulPlanerBot.Business;

public class BotDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Guild> Guilds => Set<Guild>();
    
    public DbSet<Homework> Homeworks => Set<Homework>();

    public DbSet<HomeworkSubscription> HomeworkSubscriptions => Set<HomeworkSubscription>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Guild>(builder =>
        {
            builder.HasKey(g => g.Id);

            builder.Property(g => g.Id).IsRequired();
            builder.Property(g => g.NotificationLocale).HasMaxLength(5);     // Max length in format 'en-US'
            builder.Property(g => g.DeleteHomeworksAfterDue).IsRequired();

            builder
                .OwnsMany<Notification>(g => g.Notifications, navigationBuilder =>
                {
                    navigationBuilder.HasKey(n => new { n.GuildId, n.ChannelId });
                    navigationBuilder.WithOwner().HasForeignKey(n => n.GuildId);
                
                    navigationBuilder.Property(n => n.GuildId).IsRequired();
                    navigationBuilder.Property(n => n.ChannelId).IsRequired();
                    navigationBuilder.Property(n => n.CronExpression).HasMaxLength(256).IsRequired();     // IDK how long they can be but just to be future-proof
                    navigationBuilder.Property(n => n.ObjectsIn);
                })
                .Navigation(g => g.Notifications)
                .AutoInclude();
            
            builder.HasMany<Homework>()
                .WithOne()
                .HasForeignKey(h => h.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasMany<HomeworkSubscription>()
                .WithOne()
                .HasForeignKey(s => s.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
        
        modelBuilder.Entity<Homework>(builder =>
        {
            builder.HasKey(h => h.Id);

            builder.Property(h => h.Id).ValueGeneratedOnAdd().IsRequired();
            builder.Property(h => h.GuildId).IsRequired();
            builder.Property(h => h.Due).IsRequired();
            builder.Property(h => h.Subject).HasMaxLength(32);     // Limit specified in HomeworkModal
            builder.Property(h => h.Title).HasMaxLength(64).IsRequired();     // Limit specified in HomeworkModal
            builder.Property(h => h.Details).HasMaxLength(4000);     // Discords limit for text inputs
            builder.Property(h => h.CreatedAt).ValueGeneratedOnAdd().IsRequired();
            builder.Property(h => h.CreatedBy).IsRequired();
            builder.Property(h => h.LastModifiedAt);
            builder.Property(h => h.LastModifiedBy);
        });

        modelBuilder.Entity<HomeworkSubscription>(builder =>
        {
            builder.HasKey(s => new { s.GuildId, s.UserId });

            builder.Property(s => s.GuildId).IsRequired();
            builder.Property(s => s.UserId).IsRequired();
            builder.Property(s => s.AnySubject).HasDefaultValue(true);
            builder.Property(s => s.Include);
            builder.Property(s => s.Exclude);
        });
    }
}
