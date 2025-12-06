using Dashboard.Data;
using Dashboard.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Dashboard.Controllers;

[Authorize]
public class EmailSettingsController : Controller
{
    private readonly BlogContext _db;
    private readonly UserManager<IdentityUser> _userMgr;

    public EmailSettingsController(BlogContext db, UserManager<IdentityUser> userMgr)
    {
        _db = db;
        _userMgr = userMgr;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var settings = await _db.EmailSettings.FirstOrDefaultAsync(x => x.UserId == userId) ?? new EmailSettings
        {
            UserId = userId,
            RecipientEmail = await _userMgr.GetEmailAsync(await _userMgr.GetUserAsync(User)) ?? string.Empty
        };
        return View(settings);
    }

    [HttpPost]
    public async Task<IActionResult> Index(EmailSettings model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        if (userId != model.UserId) model.UserId = userId;
        if (!ModelState.IsValid) return View(model);
        var existing = await _db.EmailSettings.FirstOrDefaultAsync(x => x.UserId == userId);
        if (existing == null)
        {
            _db.EmailSettings.Add(model);
        }
        else
        {
            existing.Enabled = model.Enabled;
            existing.Frequency = model.Frequency;
            existing.Hour = model.Hour;
            existing.Minute = model.Minute;
            existing.DayOfWeek = model.DayOfWeek;
            existing.DayOfMonth = model.DayOfMonth;
            existing.RecipientEmail = model.RecipientEmail;
        }
        await _db.SaveChangesAsync();
        TempData["ok"] = "Paramètres enregistrés";
        return RedirectToAction(nameof(Index));
    }
}
