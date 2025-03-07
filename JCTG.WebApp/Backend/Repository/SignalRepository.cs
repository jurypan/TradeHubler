﻿using JCTG.Entity;
using JCTG.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;

namespace JCTG.WebApp.Backend.Repository;

public class SignalRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<Signal>> GetAllLast200(int accountId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal
            .Include(f => f.Orders)
            .Where(f => f.AccountID == accountId)
            .OrderByDescending(f => f.DateLastUpdated)
            .Take(200)
            .ToListAsync();
    }

    public async Task<List<Signal>> GetAll(int accountId, DateTime startDate)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal
            .Include(f => f.Orders)
            .Where(f => f.AccountID == accountId && f.DateCreated >= startDate)
            .OrderByDescending(f => f.DateCreated)
            .ToListAsync();
    }

    public async Task<List<Signal>> GetAllByInstrument(int accountId, string instrument, DateTime startDate)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal
            .Include(f => f.Orders)
            .Where(f => f.AccountID == accountId && f.Instrument == instrument && f.DateCreated >= startDate)
            .OrderByDescending(f => f.DateCreated)
            .ToListAsync();
    }

    public async Task<List<Signal>> GetAllByInstrumentAndStrategyType(int accountId, string instrument, string orderType, long strategyId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal
            .Include(f => f.Orders)
            .Where(f => f.AccountID == accountId && f.Instrument == instrument && f.OrderType.Contains(orderType) && f.StrategyID == strategyId)
            .OrderByDescending(f => f.DateLastUpdated)
            .ToListAsync();
    }

    public async Task<List<Signal>> GetAllLast200ByClientId(int accountId, long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        var pairs = await context.ClientPair.Where(f => f.Client != null && f.Client.AccountID == accountId && f.ClientID == clientId).ToListAsync();

        var signals = new List<Signal>();
        foreach(var pair in pairs)
        {
            signals.AddRange(await context.Signal
                .Include(f => f.Orders)
                .Where(f => f.AccountID == accountId && f.Instrument == pair.TickerInTradingView && f.StrategyID == pair.StrategyID)
                .ToListAsync());
        }

        return signals.OrderByDescending(f => f.DateLastUpdated).ToList();
    }

    public async Task<List<Signal>> GetAllLast200ByStrategyType(int accountId, long strategyId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal
            .Include(f => f.Orders)
            .Where(f => f.AccountID == accountId && f.StrategyID == strategyId)
            .OrderByDescending(f => f.DateLastUpdated)
            .Take(200)
            .ToListAsync();
    }

    public async Task<List<Signal>> GetAllByStrategyType(int accountId, long strategyId, DateTime startDate)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal
            .Include(f => f.Orders)
            .Where(f => f.AccountID == accountId && f.StrategyID == strategyId && f.DateCreated >= startDate)
            .OrderByDescending(f => f.DateLastUpdated)
            .ToListAsync();
    }

    public async Task<List<Signal>> GetAllByStrategyType(int accountId, long strategyId, string instrument, DateTime startDate)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal
            .Include(f => f.Orders)
            .Where(f => f.AccountID == accountId && f.StrategyID == strategyId && f.Instrument == instrument && f.DateCreated >= startDate)
            .OrderByDescending(f => f.DateLastUpdated)
            .ToListAsync();
    }

    public async Task<List<Signal>> GetAll(int accountId, string instrument, string ordertype, long strategyId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Signal.Where(f => f.AccountID == accountId && f.Instrument == instrument && f.OrderType == ordertype && f.StrategyID == strategyId).OrderByDescending(f => f.DateLastUpdated).ToListAsync();
    }



    public async Task<Signal?> GetByIdAsync(int accountId,  long signalId)
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
