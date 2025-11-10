using Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dashboard.Entities; // updated namespace for Todo entity

namespace Dashboard.Controllers;

[Authorize]
public class TodosController : Controller
{
    private readonly ITodoService _svc;
    public TodosController(ITodoService svc) { _svc = svc; }

    // MVC pages
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var todo = await _svc.GetAsync(id);
        if (todo == null) return NotFound();
        return View(todo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            ModelState.AddModelError("Description", "La description est requise");
            var todo = await _svc.GetAsync(id);
            return View(todo ?? new Todo { Id = id, Description = description ?? string.Empty });
        }
        var ok = await _svc.UpdateAsync(id, description);
        if (!ok) return NotFound();
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var todo = await _svc.GetAsync(id);
        if (todo == null) return NotFound();
        return View(todo);
    }

    // API endpoints (used by Dashboard JS)
    [HttpGet("/api/todo")]
    public async Task<IActionResult> Get() => Ok(await _svc.GetAllAsync());

    [HttpPost("/api/todo")]
    public async Task<IActionResult> Create([FromBody] TodoCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Description)) return BadRequest();
        var id = await _svc.CreateAsync(dto.Description);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpGet("/api/todo/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var t = await _svc.GetAsync(id);
        return t == null ? NotFound() : Ok(t);
    }

    [HttpPut("/api/todo/{id:int}")]
    public async Task<IActionResult> UpdateApi(int id, [FromBody] TodoCreateDto dto)
    {
        var ok = await _svc.UpdateAsync(id, dto.Description);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("/api/todo/{id:int}")]
    public async Task<IActionResult> DeleteApi(int id)
    {
        var ok = await _svc.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }
}

public record TodoCreateDto(string Description);
