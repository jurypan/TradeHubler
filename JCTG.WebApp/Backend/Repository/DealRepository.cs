using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class DealRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<List<Deal>> GetAll(long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Deal.Where(f => f.Order.ClientID == clientId && f.AccountBalance.HasValue).OrderBy(f => f.DateCreated).ToListAsync();
    }
}
