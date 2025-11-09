using System.Collections.Generic;
using Dashboard.Models;

namespace Dashboard.Models;

public class CalendarCell
{
    public DateOnly? Date { get; set; }          // null = cellule vide
    public string CssClass { get; set; } = "";   // classes bootstrap
    public string? Style { get; set; }            // style inline forcé pour garantie couleur
    public int? DayNumber => Date?.Day;
}

public class DashboardPageViewModel
{
    public required List<Goal> Goals { get; set; }
    public required List<Todo> Todos { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public Dictionary<DateOnly, bool> MonthMap { get; set; } = new();
    public Goal? SelectedGoal { get; set; }
    public List<List<CalendarCell>> CalendarRows { get; set; } = new();
}
