using Microsoft.EntityFrameworkCore;
using SchulPlanerBot.Business.Models;

namespace SchulPlanerBot.Business.Database;

public class BotDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Guild> Guilds => Set<Guild>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Guild>(builder =>
        {
            builder.Property(g => g.Id).IsRequired();
            builder.Property(g => g.ChannelId).HasDefaultValue(null);
            builder.Property(g => g.NotificationsEnabled).HasDefaultValue(false);
            builder.Property(g => g.StartNotifications).HasDefaultValue(null);
            builder.Property(g => g.BetweenNotifications).HasDefaultValue(null);

            builder.HasKey(g => g.Id);
        });
    }
}
