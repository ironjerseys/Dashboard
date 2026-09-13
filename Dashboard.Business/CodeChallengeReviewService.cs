using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public sealed record CodeChallengeCardInfo(
    int Box,
    DateOnly NextDueDate,
    bool IsDue,
    DateTime? LastReviewedUtc
);

public interface ICodeChallengeReviewService
{
    // Récupère la carte de l'utilisateur pour ce défi (la crée si absente).
    Task<CodeChallengeCardInfo> GetOrCreateCardAsync(
        string ownerId,
        string challengeKey,
        DateOnly todayUtc,
        CancellationToken cancellationToken = default);

    // Enregistre une tentative et fait avancer/réinitialiser la boîte,
    // exactement comme le système Leitner des questions techniques.
    Task<CodeChallengeCardInfo> RecordReviewAsync(
        string ownerId,
        string challengeKey,
        bool isCorrect,
        DateOnly todayUtc,
        CancellationToken cancellationToken = default);

    // Compte les défis dus aujourd'hui pour l'utilisateur, séparés en
    // défis de code (clé quelconque) et défis SQL (clé préfixée "sql-").
    Task<(int Coding, int Sql)> GetDueCountsAsync(
        string ownerId,
        DateOnly todayUtc,
        CancellationToken cancellationToken = default);
}

public class CodeChallengeReviewService : ICodeChallengeReviewService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    // Mêmes intervalles que LeitnerService (en jours, par boîte).
    private static readonly int[] BoxIntervalsDays = new[] { 2, 4, 8, 16, 32, 64 };
    private const int MaxBox = 6;

    public CodeChallengeReviewService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<CodeChallengeCardInfo> GetOrCreateCardAsync(
        string ownerId,
        string challengeKey,
        DateOnly todayUtc,
        CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        CodeChallengeCard? card = await dbContext.CodeChallengeCards
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId && c.ChallengeKey == challengeKey, cancellationToken);

        if (card is null)
        {
            card = new CodeChallengeCard
            {
                OwnerId = ownerId,
                ChallengeKey = challengeKey,
                Box = 1,
                NextDueDate = todayUtc,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            dbContext.CodeChallengeCards.Add(card);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToInfo(card, todayUtc);
    }

    public async Task<CodeChallengeCardInfo> RecordReviewAsync(
        string ownerId,
        string challengeKey,
        bool isCorrect,
        DateOnly todayUtc,
        CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        CodeChallengeCard? card = await dbContext.CodeChallengeCards
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId && c.ChallengeKey == challengeKey, cancellationToken);

        if (card is null)
        {
            card = new CodeChallengeCard
            {
                OwnerId = ownerId,
                ChallengeKey = challengeKey,
                Box = 1,
                NextDueDate = todayUtc,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            dbContext.CodeChallengeCards.Add(card);
        }

        int newBox = isCorrect ? Math.Min(card.Box + 1, MaxBox) : 1;

        card.Box = newBox;
        card.LastReviewedUtc = DateTime.UtcNow;
        card.NextDueDate = ComputeNextDueDate(todayUtc, newBox, isCorrect);
        card.UpdatedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToInfo(card, todayUtc);
    }

    public async Task<(int Coding, int Sql)> GetDueCountsAsync(
        string ownerId,
        DateOnly todayUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return (0, 0);

        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<string> dueKeys = await dbContext.CodeChallengeCards
            .AsNoTracking()
            .Where(c => c.OwnerId == ownerId && c.NextDueDate <= todayUtc)
            .Select(c => c.ChallengeKey)
            .ToListAsync(cancellationToken);

        int sql = dueKeys.Count(k => k.StartsWith("sql-", StringComparison.Ordinal));
        int coding = dueKeys.Count - sql;
        return (coding, sql);
    }

    private static CodeChallengeCardInfo ToInfo(CodeChallengeCard card, DateOnly todayUtc) =>
        new(card.Box, card.NextDueDate, card.NextDueDate <= todayUtc, card.LastReviewedUtc);

    private static DateOnly ComputeNextDueDate(DateOnly todayUtc, int box, bool isCorrect)
    {
        if (!isCorrect)
        {
            // Si raté, on revient demain.
            return todayUtc.AddDays(1);
        }

        int safeBox = Math.Clamp(box, 1, BoxIntervalsDays.Length);
        int days = BoxIntervalsDays[safeBox - 1];
        return todayUtc.AddDays(days);
    }
}
