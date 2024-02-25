using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class TradingviewAlertRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<TradingviewAlert>> GetAll(long signalId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.TradingviewAlert.Where(f => f.SignalID == signalId).OrderByDescending(f => f.DateCreated).ToListAsync();
    }
}
