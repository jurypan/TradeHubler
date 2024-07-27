using JCTG.Entity;
using JCTG.Models;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class MarketAbstentionRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<MarketAbstention>> GetAllByStrategyId(int accountId, long clientId, long strategyId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.MarketAbstention.Where(f => f.Client != null 
                                                    && f.Client.AccountID == accountId 
                                                    && f.ClientID == clientId
                                                    && f.Signal != null
                                                    && f.Signal.StrategyID == strategyId
                                                    ).OrderBy(f => f.DateLastUpdated).ToListAsync();
    }

    public async Task<List<MarketAbstention>> GetAllByClientId(int accountId, long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.MarketAbstention.Where(f => f.Client != null
                                                    && f.Client.AccountID == accountId
                                                    && f.ClientID == clientId
                                                    && f.Signal != null
                                                    ).OrderBy(f => f.DateLastUpdated).ToListAsync();
    }

    public async Task<List<MarketAbstention>> GetAllBySignalId(int accountId, long signalId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.MarketAbstention
                            .Include(f => f.Signal)
                            .Include(f => f.Client)
                            .Where(f => f.Client != null
                                            && f.Client.AccountID == accountId
                                            && f.SignalID == signalId
                                            )
            .OrderByDescending(f => f.DateLastUpdated).ToListAsync();
    }

    public async Task<MarketAbstention?> GetById(int accountId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.MarketAbstention
            .FirstOrDefaultAsync(f => f.Client != null && f.Client.AccountID == accountId && f.ID == id);
    }

    public async Task AddAsync(MarketAbstention model)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await context.MarketAbstention.AddAsync(model);
        await context.SaveChangesAsync();
    }

    public async Task EditAsync(MarketAbstention model)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.MarketAbstention.FirstOrDefaultAsync(f => f.ClientID == model.ClientID && f.ID == model.ID);
        if (entity != null)
        {
            context.Entry(entity).CurrentValues.SetValues(model);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(MarketAbstention model)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.MarketAbstention.FirstOrDefaultAsync(f => f.ClientID == model.ClientID && f.ID == model.ID);
        if (entity != null)
        {
            context.MarketAbstention.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
