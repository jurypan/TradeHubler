using Microsoft.EntityFrameworkCore;

namespace JCTG
{
    public class JCTGDbContext : DbContext
    {


        public JCTGDbContext(DbContextOptions<JCTGDbContext> options)
            : base(options)
        {
        }

        public DbSet<Account> Account { get; set; }

        public DbSet<Client> Client { get; set; }

        public DbSet<Trade> Trade { get; set; }

        public DbSet<TradingviewAlert> TradingviewAlert { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Account - Client
            modelBuilder.Entity<Account>()
                .HasMany(a => a.Clients)
                .WithOne(c => c.Account)
                .HasForeignKey(c => c.AccountID);

            // Account - Trade
            modelBuilder.Entity<Account>()
                .HasMany(a => a.Trades)
                .WithOne(t => t.Account)
                .HasForeignKey(t => t.AccountID);

            // Client - Trade
            modelBuilder.Entity<Client>()
                .HasMany(c => c.Trades)
                .WithOne(t => t.Client)
                .HasForeignKey(t => t.ClientID);

            // TradingviewAlert - Trade
            modelBuilder.Entity<TradingviewAlert>()
                .HasMany(ta => ta.Trades)
                .WithOne(t => t.TradingviewAlert)
                .HasForeignKey(t => t.TradingviewAlertID);

            // TradingviewAlert - Account
            modelBuilder.Entity<TradingviewAlert>()
                .HasOne(ta => ta.Account)
                .WithMany(t => t.TradingviewAlerts)
                .HasForeignKey(ta => ta.AccountID);
        }
    }
}
