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

    // Usings prepended to every submission. Kept as a single constant so the
    // line offset used for error mapping stays in sync automatically.
    private const string Preamble =
        "using System;\n" +
        "using System.Collections.Generic;\n" +
        "using System.Linq;\n" +
        "using System.Reflection;\n" +
        "\n";

    private static readonly int PreambleLineCount = Preamble.Count(c => c == '\n');

    // ── Test harnesses (one per challenge) ──────────────────────────────────
    // Each harness exposes a static __TestRunner__.Run() that returns lines
    // starting with "PASS:" or "FAIL:". Messages are in English (practice).

    private const string SingletonHarness = @"

public static class __TestRunner__
{
    public static string Run()
    {
        var sb = new System.Text.StringBuilder();

        // Test 1: GetInstance() does not return null
        try
        {
            var a = Singleton.GetInstance();
            if (a == null) sb.AppendLine(""FAIL:GetInstance() returned null"");
            else sb.AppendLine(""PASS:GetInstance() returns a valid instance"");
        }
        catch (Exception ex) { sb.AppendLine(""FAIL:GetInstance() threw an exception: "" + ex.Message); }

        // Test 2: two calls return the same instance
        try
        {
            var a = Singleton.GetInstance();
            var b = Singleton.GetInstance();
            if (object.ReferenceEquals(a, b))
                sb.AppendLine(""PASS:GetInstance() always returns the same instance"");
            else
                sb.AppendLine(""FAIL:GetInstance() returns a different instance on each call"");
        }
        catch (Exception ex) { sb.AppendLine(""FAIL:Single-instance test: "" + ex.Message); }

        // Test 3: the constructor is private
        try
        {
            var ctors = typeof(Singleton).GetConstructors(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (ctors.Length == 0)
                sb.AppendLine(""PASS:The constructor is private"");
            else
                sb.AppendLine(""FAIL:The constructor must be private — it is currently public"");
        }
        catch (Exception ex) { sb.AppendLine(""FAIL:Constructor check: "" + ex.Message); }

        return sb.ToString();
    }
}
";

    private const string TwoSumHarness = @"

public static class __TestRunner__
{
    public static string Run()
    {
        var sb = new System.Text.StringBuilder();

        Solution sol;
        try { sol = new Solution(); }
        catch (Exception ex) { sb.AppendLine(""FAIL:Could not create Solution: "" + ex.Message); return sb.ToString(); }

        Check(sb, sol, new int[] { 2, 7, 11, 15 }, 9);
        Check(sb, sol, new int[] { 3, 2, 4 }, 6);
        Check(sb, sol, new int[] { 3, 3 }, 6);
        Check(sb, sol, new int[] { -1, -2, -3, -4, -5 }, -8);

        return sb.ToString();
    }

    static void Check(System.Text.StringBuilder sb, Solution sol, int[] nums, int target)
    {
        string label = ""TwoSum(["" + string.Join("","", nums) + ""], "" + target + "")"";
        try
        {
            var res = sol.TwoSum(nums, target);
            if (res == null) { sb.AppendLine(""FAIL:"" + label + "" returned null""); return; }
            if (res.Length != 2) { sb.AppendLine(""FAIL:"" + label + "" must return exactly 2 indices""); return; }
            int i = res[0], j = res[1];
            if (i < 0 || j < 0 || i >= nums.Length || j >= nums.Length) { sb.AppendLine(""FAIL:"" + label + "" returned out-of-range indices""); return; }
            if (i == j) { sb.AppendLine(""FAIL:"" + label + "" returned the same index twice""); return; }
            if (nums[i] + nums[j] != target) { sb.AppendLine(""FAIL:"" + label + "" indices do not add up to the target""); return; }
            sb.AppendLine(""PASS:"" + label + "" returns valid indices"");
        }
        catch (Exception ex) { sb.AppendLine(""FAIL:"" + label + "" threw: "" + ex.Message); }
    }
}
";

