using Dashboard.Models;
using Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace Dashboard.Controllers;

[Authorize]
public class GoalsController : Controller
{
    private readonly IGoalService _svc;
    private readonly IArticleService _articles;

    public GoalsController(IGoalService svc, IArticleService articles)
    {
        _svc = svc;
        _articles = articles;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Single page: calendar + goals list + create
    public async Task<IActionResult> Index(int? year = null, int? month = null)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        ViewBag.Year = y;
        ViewBag.Month = m;

        ViewData["MonthMap"] = await _svc.GetMonthlyWeekMapAsync(UserId, y, m);

        // Goals for the selected month (any week-start in month)
        var first = new DateOnly(y, m, 1);
        var last = new DateOnly(y, m, DateTime.DaysInMonth(y, m));
        var mondayFirst = NormalizeToMonday(first);
        var mondayLast = NormalizeToMonday(last);

        var goals = (await _svc.GetMyGoalsAsync(UserId))
            .Where(g => g.WeekStart >= mondayFirst && g.WeekStart <= mondayLast)
            .ToList();

        // Articles for linking
        var articles = await _articles.GetArticles();
        ViewBag.Articles = new SelectList(articles, nameof(Article.Id), nameof(Article.Titre));

        return View(goals);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string titre, string? description, DateOnly weekStart, int? articleId)
    {
        var goal = new Goal
        {
            Titre = titre,
            Description = description,
            WeekStart = weekStart,
            ArticleId = articleId,
            OwnerId = UserId
        };
        await _svc.CreateAsync(goal);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDone(int id)
    {
        await _svc.ToggleDoneAsync(id, UserId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateArticle(int id, int? articleId)
    {
        await _svc.UpdateArticleAsync(id, UserId, articleId);
        return RedirectToAction(nameof(Index));
    }

    private static DateOnly NormalizeToMonday(DateOnly date)
    {
        int delta = (int)date.DayOfWeek - (int)DayOfWeek.Monday;
        if (delta < 0) delta += 7;
        return date.AddDays(-delta);
    }
}
