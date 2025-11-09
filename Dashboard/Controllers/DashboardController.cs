using Dashboard.Models;
using Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Dashboard.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ITodoService _todos;
    private readonly IGoalService _goals;
    private readonly IArticleService _articles;

    public DashboardController(ITodoService todos, IGoalService goals, IArticleService articles)
    {
        _todos = todos;
        _goals = goals;
        _articles = articles;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index(int? year = null, int? month = null, int? editGoalId = null)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        var coverageMap = await _goals.GetMonthlyCoverageMapAsync(UserId, y, m);

        var first = new DateOnly(y, m, 1);
        var last = new DateOnly(y, m, DateTime.DaysInMonth(y, m));
        var goals = (await _goals.GetMyGoalsAsync(UserId))
            .Where(g => g.Debut <= last && g.Fin >= first)
            .ToList();

        var todos = await _todos.GetAllAsync();
        ViewBag.Articles = new SelectList(await _articles.GetArticles(), nameof(Article.Id), nameof(Article.Titre));

        var selected = editGoalId.HasValue ? goals.FirstOrDefault(g => g.Id == editGoalId.Value) : null;

        var calendarRows = BuildCalendarRows(y, m, coverageMap);

        var vm = new DashboardPageViewModel
        {
            Goals = goals,
            Todos = todos,
            Year = y,
            Month = m,
            MonthMap = coverageMap,
            SelectedGoal = selected,
            CalendarRows = calendarRows
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGoal(int? id, string titre, string? description, DateOnly debut, DateOnly fin, int? articleId, bool? isDone)
    {
        if (fin < debut)
            ModelState.AddModelError("Fin", "Fin doit être >= Début");

        var userId = UserId;
        if (!ModelState.IsValid)
            return await Index(editGoalId: id);

        var ctx = HttpContext.RequestServices.GetRequiredService<Dashboard.Data.BlogContext>();

        if (id.HasValue)
        {
            var g = await ctx.Goals.FirstOrDefaultAsync(x => x.Id == id.Value && x.OwnerId == userId);
            if (g == null) return NotFound();
            g.Titre = titre;
            g.Description = description;
            g.Debut = debut;
            g.Fin = fin;
            g.ArticleId = articleId;
            if (isDone.HasValue) g.IsDone = isDone.Value;
        }
        else
        {
            ctx.Goals.Add(new Goal
            {
                Titre = titre,
                Description = description,
                Debut = debut,
                Fin = fin,
                ArticleId = articleId,
                OwnerId = userId,
                IsDone = false
            });
        }
        await ctx.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDone(int id)
    {
        await _goals.ToggleDoneAsync(id, UserId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGoal(int id)
    {
        var ctx = HttpContext.RequestServices.GetRequiredService<Dashboard.Data.BlogContext>();
        var g = await ctx.Goals.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == UserId);
        if (g != null)
        {
            ctx.Goals.Remove(g);
            await ctx.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private static List<List<CalendarCell>> BuildCalendarRows(int year, int month, Dictionary<DateOnly, bool> coverageMap)
    {
        var rows = new List<List<CalendarCell>>();
        var firstDay = new DateTime(year, month, 1);
        int startCol = ((int)firstDay.DayOfWeek + 6) % 7; // Monday=0
        int daysInMonth = DateTime.DaysInMonth(year, month);
        int day = 1;

        for (int r = 0; r < 6 && day <= daysInMonth; r++)
        {
            var row = new List<CalendarCell>(7);
            for (int c = 0; c < 7; c++)
            {
                if (r == 0 && c < startCol || day > daysInMonth)
                {
                    row.Add(new CalendarCell { Date = null, CssClass = "bg-light" });
                }
                else
                {
                    var date = new DateOnly(year, month, day);
                    string css;
                    string style;
                    if (coverageMap.TryGetValue(date, out var allDone))
                    {
                        css = allDone ? "bg-success text-white" : "bg-danger text-white";
                        style = allDone ? "background-color:#198754;color:#fff;" : "background-color:#dc3545;color:#fff;";
                    }
                    else
                    {
                        css = "bg-light";
                        style = "background-color:#f8f9fa;";
                    }
                    row.Add(new CalendarCell { Date = date, CssClass = css, Style = style });
                    day++;
                }
            }
            rows.Add(row);
        }
        return rows;
    }
}
