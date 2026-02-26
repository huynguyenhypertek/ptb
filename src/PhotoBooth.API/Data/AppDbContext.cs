using Microsoft.EntityFrameworkCore;
using PhotoBooth.API.Models;

namespace PhotoBooth.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Frame> Frames => Set<Frame>();
    public DbSet<StoreFrame> StoreFrames => Set<StoreFrame>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}
