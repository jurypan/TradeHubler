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

        public DbSet<TradeJournalDeal> TradeJournalDeal { get; set; }

        public DbSet<Log> Log { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            // Signal - Account
            modelBuilder.Entity<Signal>()
                .HasOne(ta => ta.Account)
                .WithMany(t => t.Signals)
                .HasForeignKey(ta => ta.AccountID);

            modelBuilder.Entity<TradeJournal>()
               .HasOne(tj => tj.Client)
               .WithMany(t => t.TradeJournals)
               .HasForeignKey(t => t.ClientID);

            modelBuilder.Entity<TradeJournalDeal>()
              .HasOne(tj => tj.TradeJournal)
              .WithMany(t => t.Deals)
              .HasForeignKey(t => t.TradeJournalID);

            modelBuilder.Entity<Signal>()
                .HasMany(tj => tj.Logs)
                .WithOne() 
                .HasForeignKey(t => t.SignalID); 

            modelBuilder.Entity<TradeJournal>()
                .HasOne(tj => tj.Signal)
                .WithMany(t => t.TradeJournals)
               .HasForeignKey(t => t.SignalID);

            modelBuilder.Entity<Log>()
               .HasOne(tj => tj.Client)
               .WithMany(t => t.Logs)
              .HasForeignKey(t => t.ClientID);

            modelBuilder.Entity<Log>()
               .HasOne(tj => tj.Signal)
               .WithMany(t => t.Logs)
              .HasForeignKey(t => t.SignalID)
              .IsRequired(false);


        }
    }
}
