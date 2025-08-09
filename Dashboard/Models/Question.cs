namespace Dashboard.Models;

public class Question
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = "";
    public List<string> Choices { get; set; } = new();
    public int CorrectAnswer { get; set; }
    public string Explanation { get; set; } = "";
}
