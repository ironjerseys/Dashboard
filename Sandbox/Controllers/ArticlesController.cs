using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sandbox.Data;
using Sandbox.Models;

namespace Sandbox.Controllers;

public class ArticlesController : Controller
{
    private readonly BlogContext _db;
    public ArticlesController(BlogContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Articles.OrderByDescending(a => a.DateCreation).ToListAsync());

    public IActionResult Create() => View();
    [HttpPost] public async Task<IActionResult> Create(Article a)
    {
        if (!ModelState.IsValid) return View(a);
        _db.Add(a); await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return NotFound();
        return View(article);    
    }
        

    [HttpPost] public async Task<IActionResult> Edit(int id, Article a)
    {
        if (id != a.Id) return NotFound();
        if (!ModelState.IsValid) return View(a);
        _db.Update(a); await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return NotFound();
        return View(article);
    }

    [HttpPost, ActionName("Delete")] public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var art = await _db.Articles.FindAsync(id);
        if (art is null) return NotFound();
        _db.Remove(art); await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return NotFound();
        return View(article);
    }
}