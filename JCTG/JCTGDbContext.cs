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

        public DbSet<Order> Order { get; set; }

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

            modelBuilder.Entity<TradeJournal>()
                .HasOne(tj => tj.Order)
                .WithOne()
                .HasForeignKey<TradeJournal>(t => t.OrderID);

            modelBuilder.Entity<TradeJournal>()
                .HasMany(tj => tj.Logs)
                .WithOne() 
                .HasForeignKey(t => t.TradeJournalID); 

            modelBuilder.Entity<TradeJournal>()
                .HasOne(tj => tj.Signal)
                .WithMany(t => t.TradeJournals)
               .HasForeignKey(t => t.SignalID);



            modelBuilder.Entity<Order>()
                .Property(o => o.OpenPrice)
                .HasPrecision(10, 8);
            modelBuilder.Entity<Order>()
               .Property(o => o.OpenStopLoss)
               .HasPrecision(10, 8);
            modelBuilder.Entity<Order>()
               .Property(o => o.OpenTakeProfit)
               .HasPrecision(10, 8);
            modelBuilder.Entity<Order>()
                .Property(o => o.ClosePrice)
                .HasPrecision(10, 8);
            modelBuilder.Entity<Order>()
               .Property(o => o.CloseStopLoss)
               .HasPrecision(10, 8);
            modelBuilder.Entity<Order>()
               .Property(o => o.CloseTakeProfit)
               .HasPrecision(10, 8);


            modelBuilder.Entity<Log>()
              .HasOne(tj => tj.TradeJournal)
              .WithMany(t => t.Logs)
              .HasForeignKey(t => t.TradeJournalID);


            modelBuilder.Entity<Log>()
               .HasOne(tj => tj.Client)
               .WithMany(t => t.Logs)
              .HasForeignKey(t => t.ClientID);
        }
    }
}
