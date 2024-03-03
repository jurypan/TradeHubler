using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class DealRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<Deal>> GetAll(long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Deal.Where(f => f.Order.ClientID == clientId && f.AccountBalance.HasValue).OrderBy(f => f.DateCreated).ToListAsync();
    }

    public async Task<List<Deal>> GetAllLast10(long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Deal.Where(f => f.Order.ClientID == clientId && f.AccountBalance.HasValue).OrderByDescending(f => f.DateCreated).Take(10).OrderBy(f => f.DateCreated).ToListAsync();
    }

    public async Task<List<Deal>> GetAll(long clientId, string symbol)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Deal.Where(f => f.Order.ClientID == clientId && f.AccountBalance.HasValue && f.Symbol == symbol).OrderBy(f => f.DateCreated).ToListAsync();
    }
}
