using Dashboard.Models;
using Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Controllers;

public class QuizController(IQuizService quiz) : Controller
{
    const string ScoreKey = "QuizScore";
    const string IndexKey = "QuizIndex";

    [HttpGet]
    public IActionResult Index()
    {
        var qs = quiz.GetQuestions();
        var idx = HttpContext.Session.GetInt32(IndexKey) ?? 0;
        var score = HttpContext.Session.GetInt32(ScoreKey) ?? 0;

        if (idx >= qs.Count)
            return View("Finished", (score, total: qs.Count));

        var vm = new QuizViewModel
        {
            Current = qs[idx],
            CurrentIndex = idx,
            Total = qs.Count,
            Score = score
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Answer(int choice)
    {
        var qs = quiz.GetQuestions();
        var idx = HttpContext.Session.GetInt32(IndexKey) ?? 0;
        var score = HttpContext.Session.GetInt32(ScoreKey) ?? 0;

        if (idx >= qs.Count) return RedirectToAction(nameof(Index));

        var q = qs[idx];
        var isCorrect = (choice == q.CorrectAnswer);
        if (isCorrect) score++;

        // on n'avance pas encore : on montre la correction
        HttpContext.Session.SetInt32(ScoreKey, score);

        var vm = new QuizViewModel
        {
            Current = q,
            CurrentIndex = idx,
            Total = qs.Count,
            Score = score,
            ShowResult = true,
            IsCorrect = isCorrect
        };
        return View("Index", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Next()
    {
        var idx = (HttpContext.Session.GetInt32(IndexKey) ?? 0) + 1;
        HttpContext.Session.SetInt32(IndexKey, idx);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Restart()
    {
        HttpContext.Session.Remove(IndexKey);
        HttpContext.Session.Remove(ScoreKey);
        return RedirectToAction(nameof(Index));
    }
}