    private const string ThreeSumHarness = @"

public static class __TestRunner__
{
    public static string Run()
    {
        var sb = new System.Text.StringBuilder();

        Solution sol;
        try { sol = new Solution(); }
        catch (Exception ex) { sb.AppendLine(""FAIL:Could not create Solution: "" + ex.Message); return sb.ToString(); }

        Check(sb, sol, new int[] { -1, 0, 1, 2, -1, -4 }, new int[][] { new int[] { -1, -1, 2 }, new int[] { -1, 0, 1 } });
        Check(sb, sol, new int[] { 0, 1, 1 }, new int[][] { });
        Check(sb, sol, new int[] { 0, 0, 0 }, new int[][] { new int[] { 0, 0, 0 } });
        Check(sb, sol, new int[] { -2, 0, 1, 1, 2 }, new int[][] { new int[] { -2, 0, 2 }, new int[] { -2, 1, 1 } });

        return sb.ToString();
    }

    static string Norm(System.Collections.Generic.IEnumerable<int> t)
    {
        var a = System.Linq.Enumerable.ToList(t);
        a.Sort();
        return string.Join("","", a);
    }

    static void Check(System.Text.StringBuilder sb, Solution sol, int[] nums, int[][] expected)
    {
        string label = ""ThreeSum(["" + string.Join("","", nums) + ""])"";
        try
        {
            var res = sol.ThreeSum(nums);
            if (res == null) { sb.AppendLine(""FAIL:"" + label + "" returned null""); return; }

            var expectedSet = new System.Collections.Generic.HashSet<string>();
            foreach (var e in expected) expectedSet.Add(Norm(e));

            var actualSet = new System.Collections.Generic.HashSet<string>();
            int count = 0;
            foreach (var triplet in res)
            {
                count++;
                if (triplet == null) { sb.AppendLine(""FAIL:"" + label + "" contains a null triplet""); return; }
                var list = System.Linq.Enumerable.ToList(triplet);
                if (list.Count != 3) { sb.AppendLine(""FAIL:"" + label + "" contains a triplet that is not made of exactly 3 numbers""); return; }
                if (list[0] + list[1] + list[2] != 0) { sb.AppendLine(""FAIL:"" + label + "" contains a triplet that does not sum to 0""); return; }
                actualSet.Add(Norm(list));
            }

            if (count != actualSet.Count) { sb.AppendLine(""FAIL:"" + label + "" contains duplicate triplets""); return; }
            if (!actualSet.SetEquals(expectedSet))
            {
                sb.AppendLine(""FAIL:"" + label + "" expected "" + expectedSet.Count + "" unique triplet(s) but the result did not match"");
                return;
            }
            sb.AppendLine(""PASS:"" + label + "" returns the correct unique triplets"");
        }
        catch (Exception ex) { sb.AppendLine(""FAIL:"" + label + "" threw: "" + ex.Message); }
    }
}
";

    // ── Public API ──────────────────────────────────────────────────────────

    public Task<ChallengeRunResult> ValidateSingletonAsync(string userCode)
        => RunAsync(userCode, SingletonHarness, "SingletonChallenge_");

    public Task<ChallengeRunResult> ValidateTwoSumAsync(string userCode)
        => RunAsync(userCode, TwoSumHarness, "TwoSumChallenge_");

    public Task<ChallengeRunResult> ValidateThreeSumAsync(string userCode)
        => RunAsync(userCode, ThreeSumHarness, "ThreeSumChallenge_");

    // ── Compile + run pipeline (shared by every challenge) ───────────────────

    private async Task<ChallengeRunResult> RunAsync(string userCode, string testHarness, string assemblyPrefix)
    {
        if (string.IsNullOrWhiteSpace(userCode))
            return new ChallengeRunResult(false, null,
                [new ChallengeTestResult(false, "The code is empty — write your implementation before validating.")]);

        var fullSource = Preamble + userCode + "\n" + testHarness;

        var syntaxTree = CSharpSyntaxTree.ParseText(fullSource);
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyPrefix + Guid.NewGuid().ToString("N"),
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
                    var userLine = span.StartLinePosition.Line + 1 - PreambleLineCount;
                    return $"Line {Math.Max(1, userLine)}: {d.GetMessage()}";
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
                [new ChallengeTestResult(false, "Timed out (10 s) — your code may contain an infinite loop.")]);
        }
        catch (Exception ex)
        {
            return new ChallengeRunResult(false, null,
                [new ChallengeTestResult(false, "Unexpected runtime error: " + ex.Message)]);
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
