namespace Dashboard.Models;

public class CalendarCell
{
    public DateOnly? Date { get; set; }          // null = cellule vide
    public string CssClass { get; set; } = "";   // classes bootstrap
    public string? Style { get; set; }            // style inline forcé pour garantie couleur
    public int? DayNumber => Date?.Day;
}


