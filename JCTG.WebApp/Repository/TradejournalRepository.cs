using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Repository
{
    public class TradejournalRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
    {
        public async Task<IEnumerable<TradeJournal>> GetAll(long clientId)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.TradeJournal.Where(f => f.ClientID == clientId).ToListAsync();
        }

        public async Task<TradeJournal?> GetById(int clientId, long id)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.TradeJournal.FirstOrDefaultAsync(f => f.ClientID == clientId && f.ID == id);
        }
    }
}
