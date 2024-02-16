using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class OrderRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<IEnumerable<Order>> GetAll(long clientId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Order.Where(f => f.ClientID == clientId).ToListAsync();
    }

    public async Task<Order?> GetById(int clientId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Order.FirstOrDefaultAsync(f => f.ClientID == clientId && f.ID == id);
    }
}
