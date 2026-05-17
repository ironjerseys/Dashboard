using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.Loader;

namespace Dashboard.Services;

public record ChallengeTestResult(bool Passed, string Message);
public record ChallengeRunResult(bool HasCompileError, string? CompileErrors, IReadOnlyList<ChallengeTestResult> TestResults);

public class CodeChallengeService
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> _refs = new(BuildReferences);

    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        var paths = ((string)(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? ""))
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return paths
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
    }

    private const string TestHarness = @"

public static class __TestRunner__
{
    public static string Run()
    {
        var sb = new System.Text.StringBuilder();

        // Test 1 : GetInstance() ne retourne pas null
        try
        {
            var a = Singleton.GetInstance();
            if (a == null) sb.AppendLine(""FAIL:GetInstance() a retourné null"");
            else sb.AppendLine(""PASS:GetInstance() retourne une instance valide"");
        }
        catch (Exception ex) { sb.AppendLine(""FAIL:GetInstance() a levé une exception : "" + ex.Message); }

        // Test 2 : Deux appels retournent la même instance
        try
        {
            var a = Singleton.GetInstance();
            var b = Singleton.GetInstance();
            if (object.ReferenceEquals(a, b))
                sb.AppendLine(""PASS:GetInstance() retourne toujours la même instance"");
            else
                sb.AppendLine(""FAIL:GetInstance() retourne des instances différentes à chaque appel"");
        }
        catch (Exception ex) { sb.AppendLine(""FAIL:Test instance unique : "" + ex.Message); }

        // Test 3 : Constructeur privé
        try
        {
            var ctors = typeof(Singleton).GetConstructors(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (ctors.Length == 0)
                sb.AppendLine(""PASS:Le constructeur est bien privé"");
            else
                sb.AppendLine(""FAIL:Le constructeur doit être privé — il est actuellement public"");
        }
        catch (Exception ex) { sb.AppendLine(""FAIL:Vérification du constructeur : "" + ex.Message); }

        // Test 4 : Thread-safety — 8 threads doivent obtenir la même instance
        try
        {
            var instances = new Singleton[8];
            var threads = new System.Threading.Thread[8];
            for (int i = 0; i < 8; i++)
            {
                var idx = i;
                threads[idx] = new System.Threading.Thread(() =>
                {
                    try { instances[idx] = Singleton.GetInstance(); }
                    catch { /* l'instance reste null */ }
                });
            }
            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join(2000);

            bool allNonNull = instances.All(inst => inst != null);
            bool allSame = allNonNull && instances.All(inst => object.ReferenceEquals(inst, instances[0]));

            if (allSame)
                sb.AppendLine(""PASS:Thread-safe — 8 threads obtiennent la même instance"");
            else if (!allNonNull)
                sb.AppendLine(""FAIL:Thread-safety non vérifiable — GetInstance() a échoué dans certains threads"");
            else
                sb.AppendLine(""FAIL:Non thread-safe — des instances différentes ont été créées en parallèle"");
        }
        catch (Exception ex) { sb.AppendLine(""FAIL:Test thread-safety : "" + ex.Message); }

        return sb.ToString();
    }
}
";

    private const int PreambleLines = 3;

    public async Task<ChallengeRunResult> ValidateSingletonAsync(string userCode)
    {
        if (string.IsNullOrWhiteSpace(userCode))
            return new ChallengeRunResult(false, null,
                [new ChallengeTestResult(false, "Le code est vide — écris ton implémentation avant de valider.")]);

        var fullSource =
            "using System;\n" +
            "using System.Reflection;\n" +
            "using System.Linq;\n" +
            "\n" +
            userCode +
            "\n" +
            TestHarness;

        var syntaxTree = CSharpSyntaxTree.ParseText(fullSource);
        var compilation = CSharpCompilation.Create(
            assemblyName: "SingletonChallenge_" + Guid.NewGuid().ToString("N"),
            syntaxTrees: [syntaxTree],
            references: _refs.Value,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d =>
                {
                    var span = d.Location.GetLineSpan();
                    var userLine = span.StartLinePosition.Line + 1 - PreambleLines;
                    return $"Ligne {Math.Max(1, userLine)} : {d.GetMessage()}";
                }));
            return new ChallengeRunResult(true, errors, []);
        }

        var assemblyBytes = ms.ToArray();

        string output;
        try
        {
            output = await Task.Run(() =>
            {
                var ctx = new AssemblyLoadContext(null, isCollectible: true);
                try
                {
                    using var stream = new MemoryStream(assemblyBytes);
                    var asm = ctx.LoadFromStream(stream);
                    var runner = asm.GetType("__TestRunner__")!;
                    var method = runner.GetMethod("Run")!;
                    return (string)method.Invoke(null, null)!;
                }
                finally
                {
                    ctx.Unload();
                }
            }).WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            return new ChallengeRunResult(false, null,
                [new ChallengeTestResult(false, "Délai dépassé (10 s) — ton code contient peut-être une boucle infinie.")]);
        }
        catch (Exception ex)
        {
            return new ChallengeRunResult(false, null,
                [new ChallengeTestResult(false, "Erreur d'exécution inattendue : " + ex.Message)]);
        }

        var results = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("PASS:", StringComparison.Ordinal) || line.StartsWith("FAIL:", StringComparison.Ordinal))
            .Select(line => new ChallengeTestResult(
                line.StartsWith("PASS:", StringComparison.Ordinal),
                line[5..]))
            .ToList();

        return new ChallengeRunResult(false, null, results);
    }
}
