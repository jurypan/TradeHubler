using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class DealRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<Deal>> GetAllAsync(long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Deal.Where(f => f.Order.ClientID == clientId && f.AccountBalance.HasValue).OrderBy(f => f.DateCreated).ToListAsync();
    }

    public async Task<List<Deal>> GetAllLast25Async(long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Deal.Where(f => f.Order.ClientID == clientId && f.AccountBalance.HasValue).OrderByDescending(f => f.DateCreated).Take(25).OrderBy(f => f.DateCreated).ToListAsync();
    }

    public async Task<List<Deal>> GetAllAsync(long clientId, string symbol)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Deal.Where(f => f.Order.ClientID == clientId && f.AccountBalance.HasValue && f.Symbol == symbol).OrderBy(f => f.DateCreated).ToListAsync();
    }

    public async Task<List<Deal>> GetAllBySignalIdAsync(long accountId, long signalId, long orderId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Deal
            .Include(f => f.Order)
            .Include(f => f.Order.Signal)
            .Include(f => f.Order.Client)
            .Where(f => f.Order.Client != null
                        && f.Order.Client.AccountID == accountId
                        && f.Order.SignalID == signalId
                        && f.OrderID == orderId
                        )
                        .OrderByDescending(f => f.DateCreated)
                        .ToListAsync();
    }

    public async Task<Deal?> GetByIdAsync(long accountId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Deal
            .Include(f => f.Order)
            .FirstOrDefaultAsync(f => f.Order.Client != null && f.Order.Client.AccountID == accountId && f.ID == id);
    }

    public async Task<Deal?> GetByIdAsync(long accountId, long orderId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Deal.FirstOrDefaultAsync(f => f.Order.Client != null && f.Order.Client.AccountID == accountId && f.OrderID == orderId && f.ID == id);
    }

    public async Task AddAsync(Deal model)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await context.Deal.AddAsync(model);
        await context.SaveChangesAsync();
    }

    public async Task EditAsync(Deal model)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.Deal.FirstOrDefaultAsync(f => f.OrderID == model.OrderID && f.ID == model.ID);
        if (entity != null)
        {
            context.Entry(entity).CurrentValues.SetValues(model);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Deal model)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();

        // Order
        var entity = await context.Deal.FirstOrDefaultAsync(f => f.OrderID == model.OrderID && f.ID == model.ID);
        if (entity != null)
        {
            context.Deal.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
