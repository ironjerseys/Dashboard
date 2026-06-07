namespace Dashboard.Services;

public class LanguageService
{
    public string Current { get; private set; } = "en";

    public event Action? OnChange;

    public void Set(string lang) { }

    public string Translate(string fr, string en) => en;
}
