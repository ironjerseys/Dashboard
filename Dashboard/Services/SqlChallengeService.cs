using System.Text.RegularExpressions;
using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

// Jeu de résultats renvoyé par une requête (colonnes + lignes).
public sealed record SqlResultSet(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows);

public sealed record SqlChallengeRunResult(
    bool HasError,
    string? Error,
    bool Passed,
    SqlResultSet? UserResult,
    SqlResultSet? ExpectedResult
);

public interface ISqlChallengeService
{
    Task<IReadOnlyList<SqlChallenge>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SqlChallenge?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<SqlChallengeRunResult> ValidateAsync(SqlChallenge challenge, string userSql, CancellationToken cancellationToken = default);
    Task EnsureSeedAsync(CancellationToken cancellationToken = default);
}

public class SqlChallengeService : ISqlChallengeService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    // Nombre maximum de lignes renvoyées pour éviter un résultat gigantesque.
    private const int MaxRows = 500;
    private const int CommandTimeoutSeconds = 5;

    // Mots-clés interdits dans la requête utilisateur (défense en profondeur :
    // la base est de toute façon jetable et en mémoire).
    private static readonly string[] ForbiddenKeywords =
    {
        "ATTACH", "DETACH", "PRAGMA", "INSERT", "UPDATE", "DELETE", "DROP",
        "ALTER", "CREATE", "REPLACE", "VACUUM", "REINDEX", "ANALYZE",
        "TRIGGER", "load_extension", "writefile", "readfile"
    };

    public SqlChallengeService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<SqlChallenge>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.SqlChallenges
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<SqlChallenge?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using BlogContext db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.SqlChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<SqlChallengeRunResult> ValidateAsync(
        SqlChallenge challenge,
        string userSql,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userSql))
            return new SqlChallengeRunResult(true, "La requête est vide — écris ton SELECT avant de valider.", false, null, null);

        string? validationError = ValidateReadOnly(userSql);
        if (validationError is not null)
            return new SqlChallengeRunResult(true, validationError, false, null, null);

        // Base SQLite privée, en mémoire, recréée à chaque appel : aucun accès
        // à la vraie base de données SQL Server de l'application.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);

        try
        {
            await ExecuteScriptAsync(connection, challenge.SchemaSql, cancellationToken);
            if (!string.IsNullOrWhiteSpace(challenge.SeedSql))
                await ExecuteScriptAsync(connection, challenge.SeedSql, cancellationToken);

            // Résultat attendu, calculé côté serveur via la requête solution.
            SqlResultSet expected = await RunQueryAsync(connection, challenge.SolutionSql, cancellationToken);

            // Résultat de l'utilisateur.
            SqlResultSet userResult;
            try
            {
                userResult = await RunQueryAsync(connection, userSql, cancellationToken);
            }
            catch (SqliteException ex)
            {
                return new SqlChallengeRunResult(true, "Erreur SQL : " + ex.Message, false, null, null);
            }

            bool passed = ResultsMatch(userResult, expected, challenge.OrderMatters);
            return new SqlChallengeRunResult(false, null, passed, userResult, expected);
        }
        catch (SqliteException ex)
        {
            // Erreur dans le schéma/seed de la question elle-même.
            return new SqlChallengeRunResult(true, "Erreur de configuration de la question : " + ex.Message, false, null, null);
        }
    }

    public async Task EnsureSeedAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (await db.SqlChallenges.AnyAsync(cancellationToken))
            return;

        db.SqlChallenges.Add(new SqlChallenge
        {
            Title = "Utilisateurs de plus de 30 ans",
            Prompt = "La table `users` contient des utilisateurs avec leur âge. " +
                     "Écris une requête qui sélectionne le nom (`name`) et l'âge (`age`) " +
                     "de tous les utilisateurs de plus de 30 ans, triés par âge croissant.",
            SchemaSql = "CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL, age INTEGER NOT NULL);",
            SeedSql =
                "INSERT INTO users (id, name, age) VALUES " +
                "(1, 'Alice', 25), " +
                "(2, 'Bob', 34), " +
                "(3, 'Charlie', 41), " +
                "(4, 'Diana', 30), " +
                "(5, 'Erin', 52);",
            SolutionSql = "SELECT name, age FROM users WHERE age > 30 ORDER BY age ASC;",
            OrderMatters = true,
            Hint = "Utilise WHERE age > 30 puis ORDER BY age.",
            SortOrder = 0
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    // ── Sécurité : on n'autorise qu'une seule instruction de lecture ──
    private static string? ValidateReadOnly(string sql)
    {
        string trimmed = sql.Trim().TrimEnd(';').Trim();

        if (trimmed.Length == 0)
            return "La requête est vide.";

        // Une seule instruction : pas de point-virgule au milieu.
        if (trimmed.Contains(';'))
            return "Une seule requête est autorisée (pas de point-virgule séparant plusieurs instructions).";

        // Doit commencer par SELECT ou WITH (CTE).
        if (!Regex.IsMatch(trimmed, @"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase))
            return "Seules les requêtes de lecture (SELECT) sont autorisées.";

        foreach (string keyword in ForbiddenKeywords)
        {
            if (Regex.IsMatch(trimmed, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase))
                return $"Mot-clé non autorisé : {keyword}.";
        }

        return null;
    }

    private static async Task ExecuteScriptAsync(SqliteConnection connection, string script, CancellationToken ct)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = script;
        command.CommandTimeout = CommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<SqlResultSet> RunQueryAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);

        var columns = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
            columns.Add(reader.GetName(i));

        var rows = new List<IReadOnlyList<string?>>();
        while (await reader.ReadAsync(ct) && rows.Count < MaxRows)
        {
            var row = new string?[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
            rows.Add(row);
        }

        return new SqlResultSet(columns, rows);
    }

    private static bool ResultsMatch(SqlResultSet user, SqlResultSet expected, bool orderMatters)
    {
        // Le nombre de colonnes et de lignes doit correspondre.
        if (user.Columns.Count != expected.Columns.Count)
            return false;
        if (user.Rows.Count != expected.Rows.Count)
            return false;

        IReadOnlyList<IReadOnlyList<string?>> userRows = user.Rows;
        IReadOnlyList<IReadOnlyList<string?>> expectedRows = expected.Rows;

        if (!orderMatters)
        {
            userRows = user.Rows.OrderBy(r => r, RowComparer.Instance).ToList();
            expectedRows = expected.Rows.OrderBy(r => r, RowComparer.Instance).ToList();
        }

        for (int i = 0; i < userRows.Count; i++)
        {
            if (!RowEquals(userRows[i], expectedRows[i]))
                return false;
        }

        return true;
    }

    private static bool RowEquals(IReadOnlyList<string?> a, IReadOnlyList<string?> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    // Ordonne deux lignes cellule par cellule (NULL avant toute valeur).
    private sealed class RowComparer : IComparer<IReadOnlyList<string?>>
    {
        public static readonly RowComparer Instance = new();

        public int Compare(IReadOnlyList<string?>? x, IReadOnlyList<string?>? y)
        {
            if (x is null || y is null)
                return (x is null ? 0 : 1) - (y is null ? 0 : 1);

            int count = Math.Min(x.Count, y.Count);
            for (int i = 0; i < count; i++)
            {
                int c = string.CompareOrdinal(x[i], y[i]);
                if (c != 0)
                    return c;
            }
            return x.Count - y.Count;
        }
    }
}
