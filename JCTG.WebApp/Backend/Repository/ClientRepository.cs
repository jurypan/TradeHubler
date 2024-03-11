using JCTG.Entity;
using Microsoft.EntityFrameworkCore;
using System;

namespace JCTG.WebApp.Backend.Repository;

public class ClientRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<Client>> GetAllAsync(int accountId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Client.Where(f => f.AccountID == accountId).ToListAsync();
    }

    public async Task<ClientPair> AddPairAsync(ClientPair clientPair)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await context.ClientPair.AddAsync(clientPair);
        await context.SaveChangesAsync();
        return clientPair;
    }

    public async Task<ClientPair> CopyPairAsync(ClientPair clientPair)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        context.Entry(clientPair).State = EntityState.Detached;
        clientPair.GetType().GetProperty("ID")?.SetValue(clientPair, 0);
        clientPair.GetType().GetProperty("Client")?.SetValue(clientPair, null);
        clientPair.TickerInMetatrader += " (copy)";
        clientPair.TickerInTradingView += " (copy)";
        clientPair.DateCreated = DateTime.UtcNow;
        return await AddPairAsync(clientPair);
    }

    public async Task EditPairAsync(ClientPair clientPair)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.ClientPair.FirstOrDefaultAsync(f => f.ClientID == clientPair.ClientID && f.ID == clientPair.ID);
        if (entity != null)
        {
            context.Entry(entity).CurrentValues.SetValues(clientPair);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeletePairAsync(ClientPair clientPair)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.ClientPair.FirstOrDefaultAsync(f => f.ClientID == clientPair.ClientID && f.ID == clientPair.ID);
        if (entity != null)
        {
            context.ClientPair.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task AddRiskAsync(ClientRisk clientRisk)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await context.ClientRisk.AddAsync(clientRisk);
        await context.SaveChangesAsync();
    }

    public async Task EditRiskAsync(ClientRisk clientRisk)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.ClientRisk.FirstOrDefaultAsync(f => f.ClientID == clientRisk.ClientID && f.ID == clientRisk.ID);
        if (entity != null)
        {
            context.Entry(entity).CurrentValues.SetValues(clientRisk);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteRiskAsync(ClientRisk clientRisk)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.ClientRisk.FirstOrDefaultAsync(f => f.ClientID == clientRisk.ClientID && f.ID == clientRisk.ID);
        if (entity != null)
        {
            context.ClientRisk.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task<Client?> GetByIdAsync(int accountId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Client
            .Include(f => f.Pairs)
            .Include(f => f.Risks)
            .FirstOrDefaultAsync(f => f.AccountID == accountId && f.ID == id);
    }

    public async Task EditAsync(int accountId, Client client)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.Client.FirstOrDefaultAsync(f => f.AccountID == accountId && f.ID == client.ID);
        if (entity != null)
        {
            context.Entry(entity).CurrentValues.SetValues(client);
            await context.SaveChangesAsync();
        }
    }
    public async Task DeleteAsync(int accountId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();

        // Client Pairs
        var entitiesClientPairs = await context.ClientPair.Where(f => f.ClientID == id).ToListAsync();
        context.ClientPair.RemoveRange(entitiesClientPairs);

        // Client Risk
        var entitiesClientRisks = await context.ClientRisk.Where(f => f.ClientID == id).ToListAsync();
        context.ClientRisk.RemoveRange(entitiesClientRisks);

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
