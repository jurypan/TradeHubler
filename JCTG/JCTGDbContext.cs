using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG
{
    public class JCTGDbContext(DbContextOptions<JCTGDbContext> options) : DbContext(options)
    {
        public DbSet<Account> Account { get; set; }

        public DbSet<Client> Client { get; set; }

        public DbSet<ClientPair> ClientPair { get; set; }

        public DbSet<ClientRisk> ClientRisk { get; set; }

        public DbSet<Signal> Signal { get; set; }

        public DbSet<TradingviewAlert> TradingviewAlert { get; set; }

        public DbSet<Order> Order { get; set; }

        public DbSet<MarketAbstention> MarketAbstention { get; set; }

        public DbSet<Deal> Deal { get; set; }

        public DbSet<Log> Log { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            // Signal - Account
            modelBuilder.Entity<Signal>()
                .HasOne(ta => ta.Account)
                .WithMany(t => t.Signals)
                .HasForeignKey(ta => ta.AccountID);

            modelBuilder.Entity<TradingviewAlert>()
               .HasOne(ta => ta.Signal)
               .WithMany(t => t.TradingviewAlerts)
               .HasForeignKey(ta => ta.SignalID);

            modelBuilder.Entity<Order>()
               .HasOne(tj => tj.Client)
               .WithMany(t => t.Orders)
               .HasForeignKey(t => t.ClientID);

            modelBuilder.Entity<Deal>()
              .HasOne(tj => tj.Order)
              .WithMany(t => t.Deals)
              .HasForeignKey(t => t.OrderID);

            modelBuilder.Entity<Signal>()
                .HasMany(tj => tj.Logs)
                .WithOne() 
                .HasForeignKey(t => t.SignalID); 

            modelBuilder.Entity<Order>()
                .HasOne(tj => tj.Signal)
                .WithMany(t => t.Orders)
               .HasForeignKey(t => t.SignalID);

            modelBuilder.Entity<MarketAbstention>()
               .HasOne(tj => tj.Signal)
               .WithMany(t => t.MarketAbstentions)
              .HasForeignKey(t => t.SignalID);

            modelBuilder.Entity<MarketAbstention>()
               .HasOne(tj => tj.Client)
               .WithMany(t => t.MarketAbstentions)
              .HasForeignKey(t => t.ClientID);

            modelBuilder.Entity<Log>()
               .HasOne(tj => tj.Client)
               .WithMany(t => t.Logs)
              .HasForeignKey(t => t.ClientID);

            modelBuilder.Entity<Log>()
               .HasOne(tj => tj.Signal)
               .WithMany(t => t.Logs)
              .HasForeignKey(t => t.SignalID)
              .IsRequired(false);

            modelBuilder.Entity<ClientPair>()
              .HasOne(tj => tj.Client)
              .WithMany(t => t.Pairs)
              .HasForeignKey(t => t.ClientID);

            modelBuilder.Entity<ClientRisk>()
             .HasOne(tj => tj.Client)
             .WithMany(t => t.Risks)
             .HasForeignKey(t => t.ClientID);
        }
    }
}
