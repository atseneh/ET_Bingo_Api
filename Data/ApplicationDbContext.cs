using bingooo.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using YourNamespace.Models;

namespace bingooo.data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<ApplicationUser> ApplicationUser { get; set; }

        public DbSet<Balance> Balance { get; set; }
        public DbSet<Sales> Sales { get; set; }
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
         protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the foreign key relationship between Balance and ApplicationUser
            modelBuilder.Entity<Balance>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the foreign key relationship for Sales
            modelBuilder.Entity<Sales>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
        public DbSet<Game> Games { get; set; }
        public DbSet<Shop> Shops { get; set; }
        public DbSet<Commission> Commissions { get; set; }
        public DbSet<UserCommission> UserCommissions { get; set; }
        
        
    }
}