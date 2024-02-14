using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Repository
{
    public class SignalRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
    {
        public async Task<List<Signal>> GetAll(int accountId)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.Signal.Where(f => f.AccountID == accountId).OrderByDescending(f => f.DateCreated).ToListAsync();
        }

        public async Task<Signal?> GetById(int accountId,  long signalId)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.Signal
                .Include(f => f.Orders).ThenInclude(f => f.Client)
                .Include(f => f.Logs)
                .FirstOrDefaultAsync(f => f.AccountID == accountId && f.ID == signalId);
        }
    }
}
