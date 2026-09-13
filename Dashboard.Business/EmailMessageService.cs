using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Business;

public interface IEmailMessageService
{
    Task<List<EmailMessage>> GetRecentAsync(int take = 25, CancellationToken cancellationToken = default);
    Task<EmailMessage?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

public sealed class EmailMessageService : IEmailMessageService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public EmailMessageService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<EmailMessage>> GetRecentAsync(int take = 25, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // On ne remonte pas les corps dans la liste : ils peuvent peser lourd et l'ecran
        // n'en a besoin qu'au moment ou on ouvre un mail.
        return await dbContext.EmailMessages
            .AsNoTracking()
            .OrderByDescending(m => m.SentUtc)
            .Take(take)
            .Select(m => new EmailMessage
            {
                Id = m.Id,
                MessageId = m.MessageId,
                Uid = m.Uid,
                Folder = m.Folder,
                FromAddress = m.FromAddress,
                FromName = m.FromName,
                Subject = m.Subject,
                SentUtc = m.SentUtc,
                IngestedUtc = m.IngestedUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<EmailMessage?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.EmailMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.EmailMessages.CountAsync(cancellationToken);
    }
}
