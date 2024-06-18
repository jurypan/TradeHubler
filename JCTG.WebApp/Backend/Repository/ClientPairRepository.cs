using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class ClientPairRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<ClientPair>> GetAllAsync(int accountId, long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.ClientPair.Where(f => f.Client != null && f.Client.AccountID == accountId && f.ClientID == clientId).ToListAsync();
    }

    public async Task<ClientPair?> GetById(long accountId, long clientId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.ClientPair.FirstOrDefaultAsync(f => f.Client != null && f.Client.AccountID == accountId && f.ClientID == clientId && f.ID == id);
    }

    public async Task<ClientPair> AddAsync(ClientPair clientPair)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await context.ClientPair.AddAsync(clientPair);
        await context.SaveChangesAsync();
        return clientPair;
    }

    public async Task<ClientPair> CopyAsync(ClientPair clientPair)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        context.Entry(clientPair).State = EntityState.Detached;
        clientPair.GetType().GetProperty("ID")?.SetValue(clientPair, 0);
        clientPair.GetType().GetProperty("Client")?.SetValue(clientPair, null);
        clientPair.TickerInMetatrader += " (copy)";
        clientPair.TickerInTradingView += " (copy)";
        clientPair.DateCreated = DateTime.UtcNow;
        return await AddAsync(clientPair);
    }

    public async Task EditAsync(ClientPair clientPair)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.ClientPair.FirstOrDefaultAsync(f => f.ClientID == clientPair.ClientID && f.ID == clientPair.ID);
        if (entity != null)
        {
            context.Entry(entity).CurrentValues.SetValues(clientPair);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(ClientPair clientPair)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.ClientPair.FirstOrDefaultAsync(f => f.ClientID == clientPair.ClientID && f.ID == clientPair.ID);
        if (entity != null)
        {
            context.ClientPair.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
