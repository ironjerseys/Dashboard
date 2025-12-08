using Dashboard.Entities;
using Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Dashboard.Controllers;

[Authorize]
public class QuizAdminController(IDbQuizService quiz, IArticleService articles, IHttpContextAccessor accessor) : Controller
{
    private readonly IDbQuizService _quiz = quiz;
    private readonly IArticleService _articles = articles;
    private readonly IHttpContextAccessor _accessor = accessor;

    [HttpGet]
    public async Task<IActionResult> Create(int? articleId)
    {
        var all = await _articles.GetArticles(sort: ArticleSort.DateNewest);
        ViewBag.Articles = all.Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Titre }).ToList();
        var model = new QuizQuestion { ArticleId = articleId };
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(QuizQuestion model)
    {
        if (!ModelState.IsValid)
        {
            var all = await _articles.GetArticles(sort: ArticleSort.DateNewest);
            ViewBag.Articles = all.Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Titre }).ToList();
            return View(model);
        }
        var id = await _quiz.CreateAsync(model);
        TempData["ok"] = "Question créée";
        // Positionner l'index du quiz sur la question nouvellement créée (fin de liste)
        _accessor.HttpContext?.Session.SetInt32("QuizIndex", id - 1);
        return RedirectToAction("Index", "Quiz");
    }
}
