using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class LogRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<Log>> GetAll(long accountId, long signalId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Log.Where(f => f.SignalID == signalId && f.Signal != null && f.Signal.AccountID == accountId).ToListAsync();
    }

    public async Task<List<Log>> GetAll(long accountId, long signalId, long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Log
            .Where(f => f.SignalID == signalId && f.ClientID == clientId)
            .OrderByDescending(f => f.DateCreated)
            .ToListAsync();
    }

    public async Task<List<Log>> SearchByLogTitle(long accountId, long signalId, long clientId, string search)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Log
            .Where(f => f.SignalID == signalId && f.ClientID == clientId && f.Description != null && f.Description.Contains(search))
            .OrderByDescending(f => f.DateCreated)
            .ToListAsync();
    }

    public async Task<List<Log>> GetAllLast200(long accountId, long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Log
            .Where(f => f.ClientID == clientId)
            .OrderByDescending(f => f.DateCreated)
            .Take(200)
            .ToListAsync();
    }
}
