using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Dashboard.Persistance.Entities;

namespace Dashboard.Business;

/// <summary>Une ligne du classeur de suivi, deja interpretee.</summary>
public sealed record SpreadsheetApplicationRow(
    int RowNumber,
    string Company,
    string? Position,
    string? Location,
    DateTime? AppliedUtc,
    JobApplicationStatus Status,
    string? JobUrl,
    string? Notes);

/// <summary>
/// Lit la premiere feuille d'un classeur .xlsx de candidatures (colonnes Entreprise, Poste,
/// Localisation, Date, Statut, Lien, Contact, Salaire, Prochaine etape, Notes, en francais ou
/// en anglais). Lecture directe du format OpenXML : pas de dependance pour un import ponctuel.
/// </summary>
public static class XlsxApplicationReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.Ordinal)
    {
        ["entreprise"] = "company", ["company"] = "company", ["societe"] = "company",
        ["poste"] = "position", ["position"] = "position", ["titre"] = "position", ["title"] = "position",
        ["localisation"] = "location", ["location"] = "location", ["lieu"] = "location", ["ville"] = "location",
        ["date"] = "date", ["date de candidature"] = "date", ["applied"] = "date",
        ["statut"] = "status", ["status"] = "status",
        ["lien"] = "url", ["link"] = "url", ["url"] = "url",
        ["contact"] = "contact",
        ["salaire"] = "salary", ["salary"] = "salary",
        ["prochaine etape"] = "next", ["next step"] = "next",
        ["notes"] = "notes", ["note"] = "notes"
    };

    public static (List<SpreadsheetApplicationRow> Rows, List<string> Warnings) Read(Stream xlsx)
    {
        var warnings = new List<string>();

        using var archive = new ZipArchive(xlsx, ZipArchiveMode.Read, leaveOpen: true);

        List<string> sharedStrings = ReadSharedStrings(archive);
        List<Dictionary<int, string>> sheet = ReadFirstSheet(archive, sharedStrings);

        int headerIndex = sheet.FindIndex(r => r.Values.Any(v => Alias(v) == "company"));
        if (headerIndex < 0)
        {
            throw new InvalidDataException("Colonne Entreprise (ou Company) introuvable dans la premiere feuille.");
        }

        Dictionary<string, int> columns = sheet[headerIndex]
            .Where(c => Alias(c.Value) is not null)
            .GroupBy(c => Alias(c.Value)!)
            .ToDictionary(g => g.Key, g => g.First().Key);

        var rows = new List<SpreadsheetApplicationRow>();

        for (int i = headerIndex + 1; i < sheet.Count; i++)
        {
            Dictionary<int, string> cells = sheet[i];
            int rowNumber = i + 1;

            string? Get(string key) =>
                columns.TryGetValue(key, out int col) && cells.TryGetValue(col, out string? v) && !string.IsNullOrWhiteSpace(v)
                    ? v.Trim()
                    : null;

            string? company = Get("company");
            if (company is null)
            {
                if (cells.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                {
                    warnings.Add($"Ligne {rowNumber} ignoree : entreprise vide.");
                }
                continue;
            }

            JobApplicationStatus status = ParseStatus(Get("status"), rowNumber, warnings);
            DateTime? applied = ParseDate(Get("date"), rowNumber, warnings);

            // Contact, salaire et prochaine etape n'ont pas de colonne dediee : ils vont dans les notes.
            var notes = new List<string>();
            if (Get("contact") is { } contact) notes.Add("Contact : " + contact);
            if (Get("salary") is { } salary) notes.Add("Salaire : " + salary);
            if (Get("next") is { } next) notes.Add("Prochaine étape : " + next);
            if (Get("notes") is { } note) notes.Add(note);

            rows.Add(new SpreadsheetApplicationRow(
                rowNumber,
                Truncate(company, 256)!,
                Truncate(Get("position"), 256),
                Truncate(Get("location"), 256),
                applied,
                status,
                Truncate(Get("url"), 2048),
                notes.Count == 0 ? null : Truncate(string.Join("\n", notes), 2000)));
        }

        return (rows, warnings);
    }

    private static JobApplicationStatus ParseStatus(string? value, int rowNumber, List<string> warnings)
    {
        if (value is null)
        {
            return JobApplicationStatus.Applied;
        }

        switch (Normalize(value))
        {
            case "envoye" or "envoyee" or "applied" or "sent" or "postule" or "en attente" or "pending" or "en cours":
                return JobApplicationStatus.Applied;
            case "rejected" or "refuse" or "refusee" or "rejete" or "rejetee" or "refus":
                return JobApplicationStatus.Rejected;
            case "entretien" or "interview":
                return JobApplicationStatus.Interview;
            case "test" or "assessment" or "exercice":
                return JobApplicationStatus.Assessment;
            case "offre" or "offer" or "accepte" or "acceptee" or "accepted":
                return JobApplicationStatus.Offer;
            default:
                warnings.Add($"Ligne {rowNumber} : statut \"{value}\" inconnu, compte comme Envoyee.");
                return JobApplicationStatus.Applied;
        }
    }

    private static DateTime? ParseDate(string? value, int rowNumber, List<string> warnings)
    {
        if (value is null)
        {
            return null;
        }

        DateTime date;

        if (DateTime.TryParseExact(value, new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            date = parsed;
        }
        else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial))
        {
            // Cellule au format date : Excel stocke un numero de jour.
            date = DateTime.FromOADate(serial);
        }
        else
        {
            warnings.Add($"Ligne {rowNumber} : date \"{value}\" illisible.");
            return null;
        }

        // Midi UTC : la date reste le meme jour une fois affichee dans n'importe quel fuseau des Ameriques ou d'Europe.
        return new DateTime(date.Year, date.Month, date.Day, 12, 0, 0, DateTimeKind.Utc);
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return new List<string>();
        }

        using Stream stream = entry.Open();
        return XDocument.Load(stream).Root!
            .Elements(Main + "si")
            .Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value)))
            .ToList();
    }

    private static List<Dictionary<int, string>> ReadFirstSheet(ZipArchive archive, List<string> sharedStrings)
    {
        string sheetPath = FirstSheetPath(archive);
        ZipArchiveEntry entry = archive.GetEntry(sheetPath)
            ?? throw new InvalidDataException($"Feuille {sheetPath} absente du classeur.");

        using Stream stream = entry.Open();
        XElement sheetData = XDocument.Load(stream).Root!.Element(Main + "sheetData")
            ?? throw new InvalidDataException("Feuille sans donnees.");

        var rows = new List<Dictionary<int, string>>();

        foreach (XElement row in sheetData.Elements(Main + "row"))
        {
            // Les lignes vides sont absentes du XML : on les recree pour que les numeros restent justes.
            int rowNumber = (int?)row.Attribute("r") ?? rows.Count + 1;
            while (rows.Count < rowNumber - 1)
            {
                rows.Add(new Dictionary<int, string>());
            }

            var cells = new Dictionary<int, string>();

            foreach (XElement cell in row.Elements(Main + "c"))
            {
                string reference = (string?)cell.Attribute("r") ?? "";
                int column = ColumnIndex(reference);
                string? type = (string?)cell.Attribute("t");
                string? raw = cell.Element(Main + "v")?.Value;

                string? value = type switch
                {
                    "s" when int.TryParse(raw, out int index) && index < sharedStrings.Count => sharedStrings[index],
                    "inlineStr" => string.Concat(cell.Descendants(Main + "t").Select(t => t.Value)),
                    _ => raw
                };

                if (column >= 0 && value is not null)
                {
                    cells[column] = value;
                }
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static string FirstSheetPath(ZipArchive archive)
    {
        using (Stream workbookStream = archive.GetEntry("xl/workbook.xml")?.Open()
                                       ?? throw new InvalidDataException("Ce fichier n'est pas un classeur .xlsx."))
        {
            XElement? firstSheet = XDocument.Load(workbookStream).Root!
                .Element(Main + "sheets")?
                .Elements(Main + "sheet")
                .FirstOrDefault();

            string? relationId = (string?)firstSheet?.Attribute(Rel + "id");
            ZipArchiveEntry? relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

            if (relationId is not null && relsEntry is not null)
            {
                using Stream relsStream = relsEntry.Open();
                string? target = XDocument.Load(relsStream).Root!
                    .Elements(PackageRel + "Relationship")
                    .FirstOrDefault(r => (string?)r.Attribute("Id") == relationId)?
                    .Attribute("Target")?.Value;

                if (target is not null)
                {
                    return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
                }
            }
        }

        return "xl/worksheets/sheet1.xml";
    }

    /// <summary>"B12" -> 1 (colonnes numerotees a partir de 0).</summary>
    private static int ColumnIndex(string reference)
    {
        int index = 0;
        int letters = 0;

        foreach (char c in reference)
        {
            if (!char.IsLetter(c))
            {
                break;
            }

            index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            letters++;
        }

        return letters == 0 ? -1 : index - 1;
    }

    private static string? Alias(string header) => HeaderAliases.GetValueOrDefault(Normalize(header));

    private static string Normalize(string value)
    {
        string decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
