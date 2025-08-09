namespace Dashboard.Models;

public class Question
{
    public int Id { get; set; }
    public string QuestionText { get; set; }  // "Question" dans le JSON → mappé en controller
    public List<string> Choices { get; set; } = new();
    public int CorrectAnswer { get; set; }
    public string Explanation { get; set; }
}
