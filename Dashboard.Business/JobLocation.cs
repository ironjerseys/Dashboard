using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Dashboard.Business;

/// <summary>
/// Region d'une candidature. <see cref="ProvinceCode"/> est null quand le lieu est absent,
/// hors Canada ou trop vague ("Canada", "Remote") ; <see cref="City"/> est la ville ou
/// l'agglomeration qui regroupe les banlieues (Scarborough et Markham comptent pour Toronto).
/// </summary>
public sealed record JobRegion(string? ProvinceCode, string? City)
{
    public static readonly JobRegion Unknown = new(null, null);

    public string ProvinceName => ProvinceCode is null ? "" : JobLocation.ProvinceNames[ProvinceCode];
}

/// <summary>
/// Ramene un lieu ecrit librement ("Canada-Quebec-Montreal", "Scarborough, ON, CA",
/// "Longueuil, QC J4H 4A6") a une province et une agglomeration, pour des statistiques par region.
/// </summary>
public static partial class JobLocation
{
    public static readonly IReadOnlyDictionary<string, string> ProvinceNames = new Dictionary<string, string>
    {
        ["QC"] = "Québec",
        ["ON"] = "Ontario",
        ["BC"] = "British Columbia",
        ["AB"] = "Alberta",
        ["MB"] = "Manitoba",
        ["SK"] = "Saskatchewan",
        ["NS"] = "Nova Scotia",
        ["NB"] = "New Brunswick",
        ["NL"] = "Newfoundland and Labrador",
        ["PE"] = "Prince Edward Island",
        ["YT"] = "Yukon",
        ["NT"] = "Northwest Territories",
        ["NU"] = "Nunavut"
    };

    /// <summary>Noms de province, normalises (minuscules, sans accents ni ponctuation).</summary>
    private static readonly Dictionary<string, string> ProvinceByName = new()
    {
        ["quebec"] = "QC",
        ["ontario"] = "ON",
        ["british columbia"] = "BC",
        ["colombie britannique"] = "BC",
        ["alberta"] = "AB",
        ["manitoba"] = "MB",
        ["saskatchewan"] = "SK",
        ["nova scotia"] = "NS",
        ["nouvelle ecosse"] = "NS",
        ["new brunswick"] = "NB",
        ["nouveau brunswick"] = "NB",
        ["newfoundland"] = "NL",
        ["newfoundland and labrador"] = "NL",
        ["terre neuve"] = "NL",
        ["prince edward island"] = "PE",
        ["ile du prince edouard"] = "PE",
        ["yukon"] = "YT",
        ["northwest territories"] = "NT",
        ["nunavut"] = "NU"
    };

    /// <summary>Ville normalisee -> (province, agglomeration affichee).</summary>
    private static readonly Dictionary<string, (string Province, string City)> Cities = BuildCities();

    /// <summary>Plus long nombre de mots d'une ville connue, pour chercher "richmond hill" avant "richmond".</summary>
    private static readonly int MaxCityWords = Cities.Keys.Max(k => k.Count(c => c == ' ') + 1);

    /// <summary>"CA" est exclu : il veut aussi dire Canada ("Scarborough, ON, CA").</summary>
    private static readonly HashSet<string> UsStates = new(StringComparer.Ordinal)
    {
        "AL", "AK", "AZ", "AR", "CO", "CT", "DE", "DC", "FL", "GA", "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME",
        "MD", "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ", "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA",
        "RI", "SC", "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY"
    };

    /// <summary>Premiere lettre du code postal -> province.</summary>
    private static readonly Dictionary<char, string> ProvinceByPostalLetter = new()
    {
        ['A'] = "NL", ['B'] = "NS", ['C'] = "PE", ['E'] = "NB",
        ['G'] = "QC", ['H'] = "QC", ['J'] = "QC",
        ['K'] = "ON", ['L'] = "ON", ['M'] = "ON", ['N'] = "ON", ['P'] = "ON",
        ['R'] = "MB", ['S'] = "SK", ['T'] = "AB", ['V'] = "BC", ['Y'] = "YT"
    };

    public static JobRegion Resolve(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return JobRegion.Unknown;
        }

        // Les codes de province ("ON", "QC") se lisent sur le texte d'origine : "on" en minuscules est un mot anglais.
        List<string> codes = CodeToken().Matches(location).Select(m => m.Value).ToList();
        string? provinceFromCode = codes.FirstOrDefault(ProvinceNames.ContainsKey);

        // "Richmond, VA", "Cambridge, MA" : un etat americain sans province canadienne, c'est hors Canada.
        if (provinceFromCode is null && codes.Any(UsStates.Contains))
        {
            return JobRegion.Unknown;
        }

        string? provinceFromPostal = PostalCode().Match(location) is { Success: true } postal
            ? ProvinceByPostalLetter.GetValueOrDefault(postal.Value[0])
            : null;

        List<string> words = Normalize(location).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        (string Province, string City)? city = FindCity(words);
        string? explicitProvince = provinceFromCode ?? FindProvinceName(words);

