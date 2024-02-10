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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            // Signal - Account
            modelBuilder.Entity<Signal>()
                .HasOne(ta => ta.Account)
                .WithMany(t => t.Signals)
                .HasForeignKey(ta => ta.AccountID);

            modelBuilder.Entity<TradeJournal>()
                    .HasOne(e => e.Signal)
                    .WithOne(e => e.TradeJournal)
                    .HasForeignKey<Signal>(e => e.TradeJournalID)
                    .IsRequired(false);
        }
    }
}
