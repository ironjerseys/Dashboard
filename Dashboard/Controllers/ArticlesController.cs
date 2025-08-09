using Dashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.Controllers;

using Services;

[Authorize]
public class ArticlesController : Controller
{
    private readonly IArticleService _articleService;
    private readonly IAuthorizationService _auth;

    public ArticlesController(IArticleService articleService, IAuthorizationService auth)
    {
        _articleService = articleService;
        _auth = auth;
    }


    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var articles = _articleService.GetArticles().Result;
        return View(articles);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var article = _articleService.GetArticle(id).Result;
        return View(article);
    }

    public IActionResult Create() => View();
    [HttpPost]
    public async Task<IActionResult> Create(Article article)
    {
        _articleService.CreateArticle(article);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        Article article = _articleService.GetArticle(id).Result;
        return View(article);
    }


    [HttpPost]
    public async Task<IActionResult> Edit(int id, Article article)
    {
        if (id != article.Id) return NotFound();
        if (!ModelState.IsValid) return View(article);
        _articleService.UpdateArticle(article);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        Article article = _articleService.GetArticle(id).Result;
        if (article == null) return NotFound();
        return View(article);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _articleService.Delete(id);
        return RedirectToAction(nameof(Index));
    }
}