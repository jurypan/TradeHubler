using JCTG.Entity;
using JCTG.Models;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class SignalRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<Signal>> GetAll(int accountId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal.Where(f => f.AccountID == accountId).OrderByDescending(f => f.DateLastUpdated).Take(100).ToListAsync();
    }

    public async Task<List<Signal>> GetAllTradesByStrategyType(int accountId, StrategyType strategyType)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal.Where(f => f.AccountID == accountId && f.StrategyType == strategyType && f.ExitRiskRewardRatio.HasValue).OrderBy(f => f.DateLastUpdated).ToListAsync();
    }

    public async Task<List<Signal>> GetAll(int accountId, string instrument, string ordertype, StrategyType strategyType)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal.Where(f => f.AccountID == accountId && f.Instrument == instrument && f.OrderType == ordertype && f.StrategyType == strategyType).OrderByDescending(f => f.DateLastUpdated).ToListAsync();
    }

    public async Task<List<Signal>> GetAllByStrategy(int accountId, StrategyType strategyType)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal.Where(f => f.AccountID == accountId && f.StrategyType == strategyType).OrderByDescending(f => f.DateLastUpdated).ToListAsync();
    }

    public async Task<Signal?> GetById(int accountId,  long signalId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal
            .Include(f => f.Orders).ThenInclude(f => f.Client)
            .Include(f => f.Logs)
            .FirstOrDefaultAsync(f => f.AccountID == accountId && f.ID == signalId);
    }

    public async Task AddAsync(Signal signal)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        await context.Signal.AddAsync(signal);
        await context.SaveChangesAsync();
    }

    public async Task EditAsync(Signal signal)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.Signal.FirstOrDefaultAsync(f => f.AccountID == signal.AccountID && f.ID == signal.ID);
        if (entity != null)
        {
            context.Entry(entity).CurrentValues.SetValues(signal);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Signal signal)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var entity = await context.Signal.FirstOrDefaultAsync(f => f.AccountID == signal.AccountID && f.ID == signal.ID);
        if (entity != null)
        {
            context.Log.RemoveRange(await context.Log.Where(f => f.SignalID == signal.ID).ToListAsync());
            context.MarketAbstention.RemoveRange(await context.MarketAbstention.Where(f => f.SignalID == signal.ID).ToListAsync());
            context.Order.RemoveRange(await context.Order.Where(f => f.SignalID == signal.ID).ToListAsync());
            context.Signal.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
