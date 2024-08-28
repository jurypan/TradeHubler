using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class TradingviewAlertRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<TradingviewAlert>> GetAll(long tvMagic)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.TradingviewAlert.Where(f => f.TvMagic == tvMagic).OrderByDescending(f => f.DateCreated).ToListAsync();
    }

    public async Task<List<TradingviewAlert>> GetAllLast200(long accountId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.TradingviewAlert
            .Where(f => f.AccountID == accountId)
            .OrderByDescending(f => f.DateCreated)
            .Take(200)
            .ToListAsync();
    }
}