using Dashboard.Entities;
using Dashboard.Models;
using Dashboard.Data;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public interface IDbQuizService
{
    Task<List<Question>> GetQuestionsAsync();
    Task<QuizQuestion?> GetAsync(int id);
    Task<int> CreateAsync(QuizQuestion q);
    Task<List<QuizQuestion>> GetByArticleAsync(int articleId);
}

public class DbQuizService(BlogContext db) : IDbQuizService
{
    private readonly BlogContext _db = db;

    public async Task<List<Question>> GetQuestionsAsync()
    {
        var qs = await _db.QuizQuestions.AsNoTracking().OrderBy(q => q.Id).ToListAsync();
        return qs.Select(q => new Question
        {
            Id = q.Id,
            QuestionText = q.QuestionText,
            Choices = new List<string> { q.Choice0, q.Choice1, q.Choice2, q.Choice3 },
            CorrectAnswer = q.CorrectAnswer,
            Explanation = q.Explanation
        }).ToList();
    }

    public Task<QuizQuestion?> GetAsync(int id) => _db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == id);

    public async Task<int> CreateAsync(QuizQuestion q)
    {
        _db.QuizQuestions.Add(q);
        await _db.SaveChangesAsync();
        return q.Id;
    }

    public Task<List<QuizQuestion>> GetByArticleAsync(int articleId)
        => _db.QuizQuestions.Where(q => q.ArticleId == articleId).OrderBy(q => q.Id).ToListAsync();
}
