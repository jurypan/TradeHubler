using JCTG.Entity;
using JCTG.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;

namespace JCTG.WebApp.Backend.Repository;

public class ClientRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<Client>> GetAllAsync(int accountId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Client
            .Include(f => f.Pairs)
            .Where(f => f.AccountID == accountId).ToListAsync();
    }

    public async Task<ClientPair> AddPairAsync(ClientPair clientPair)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await context.ClientPair.AddAsync(clientPair);
        await context.SaveChangesAsync();
        return clientPair;
    }

    public async Task AddPairAsync(List<ClientPair> clientPairs, long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        foreach (var pair in clientPairs)
        {
            context.Entry(pair).State = EntityState.Detached;
            pair.GetType().GetProperty("ID")?.SetValue(pair, 0);
            pair.GetType().GetProperty("Client")?.SetValue(pair, null);
            pair.ClientID = clientId;
            await context.ClientPair.AddAsync(pair);
        }
        await context.SaveChangesAsync();
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

    public async Task AddRiskAsync(List<ClientRisk> clientRisks, long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        foreach (var risk in clientRisks)
        {
            context.Entry(risk).State = EntityState.Detached;
            risk.GetType().GetProperty("ID")?.SetValue(risk, 0);
            risk.GetType().GetProperty("Client")?.SetValue(risk, null);
            risk.ClientID = clientId;
            await context.ClientRisk.AddAsync(risk);
        }
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

    public async Task<List<ClientPair>> GetAllPairsByIdAsync(int accountId, long id, StrategyType strategyType)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Client
            .Include(f => f.Pairs)
            .Where(f => f.AccountID == accountId && f.ID == id)
            .SelectMany(f => f.Pairs.Where(g => g.StrategyType == strategyType)).ToListAsync();
    }

    public async Task<List<ClientPair>> GetAllPairsByIdAsync(int accountId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Client
            .Include(f => f.Pairs)
            .Where(f => f.AccountID == accountId && f.ID == id)
            .SelectMany(f => f.Pairs).ToListAsync();
    }

    public async Task<Client> AddAsync(Client client)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await context.Client.AddAsync(client);
        await context.SaveChangesAsync();
        return client;
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

        // MarketAbstention
        var entitiesMarketAbstention = await context.MarketAbstention.Where(f => f.ClientID == id).ToListAsync();
        context.MarketAbstention.RemoveRange(entitiesMarketAbstention);

        // Client
        var entity = await context.Client.FirstOrDefaultAsync(f => f.AccountID == accountId && f.ID == id);
        if (entity != null)
            context.Client.Remove(entity);
        await context.SaveChangesAsync();
    }

    public async Task<Client> CopyAsync(Client client, List<ClientRisk> clientRisks, List<ClientPair> clientPairs)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        context.Entry(client).State = EntityState.Detached;
        client.Balance = client.StartBalance;
        client.Equity = client.StartBalance;
        client.DateCreated = DateTime.UtcNow;
        client.Risks = [];
        client.Pairs = [];
        client = await AddAsync(client);
        await AddRiskAsync(clientRisks, client.ID);
        await AddPairAsync(clientPairs, client.ID);
        return client;
    }
}
