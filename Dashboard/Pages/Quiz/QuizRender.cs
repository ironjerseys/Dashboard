using System.Text.RegularExpressions;
using Dashboard.Persistance.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Dashboard.Pages.Quiz;

// Helpers partagés par les pages de quiz (Review, AllQuestions, Manage).
// Logique sans état extraite de l'ancien QuestionsTechniques.razor pour
// éviter la duplication entre les trois pages.
public static class QuizRender
{
    public static string GetChoiceText(QuestionTechnique q, int i) => i switch
    {
        0 => q.Choice0,
        1 => q.Choice1,
        2 => q.Choice2,
        3 => q.Choice3,
        _ => string.Empty
    };

    public static bool LooksLikeHtml(string? s)
        => !string.IsNullOrWhiteSpace(s) && Regex.IsMatch(s, @"<[a-zA-Z]");

    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var s = html
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n", StringComparison.OrdinalIgnoreCase);
        return Regex.Replace(s, "<.*?>", string.Empty);
    }

    public static bool HasCodeFence(string? text)
        => !string.IsNullOrEmpty(text) && text.Contains("```", StringComparison.Ordinal);

    public static string ExtractCode(string? text)
    {
        text ??= string.Empty;
        int start = text.IndexOf("```", StringComparison.Ordinal);
        int end = text.LastIndexOf("```", StringComparison.Ordinal);
        return start >= 0 && end > start
            ? text.Substring(start + 3, end - start - 3).Trim()
            : text.Trim();
    }

    public static string MakePreview(string? text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        string n = ToPlainText(text).Replace("\r", " ").Replace("\n", " ").Trim();
        return n.Length <= maxChars ? n : n[..maxChars].TrimEnd() + "…";
    }

    // Rend l'énoncé d'une question : bloc de code si fence ```, HTML si balises,
    // sinon texte brut.
    public static RenderFragment RenderQuestionText(string? text) => builder =>
    {
        string html = text ?? string.Empty;
        string plain = ToPlainText(html);
        if (HasCodeFence(plain))
        {
            builder.OpenElement(0, "pre");
            builder.AddAttribute(1, "class", "mb-3 p-3");
            builder.AddAttribute(2, "style", "background:rgba(0,0,0,.06);border:1px solid rgba(0,0,0,.10);border-radius:12px;overflow:auto;");
            builder.OpenElement(3, "code");
            builder.AddContent(4, ExtractCode(plain));
            builder.CloseElement();
            builder.CloseElement();
            return;
        }
        if (LooksLikeHtml(html))
        {
            builder.OpenElement(10, "div");
            builder.AddAttribute(11, "class", "mb-3 quiz-html");
            builder.AddMarkupContent(12, html);
            builder.CloseElement();
            return;
        }
        builder.OpenElement(20, "p");
        builder.AddAttribute(21, "class", "mb-3");
        builder.AddContent(22, html);
        builder.CloseElement();
    };

    // Rend une valeur courte (un choix) en HTML inline si nécessaire, sinon en texte.
    public static RenderFragment RenderInlineHtmlOrText(string? value) => builder =>
    {
        string html = value ?? string.Empty;
        if (LooksLikeHtml(html))
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "quiz-html");
            builder.AddMarkupContent(2, html);
            builder.CloseElement();
        }
        else
        {
            builder.AddContent(10, html);
        }
    };
}
