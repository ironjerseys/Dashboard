using Dashboard.Data;
using Dashboard.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public interface IDbQuizService
{
    Task<List<QuizQuestion>> GetQuestionsAsync(CancellationToken cancellationToken = default);
    Task<QuizQuestion?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(QuizQuestion quizQuestion, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(QuizQuestion quizQuestion, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<List<QuizQuestion>> GetByArticleAsync(int articleId, CancellationToken cancellationToken = default);
}

public sealed class DbQuizService : IDbQuizService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public DbQuizService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<QuizQuestion>> GetQuestionsAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.QuizQuestions
            .AsNoTracking()
            .OrderBy(question => question.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<QuizQuestion?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.QuizQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(question => question.Id == id, cancellationToken);
    }

    public async Task<int> CreateAsync(QuizQuestion quizQuestion, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.QuizQuestions.Add(quizQuestion);
        await dbContext.SaveChangesAsync(cancellationToken);

        return quizQuestion.Id;
    }

    public async Task<bool> UpdateAsync(QuizQuestion quizQuestion, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        QuizQuestion? existing = await dbContext.QuizQuestions
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
        existing.ArticleId = quizQuestion.ArticleId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        QuizQuestion? existing = await dbContext.QuizQuestions
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        dbContext.QuizQuestions.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<List<QuizQuestion>> GetByArticleAsync(int articleId, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.QuizQuestions
            .AsNoTracking()
            .Where(q => q.ArticleId == articleId)
            .OrderBy(q => q.Id)
            .ToListAsync(cancellationToken);
    }
}