        // Une province ecrite l'emporte sur une homonymie ("Windsor, Nova Scotia" n'est pas Windsor, Ontario).
        if (city is not null && explicitProvince is not null && explicitProvince != city.Value.Province)
        {
            city = null;
        }

        string? province = city?.Province ?? explicitProvince ?? provinceFromPostal;

        // "Quebec" seul designe la province ; accompagne de "QC" ou d'une autre mention de la province, la ville.
        if (city is null && province == "QC" && words.Count(w => w == "quebec") + (provinceFromCode == "QC" ? 1 : 0) >= 2)
        {
            return new JobRegion("QC", "Québec");
        }

        return new JobRegion(province, city?.City);
    }

    private static (string Province, string City)? FindCity(List<string> words)
    {
        for (int length = Math.Min(MaxCityWords, words.Count); length >= 1; length--)
        {
            for (int start = 0; start + length <= words.Count; start++)
            {
                string candidate = string.Join(' ', words.Skip(start).Take(length));

                if (Cities.TryGetValue(candidate, out var city))
                {
                    return city;
                }
            }
        }

        return null;
    }

    private static string? FindProvinceName(List<string> words)
    {
        string text = " " + string.Join(' ', words) + " ";

        return ProvinceByName
            .OrderByDescending(p => p.Key.Length)
            .Where(p => text.Contains(" " + p.Key + " ", StringComparison.Ordinal))
            .Select(p => p.Value)
            .FirstOrDefault();
    }

    /// <summary>Minuscules, sans accents, ponctuation et tirets remplaces par des espaces ("Saint-Laurent" -> "saint laurent").</summary>
    private static string Normalize(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return builder.ToString();
    }

    private static Dictionary<string, (string, string)> BuildCities()
    {
        var cities = new Dictionary<string, (string, string)>();

        void Add(string province, string metro, params string[] names)
        {
            foreach (string name in names)
            {
                cities[Normalize(name).Trim()] = (province, metro);
            }
        }

        Add("ON", "Toronto", "Toronto", "GTA", "Greater Toronto Area", "North York", "Scarborough", "Etobicoke",
            "Markham", "Mississauga", "Brampton", "Vaughan", "Woodbridge", "Concord", "Thornhill", "Richmond Hill",
            "Oakville", "Burlington", "Milton", "Pickering", "Ajax", "Whitby", "Oshawa", "Newmarket", "Aurora");
        Add("ON", "Ottawa", "Ottawa", "Kanata", "Nepean", "Orleans");
        Add("ON", "Waterloo", "Waterloo", "Kitchener", "Cambridge");
        Add("ON", "Hamilton", "Hamilton");
        Add("ON", "Guelph", "Guelph");
        Add("ON", "Windsor", "Windsor");
        Add("ON", "Kingston", "Kingston");

        Add("QC", "Montréal", "Montreal", "Montréal", "Grand Montreal", "Greater Montreal", "Laval", "Longueuil",
            "Saint-Laurent", "St-Laurent", "Ville Saint-Laurent", "Brossard", "Boucherville", "Pointe-Claire", "Dorval",
            "Verdun", "Lachine", "Anjou", "Saint-Leonard", "Mont-Royal", "Kirkland", "Terrebonne", "Repentigny",
            "Boisbriand", "Blainville", "Saint-Jerome", "Vaudreuil", "Chateauguay", "Saint-Hubert", "Candiac");
        Add("QC", "Québec", "Quebec City", "Ville de Quebec", "Levis", "Sainte-Foy");
        Add("QC", "Gatineau", "Gatineau");
        Add("QC", "Sherbrooke", "Sherbrooke");
        Add("QC", "Trois-Rivières", "Trois-Rivieres");
        Add("QC", "Drummondville", "Drummondville");
        Add("QC", "Saguenay", "Saguenay", "Chicoutimi");

        Add("BC", "Vancouver", "Vancouver", "Burnaby", "Surrey", "Richmond", "Coquitlam", "New Westminster",
            "North Vancouver", "West Vancouver", "Delta", "Langley");
        Add("BC", "Victoria", "Victoria");
        Add("BC", "Kelowna", "Kelowna");

        Add("AB", "Calgary", "Calgary");
        Add("AB", "Edmonton", "Edmonton");
        Add("MB", "Winnipeg", "Winnipeg");
        Add("SK", "Regina", "Regina");
        Add("SK", "Saskatoon", "Saskatoon");
        Add("NS", "Halifax", "Halifax", "Dartmouth");
        Add("NB", "Moncton", "Moncton");
        Add("NB", "Fredericton", "Fredericton");
        Add("NL", "St. John's", "St John's", "Saint John's");

        return cities;
    }

    [GeneratedRegex(@"(?<![A-Za-z])[A-Z]{2}(?![A-Za-z])")]
    private static partial Regex CodeToken();

    [GeneratedRegex(@"(?<![A-Za-z0-9])[ABCEGHJ-NPRSTVXY]\d[A-Z] ?\d[A-Z]\d(?![A-Za-z0-9])")]
    private static partial Regex PostalCode();
}
