using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class ClientRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<Client>> GetAllAsync(int accountId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Client.Where(f => f.AccountID == accountId).ToListAsync();
    }

    public async Task<Client?> GetByIdAsync(int accountId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Client.FirstOrDefaultAsync(f => f.AccountID == accountId && f.ID == id);
    }

    public async Task DeleteAsync(int accountId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();

        // Deal
        var entitiesDeals = await context.Deal.Where(f => f.Order.ClientID == id).ToListAsync();
        context.Deal.RemoveRange(entitiesDeals);

        // Orders
        var entitiesOrders = await context.Order.Where(f => f.ClientID == id).ToListAsync();
        context.Order.RemoveRange(entitiesOrders);

        // Logs
        var entitiesLogs = await context.Log.Where(f => f.ClientID == id).ToListAsync();
        context.Log.RemoveRange(entitiesLogs);

        // Client
        var entity = await context.Client.FirstOrDefaultAsync(f => f.AccountID == accountId && f.ID == id);
        if (entity != null)
            context.Client.Remove(entity);
        await context.SaveChangesAsync();
    }
}
