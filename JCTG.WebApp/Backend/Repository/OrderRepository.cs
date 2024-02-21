using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class OrderRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<Order>> GetAll(long accountId, long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Order
            .Include(f => f.Signal)
            .Where(f => f.Client != null
                        && f.Client.AccountID == accountId
                        && f.ClientID == clientId
                        )
                        .OrderBy(f => f.DateCreated)
                        .ToListAsync();
    }

    public async Task<List<Order>> GetAll(long accountId, long clientId, string symbol)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Order
            .Include(f => f.Signal)
            .Where(f => f.Client != null 
                        && f.Client.AccountID == accountId 
                        && f.ClientID == clientId 
                        && f.Symbol == symbol
                        )
                        .OrderBy(f => f.DateCreated)
                        .ToListAsync();
    }

    public async Task<List<string>> GetAllPairs(long accountId, long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Order
            .Where(f => f.Client != null
                        && f.Client.AccountID == accountId
                        && f.ClientID == clientId
                        )
                        .GroupBy(f => f.Symbol)
                        .Select(f => f.Key)
                        .ToListAsync()
                        ;
    }
}
