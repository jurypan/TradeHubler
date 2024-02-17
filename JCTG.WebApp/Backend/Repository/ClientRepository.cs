using JCTG.Entity;
using Microsoft.EntityFrameworkCore;

namespace JCTG.WebApp.Backend.Repository;

public class ClientRepository(IDbContextFactory<JCTGDbContext> dbContextFactory)
{
    public async Task<IEnumerable<Client>> GetAll(int accountId)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Client.Where(f => f.AccountID == accountId).ToListAsync();
    }

    public async Task<Client?> GetById(int accountId, long id)
    {
        using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.Client.FirstOrDefaultAsync(f => f.AccountID == accountId && f.ID == id);
    }
}
