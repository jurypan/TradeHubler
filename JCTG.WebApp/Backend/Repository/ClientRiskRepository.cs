using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class ClientRiskRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<ClientRisk>> GetAllAsync(int accountId, long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.ClientRisk.Where(f => f.Client != null && f.Client.AccountID == accountId && f.ClientID == clientId).ToListAsync();
    }

    public async Task<ClientRisk?> GetById(long accountId, long clientId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.ClientRisk.FirstOrDefaultAsync(f => f.Client != null && f.Client.AccountID == accountId && f.ClientID == clientId && f.ID == id);
    }

    public async Task<ClientRisk> AddAsync(ClientRisk clientRisk)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await context.ClientRisk.AddAsync(clientRisk);
        await context.SaveChangesAsync();
        return clientRisk;
    }

    public async Task<ClientRisk> CopyAsync(ClientRisk clientRisk)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        context.Entry(clientRisk).State = EntityState.Detached;
        clientRisk.GetType().GetProperty("ID")?.SetValue(clientRisk, 0);
        clientRisk.GetType().GetProperty("Client")?.SetValue(clientRisk, null);
        clientRisk.DateCreated = DateTime.UtcNow;
        return await AddAsync(clientRisk);
    }

    public async Task EditAsync(ClientRisk clientRisk)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.ClientRisk.FirstOrDefaultAsync(f => f.ClientID == clientRisk.ClientID && f.ID == clientRisk.ID);
        if (entity != null)
        {
            context.Entry(entity).CurrentValues.SetValues(clientRisk);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(ClientRisk clientRisk)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.ClientRisk.FirstOrDefaultAsync(f => f.ClientID == clientRisk.ClientID && f.ID == clientRisk.ID);
        if (entity != null)
        {
            context.ClientRisk.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task AddTemplateAsync(long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await AddAsync(new ClientRisk()
        {
            ClientID = clientId,
            DateCreated = DateTime.UtcNow,
            Procent = -6,
            Multiplier = 0.25,
        });
        await AddAsync(new ClientRisk()
        {
            ClientID = clientId,
            DateCreated = DateTime.UtcNow,
            Procent = -5,
            Multiplier = 0.4,
        });
        await AddAsync(new ClientRisk()
        {
            ClientID = clientId,
            DateCreated = DateTime.UtcNow,
            Procent = -4,
            Multiplier = 0.5,
        });
        await AddAsync(new ClientRisk()
        {
            ClientID = clientId,
            DateCreated = DateTime.UtcNow,
            Procent = -3,
            Multiplier = 0.6,
        });
        await AddAsync(new ClientRisk()
        {
            ClientID = clientId,
            DateCreated = DateTime.UtcNow,
            Procent = -2,
            Multiplier = 0.75,
        });
        await AddAsync(new ClientRisk()
        {
            ClientID = clientId,
            DateCreated = DateTime.UtcNow,
            Procent = -1,
            Multiplier = 0.85,
        });
        await AddAsync(new ClientRisk()
        {
            ClientID = clientId,
            DateCreated = DateTime.UtcNow,
            Procent = 0,
            Multiplier = 1,
        });
        await AddAsync(new ClientRisk()
        {
            ClientID = clientId,
            DateCreated = DateTime.UtcNow,
            Procent = 1,
            Multiplier = 1.1,
        });
        await AddAsync(new ClientRisk()
        {
            ClientID = clientId,
            DateCreated = DateTime.UtcNow,
            Procent = 2,
            Multiplier = 1.25,
        });
        await AddAsync(new ClientRisk()
        {
            ClientID = clientId,
            DateCreated = DateTime.UtcNow,
            Procent = 3,
            Multiplier = 1.4,
        });
        await AddAsync(new ClientRisk()
        {
            ClientID = clientId,
            DateCreated = DateTime.UtcNow,
            Procent = 4,
            Multiplier = 1.5,
        });
    }
}
