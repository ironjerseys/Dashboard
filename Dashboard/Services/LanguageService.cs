namespace Dashboard.Services;

public class LanguageService
{
    public string Current { get; private set; } = "fr";

    public event Action? OnChange;

    public void Set(string lang)
    {
        if (lang != "fr" && lang != "en")
        {
            return;
        }

        if (Current == lang)
        {
            return;
        }

        Current = lang;
        OnChange?.Invoke();
    }

    public string Translate(string fr, string en)
    {
        return Current == "en" ? en : fr;
    }
}
