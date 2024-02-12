﻿using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Repository
{
    public class SignalRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
    {
        public async Task<List<Signal>> GetAll(int accountId)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.Signal.Where(f => f.AccountID == accountId).OrderByDescending(f => f.DateCreated).ToListAsync();
        }

        public async Task<Signal?> GetById(int accountId, long id)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.Signal
                .Include(f => f.TradeJournals).ThenInclude(f => f.Client)
                .Include(f => f.Logs)
                .FirstOrDefaultAsync(f => f.AccountID == accountId && f.ID == id);
        }

        public async Task<List<Client>> GetClientsThatDontHaveAJournalById(int accountId, long id)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();
            return await context.Client
                        .Where(f => f.AccountID == accountId && f.Account != null && f.Account.Signals.Any(s => s.ID == id && s.TradeJournals.Count == 0))
                        .ToListAsync();
        }
    }
}
