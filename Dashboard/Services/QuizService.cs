using Dashboard.Models;
using System.Text.Json;

namespace Dashboard.Services;


public interface IQuizService
{
    List<Question> GetQuestions();
}
public class QuizService(IWebHostEnvironment env) : IQuizService
{
    private readonly IWebHostEnvironment _env = env;
    private List<Question>? _cache;

    public List<Question> GetQuestions()
    {
        if (_cache != null) return _cache;
        var path = Path.Combine(_env.WebRootPath, "questions.json");
        var json = System.IO.File.ReadAllText(path);
        // ton JSON est en PascalCase (Id, Question, Choices…)
        var raw = JsonSerializer.Deserialize<List<JsonQuestion>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        _cache = raw.Select(q => new Question
        {
            Id = q.Id,
            QuestionText = q.Question,
            Choices = q.Choices ?? [],
            CorrectAnswer = q.CorrectAnswer,
            Explanation = q.Explanation
        }).ToList();
        return _cache;
    }
    private class JsonQuestion
    {
        public int Id { get; set; }
        public string Question { get; set; } = "";
        public List<string>? Choices { get; set; }
        public int CorrectAnswer { get; set; }
        public string Explanation { get; set; } = "";
    }
}
