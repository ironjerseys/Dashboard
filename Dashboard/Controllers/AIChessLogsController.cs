using Dashboard.Entities;
using Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Dashboard.Data;

namespace Dashboard.Controllers;

[ApiController]
[Route("api/aichesslogs")]
public class AIChessLogsApiController : ControllerBase
{
    private readonly IAIChessLogService _svc;
    private readonly BlogContext _db;
    public AIChessLogsApiController(IAIChessLogService svc, BlogContext db)
    {
        _svc = svc;
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AIChessLogs dto)
    {
        await Log("Info", "ApiCall", "POST /api/aichesslogs reçu");
        if (dto == null)
        {
            await Log("Warn", "BadRequest", "Payload null" );
            return BadRequest();
        }
        dto.TimestampUtc = DateTime.UtcNow;
        try
        {
            var id = await _svc.AddAsync(dto);
            await Log("Info", "Stored", $"Log Id={id}; Depth={dto.SearchDepth}; DurMs={dto.DurationMs}; Legal={dto.LegalMovesCount}; Eval={dto.EvaluatedMovesCount}; GenTotal={dto.GeneratedMovesTotal}; Nodes={dto.NodesVisited}; Leafs={dto.LeafEvaluations}; Best={dto.BestMoveUci}; Score={dto.BestScoreCp}");
            return Created($"/api/aichesslogs/{id}", new { id });
        }
        catch (Exception ex)
        {
            await Log("Error", "StoreFailed", ex.ToString());
            return StatusCode(500, "Erreur interne");
        }
    }

    private async Task Log(string level, string evt, string? message)
    {
        try
        {
            _db.Logs.Add(new LogEntry
            {
                Level = level,
                Source = nameof(AIChessLogsApiController),
                Event = evt,
                Message = message,
                TimestampUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch { }
    }
}

public class AIChessLogsController : Controller
{
    private readonly IAIChessLogService _svc;
    public AIChessLogsController(IAIChessLogService svc) { _svc = svc; }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = await _svc.GetRecentAsync(500);
        return View(items);
    }
}
