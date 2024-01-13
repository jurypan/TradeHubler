using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG
{
    public class JCTGDbContext(DbContextOptions<JCTGDbContext> options) : DbContext(options)
    {
        public DbSet<Account> Account { get; set; }

        public DbSet<Client> Client { get; set; }

        public DbSet<Signal> Signal { get; set; }

        public DbSet<TradeJournal> TradeJournal { get; set; }

        public DbSet<Log> Log { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            // Signal - Account
            modelBuilder.Entity<Signal>()
                .HasOne(ta => ta.Account)
                .WithMany(t => t.Signals)
                .HasForeignKey(ta => ta.AccountID);

            // Log - Account
            modelBuilder.Entity<Log>()
                .HasOne(ta => ta.Account)
                .WithMany(t => t.Logs)
                .HasForeignKey(ta => ta.AccountID);

            // Log - Client
            modelBuilder.Entity<Log>()
                .HasOne(ta => ta.Client)
                .WithMany(t => t.Logs)
                .HasForeignKey(ta => ta.ClientID);

            // Tradejournal - Client
            modelBuilder.Entity<TradeJournal>()
                .HasOne(ta => ta.Client)
                .WithMany(t => t.TradeJournals)
                .HasForeignKey(ta => ta.ClientID);

            // Tradejournal - Account
            modelBuilder.Entity<TradeJournal>()
                .HasOne(ta => ta.Account)
                .WithMany(t => t.TradeJournals)
                .HasForeignKey(ta => ta.AccountID);
        }
    }
}
