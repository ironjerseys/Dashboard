using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dashboard.Entities;
using Dashboard.Services;

namespace Dashboard.Controllers;

public class ArticlesController : Controller
{
    private readonly IArticleService _articleService;
    private readonly IAuthorizationService _AuthorizationService;
    private readonly IDbQuizService _quizService;

    public ArticlesController(IArticleService articleService, IAuthorizationService authorizationService, IDbQuizService quizService)
    {
        _articleService = articleService;
        _AuthorizationService = authorizationService;
        _quizService = quizService;
    }

    public async Task<IActionResult> Index([FromQuery] int[] labels, [FromQuery] ArticleSort sort = ArticleSort.DateNewest, [FromQuery] string? search = null)
    {
        var articles = await _articleService.GetArticles(labels, sort, search);

        if (!User.IsInRole("Admin"))
        {
            articles = articles.Where(a => a.IsPublic);
        }

        ViewBag.Labels = await _articleService.GetLabels();
        ViewBag.SelectedLabelIds = labels ?? Array.Empty<int>();
        ViewBag.SelectedSort = sort;
        ViewBag.Search = search ?? string.Empty;
        return View(articles);
    }

    public async Task<IActionResult> Details(int id)
    {
        var article = await _articleService.GetArticle(id);

        if (!User.IsInRole("Admin") && !article.IsPublic)
        {
            return NotFound();
        }

            ViewBag.CreateQuestionUrl = Url.Action("Create", "QuizAdmin", new { articleId = id });
        ViewBag.Questions = await _quizService.GetByArticleAsync(id);
        return View(article);
    }

    [Authorize]
    public async Task<IActionResult> Create()
    {
        ViewBag.Labels = await _articleService.GetLabels();
        return View();
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(Article article, [FromForm] string[]? newLabels, [FromForm] int[]? selectedLabelIds)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Labels = await _articleService.GetLabels();
            return View(article);
        }
        await _articleService.CreateArticle(article, newLabels, selectedLabelIds);
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var article = await _articleService.GetArticle(id);
        ViewBag.Labels = await _articleService.GetLabels();
        return View(article);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Edit(int id, Article article, [FromForm] string[]? newLabels, [FromForm] int[]? selectedLabelIds)
    {
        if (id != article.Id) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.Labels = await _articleService.GetLabels();
            return View(article);
        }
        await _articleService.UpdateArticle(article, newLabels, selectedLabelIds);
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var article = await _articleService.GetArticle(id);
        if (article == null) return NotFound();
        return View(article);
    }

    [Authorize]
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _articleService.Delete(id);
        return RedirectToAction(nameof(Index));
    }
}