using JCTG.Entity;
using JCTG.Models;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class MarketAbstentionRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<MarketAbstention>> GetAllByStrategyType(int accountId, long clientId, StrategyType strategyType)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.MarketAbstention.Where(f => f.Client != null 
                                                    && f.Client.AccountID == accountId 
                                                    && f.ClientID == clientId
                                                    && f.Signal != null
                                                    && f.Signal.StrategyType == strategyType
                                                    ).OrderBy(f => f.DateLastUpdated).ToListAsync();
    }
}
