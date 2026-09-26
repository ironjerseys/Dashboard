using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Business;

public interface IDbQuizService
{
    Task<List<QuestionTechnique>> GetQuestionsAsync(CancellationToken cancellationToken = default);
    Task<List<QuestionTechnique>> GetActiveQuestionsAsync(CancellationToken cancellationToken = default);
    Task<QuestionTechnique?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(QuestionTechnique quizQuestion, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(QuestionTechnique quizQuestion, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<int> SetActiveAsync(IReadOnlyCollection<int> ids, bool isActive, CancellationToken cancellationToken = default);
}

public sealed class QuestionTechniqueService : IDbQuizService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public QuestionTechniqueService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<QuestionTechnique>> GetQuestionsAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.QuizQuestions
            .AsNoTracking()
            .Include(q => q.Labels)
            .OrderBy(question => question.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<QuestionTechnique>> GetActiveQuestionsAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.QuizQuestions
            .AsNoTracking()
            .Include(q => q.Labels)
            .Where(question => question.IsActive)
            .OrderBy(question => question.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<QuestionTechnique?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.QuizQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(question => question.Id == id, cancellationToken);
    }

    public async Task<int> CreateAsync(QuestionTechnique quizQuestion, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.QuizQuestions.Add(quizQuestion);
        await dbContext.SaveChangesAsync(cancellationToken);

        return quizQuestion.Id;
    }

    public async Task<bool> UpdateAsync(QuestionTechnique quizQuestion, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        QuestionTechnique? existing = await dbContext.QuizQuestions
            .FirstOrDefaultAsync(q => q.Id == quizQuestion.Id, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        existing.QuestionText = quizQuestion.QuestionText;
        existing.Choice0 = quizQuestion.Choice0;
        existing.Choice1 = quizQuestion.Choice1;
        existing.Choice2 = quizQuestion.Choice2;
        existing.Choice3 = quizQuestion.Choice3;
        existing.CorrectAnswer = quizQuestion.CorrectAnswer;
        existing.Explanation = quizQuestion.Explanation;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        QuestionTechnique? existing = await dbContext.QuizQuestions
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        dbContext.QuizQuestions.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<int> SetActiveAsync(IReadOnlyCollection<int> ids, bool isActive, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.QuizQuestions
            .Where(q => ids.Contains(q.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(q => q.IsActive, isActive), cancellationToken);
    }
}
