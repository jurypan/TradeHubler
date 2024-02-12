using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Repository
{
    public class LogRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
    {
        public async Task<List<Log>> GetAll(long accountId, long signalId)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.Log.Where(f => f.SignalID == signalId && f.Signal != null && f.Signal.AccountID == accountId).ToListAsync();
        }

        public async Task<List<Log>> GetAll(long accountId, long signalId, long tradeJournalId)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.Log
                .Where(f => f.SignalID == signalId && f.Signal != null && f.Signal.AccountID == accountId && f.Signal.TradeJournals.Count(t => t.ID == tradeJournalId && f.SignalID == signalId) == 1)
                .OrderByDescending(f => f.DateCreated)
                .ToListAsync();
        }
    }
}
