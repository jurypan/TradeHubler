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

    public async Task<List<Order>> GetAllBySignalId(long accountId, long signalId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Order
            .Include(f => f.Client)
            .Include(f => f.Signal)
            .Where(f => f.Client != null
                        && f.Client.AccountID == accountId
                        && f.SignalID == signalId
                        )
                        .OrderByDescending(f => f.DateCreated)
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

    public async Task<List<Order>> GetAllByStrategType(long accountId, long clientId, Models.StrategyType strategyType)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Order
            .Include(f => f.Signal)
            .Where(f => f.Client != null
                        && f.Client.AccountID == accountId
                        && f.ClientID == clientId
                        && f.Signal.StrategyType == strategyType
                        )
                        .OrderBy(f => f.DateCreated)
                        .ToListAsync();
    }

    public async Task<Order?> GetById(long accountId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Order.FirstOrDefaultAsync(f => f.Client != null && f.Client.AccountID == accountId && f.ID == id);
    }

    public async Task AddAsync(Order model)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await context.Order.AddAsync(model);
        await context.SaveChangesAsync();
    }

    public async Task EditAsync(Order model)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.Order.FirstOrDefaultAsync(f => f.ClientID == model.ClientID && f.ID == model.ID);
        if (entity != null)
        {
            context.Entry(entity).CurrentValues.SetValues(model);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Order model)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();

        // Deals
        var entitiesDeal = await context.Deal.Where(f => f.OrderID == model.ID).ToListAsync();
        context.Deal.RemoveRange(entitiesDeal);

        // Order
        var entity = await context.Order.FirstOrDefaultAsync(f => f.ClientID == model.ClientID && f.ID == model.ID);
        if (entity != null)
        {
            context.Order.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
