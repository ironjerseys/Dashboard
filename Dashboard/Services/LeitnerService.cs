using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public sealed record LeitnerDueItem(
    int CardId,
    int Box,
    DateOnly NextDueDate,
    QuestionTechnique Question
);

public sealed record LeitnerReviewResult(
    bool Found,
    bool IsCorrect,
    int PreviousBox,
    int NewBox,
    DateOnly NextDueDate
);

public interface ILeitnerService
{
    Task SyncMissingCardsAsync(string ownerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeitnerDueItem>> GetDueAsync(
        string ownerId,
        DateOnly dueOnOrBefore,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<int> GetDueCountAsync(
        string ownerId,
        DateOnly dueOnOrBefore,
        CancellationToken cancellationToken = default);

    Task<LeitnerReviewResult> RecordReviewAsync(
        string ownerId,
        int cardId,
        bool isCorrect,
        DateOnly todayUtc,
        CancellationToken cancellationToken = default);
}

public class LeitnerService : ILeitnerService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    // Intervalles (en jours) par boîte.
    // Box=1 => 1 jour, Box=2 => 2 jours, Box=3 => 4 jours...
    private static readonly int[] BoxIntervalsDays = new[] { 1, 2, 4, 8, 16, 32 };
    private const int MaxBox = 6;

    public LeitnerService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task SyncMissingCardsAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return;
        }

        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<int> allQuestionIds = await dbContext.QuizQuestions
            .AsNoTracking()
            .Select(question => question.Id)
            .ToListAsync(cancellationToken);

        List<int> existingQuestionIds = await dbContext.LeitnerCards
            .AsNoTracking()
            .Where(card => card.OwnerId == ownerId)
            .Select(card => card.QuizQuestionId)
            .ToListAsync(cancellationToken);

        HashSet<int> existingSet = existingQuestionIds.ToHashSet();
        DateOnly todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);

        bool hasChanges = false;

        foreach (int questionId in allQuestionIds)
        {
            if (existingSet.Contains(questionId))
            {
                continue;
            }

            dbContext.LeitnerCards.Add(new LeitnerCard
            {
                OwnerId = ownerId,
                QuizQuestionId = questionId,
                Box = 1,
                NextDueDate = todayUtc,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });

            hasChanges = true;
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<LeitnerDueItem>> GetDueAsync(
        string ownerId,
        DateOnly dueOnOrBefore,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<LeitnerCard> dueCards = await dbContext.LeitnerCards
            .AsNoTracking()
            .Include(card => card.Question)
            .Where(card => card.OwnerId == ownerId && card.NextDueDate <= dueOnOrBefore)
            .OrderBy(card => card.Box)
            .ThenBy(card => card.NextDueDate)
            .ThenBy(card => card.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        List<LeitnerDueItem> results = new();

        foreach (LeitnerCard card in dueCards)
        {
            if (card.Question is null)
            {
                continue;
            }

            results.Add(new LeitnerDueItem(
                CardId: card.Id,
                Box: card.Box,
                NextDueDate: card.NextDueDate,
                Question: card.Question
            ));
        }

        return results;
    }

    public async Task<int> GetDueCountAsync(string ownerId, DateOnly dueOnOrBefore, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        int count = await dbContext.LeitnerCards
            .AsNoTracking()
            .CountAsync(card => card.OwnerId == ownerId && card.NextDueDate <= dueOnOrBefore, cancellationToken);

        return count;
    }

    public async Task<LeitnerReviewResult> RecordReviewAsync(
        string ownerId,
        int cardId,
        bool isCorrect,
        DateOnly todayUtc,
        CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        LeitnerCard? card = await dbContext.LeitnerCards
            .FirstOrDefaultAsync(c => c.Id == cardId && c.OwnerId == ownerId, cancellationToken);

        if (card is null)
        {
            return new LeitnerReviewResult(
                Found: false,
                IsCorrect: isCorrect,
                PreviousBox: 0,
                NewBox: 0,
                NextDueDate: todayUtc
            );
        }

        int previousBox = card.Box;
        int newBox;

        if (isCorrect)
        {
            newBox = Math.Min(previousBox + 1, MaxBox);
        }
        else
        {
            newBox = 1;
        }

        DateOnly nextDueDate = ComputeNextDueDate(todayUtc, newBox, isCorrect);

        card.Box = newBox;
        card.LastReviewedUtc = DateTime.UtcNow;
        card.NextDueDate = nextDueDate;
        card.UpdatedUtc = DateTime.UtcNow;

        dbContext.LeitnerReviews.Add(new LeitnerReview
        {
            OwnerId = ownerId,
            LeitnerCardId = card.Id,
            ReviewedAtUtc = DateTime.UtcNow,
            IsCorrect = isCorrect,
            PreviousBox = previousBox,
            NewBox = newBox
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new LeitnerReviewResult(
            Found: true,
            IsCorrect: isCorrect,
            PreviousBox: previousBox,
            NewBox: newBox,
            NextDueDate: nextDueDate
        );
    }

    private static DateOnly ComputeNextDueDate(DateOnly todayUtc, int box, bool isCorrect)
    {
        if (!isCorrect)
        {
            // Simple et sans boucle infinie : si faux, reviens demain.
            return todayUtc.AddDays(1);
        }

        int safeBox = Math.Clamp(box, 1, BoxIntervalsDays.Length);
        int days = BoxIntervalsDays[safeBox - 1];
        return todayUtc.AddDays(days);
    }
}
