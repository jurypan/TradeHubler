using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class StrategyRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<Strategy>> GetAllAsync(int accountId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Strategy.Where(f => f.AccountID == accountId).ToListAsync();
    }

    public async Task<Strategy?> GetById(long accountId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Strategy.FirstOrDefaultAsync(f => f.AccountID == accountId && f.ID == id);
    }

    public async Task<Strategy> AddAsync(Strategy Strategy)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await context.Strategy.AddAsync(Strategy);
        await context.SaveChangesAsync();
        return Strategy;
    }


    public async Task EditAsync(Strategy Strategy)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.Strategy.FirstOrDefaultAsync(f => f.AccountID == Strategy.AccountID && f.ID == Strategy.ID);
        if (entity != null)
        {
            context.Entry(entity).CurrentValues.SetValues(Strategy);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Strategy Strategy)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.Strategy.FirstOrDefaultAsync(f => f.AccountID == Strategy.AccountID && f.ID == Strategy.ID);
        if (entity != null)
        {
            context.Strategy.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
