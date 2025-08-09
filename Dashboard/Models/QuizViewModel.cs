namespace Dashboard.Models;

public class QuizViewModel
{
    public Question Current { get; set; } = new();
    public int CurrentIndex { get; set; }
    public int Total { get; set; }
    public int Score { get; set; }
    public bool ShowResult { get; set; }
    public bool IsCorrect { get; set; }
}
