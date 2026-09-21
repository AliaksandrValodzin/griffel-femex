using System.Text;
using System.Text.Json;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The C# half of the validation parity harness.
    ///
    /// <b>The premise.</b> <c>femex-viewer.html</c> independently mirrors this
    /// engine in JavaScript — fifteen validator families and both migrations — and it
    /// does so <i>deliberately</i>: <c>FEMEXViewer.md</c> says the viewer "reads
    /// <c>.femex</c> files handed to it by the user and has no build-time link to the
    /// C# code", and that independence is worth keeping. It opens from
    /// <c>file://</c>, survives being emailed, needs no install and cannot rot when a
    /// CDN moves.
    ///
    /// <b>And drift has not happened.</b> Three of those fifteen arrived with 1.7 and
    /// 1.8, along with the unit migration and the restraint sense; the viewer tracks
    /// <c>CURRENT_SCHEMA_VERSION</c> and cites the implementation summaries by
    /// filename. Two bumps, mirrored by hand, in step.
    ///
    /// <b>Which is the argument for this file, not against it.</b> A practice that
    /// has survived twice on discipline alone, with no stated rule and no automated
    /// check, is exactly the thing to lock down before judgement rules start arriving
    /// from paying engagements — because the day it fails, the free checker and the
    /// paid report disagree about the same file, which is the <i>confidently
    /// incorrect</i> failure class landed on the flagship claim.
    ///
    /// <b>The shape avoids reintroducing the build-time link.</b> This test owns the
    /// artefact — the C# engine is authoritative — and, when asked, writes
    /// <c>Examples/&lt;name&gt;.expected.json</c>. The viewer's headless run reads the
    /// same file. Neither repository references the other; both reference a file.
    /// </summary>
    public class ValidationParityTests
    {
        /// <summary>
        /// camelCase and unescaped, matching what <c>FemexModel.JsonOptions</c>
        /// writes: the artefact sits beside the files it is about and is read by a
        /// person as often as by a script, and <c>"</c> in the middle of a
        /// sentence makes a diff unreadable.
        /// </summary>
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        };

        public static IEnumerable<object[]> Examples()
        {
            foreach (string path in Directory.GetFiles(ExamplesDirectory, "*.femex"))
                yield return new object[] { Path.GetFileNameWithoutExtension(path) };
        }

        /// <summary>
        /// Set to <c>1</c> to let the parity test rewrite the artefacts:
        /// <c>dotnet test -e FEMEX_UPDATE_BASELINES=1</c>. <c>dotnet test</c> has no
        /// way to hand a custom switch such as <c>--update-baselines</c> to a test, so
        /// the environment is the switch.
        /// </summary>
        public const string UpdateBaselinesVariable = "FEMEX_UPDATE_BASELINES";

        /// <summary>
        /// Every example's <c>Validate()</c> output, checked against the artefact
        /// beside it.
        ///
        /// By default it only asserts, and fails with the findings that differ. A
        /// fresh clone must be green and leave <c>git status</c> clean, and a test
        /// that edits checked-in files on a stranger's first run is neither.
        ///
        /// With <see cref="UpdateBaselinesVariable"/> set it rewrites the artefact, so
        /// an intended change is a diff the author reviews rather than a number to
        /// copy by hand — and it still fails when it rewrites. A test that silently
        /// regenerated its own baseline would assert nothing.
        /// </summary>
        [Theory]
        [MemberData(nameof(Examples))]
        public void TheEngineOutput_MatchesTheCheckedInArtefact(string name)
        {
            string modelPath = Path.Combine(ExamplesDirectory, name + ".femex");
            string artefactPath = Path.Combine(ExamplesDirectory, name + ".expected.json");

            string produced = Render(FemexModel.Load(modelPath));
            string? checkedIn = File.Exists(artefactPath) ? File.ReadAllText(artefactPath) : null;

            if (string.Equals(Normalise(produced), Normalise(checkedIn), StringComparison.Ordinal))
                return;

            if (UpdatingBaselines)
            {
                File.WriteAllText(artefactPath, produced, new UTF8Encoding(false));

                Assert.Fail(checkedIn is null
                    ? $"{name}.expected.json did not exist and has been written. Review it and commit it: " +
                      "it is what the viewer's parity run will be held to."
                    : $"{name}.expected.json disagreed with the engine and has been rewritten. Review the " +
                      "diff. If the change is intended, the viewer's JavaScript mirror has to move with it " +
                      "before release — that is the rule this artefact exists to police.");
            }

            string remedy =
                $"If the change is intended, run `dotnet test -e {UpdateBaselinesVariable}=1` to rewrite the " +
                "artefact, review the diff and commit it — and move the viewer's JavaScript mirror with it " +
                "before release. That is the rule this artefact exists to police.";

            Assert.Fail(checkedIn is null
                ? $"{name}.expected.json does not exist. {remedy}"
                : $"{name}.expected.json disagrees with the engine. " +
                  "(- is the checked-in artefact, + is the engine)\n" +
                  Describe(checkedIn, produced) + "\n" + remedy);
        }

        [Fact]
        public void EveryExample_HasAnArtefact()
        {
            // The copy rule in the csproj is per-file and not a glob, and so is this:
            // a new example that gains no artefact is a file the viewer's parity run
            // will not check, and it would be silent about it.
            foreach (string path in Directory.GetFiles(ExamplesDirectory, "*.femex"))
            {
                string artefact = Path.ChangeExtension(path, ".expected.json");
                Assert.True(File.Exists(artefact),
                            $"{Path.GetFileName(path)} has no {Path.GetFileName(artefact)} beside it.");
            }
        }

        [Fact]
        public void TheArtefact_IsOrdered_AndCarriesSeverity()
        {
            // Ordered, because "the same set in a different order" is a difference the
            // viewer could have and this would not catch. Severity, because the
            // viewer's panel shows warnings and the report shows both, and a rule
            // that changed severity would otherwise pass unnoticed.
            var model = new FemexModel { SchemaVersion = "1.99" };
            model.Bars.Add(new Geometry.Bar(1, 1, 2, 1, 1));

            string rendered = Render(model);

            Assert.Contains("\"severity\": \"Error\"", rendered);
            Assert.Contains("\"severity\": \"Warning\"", rendered);
            Assert.StartsWith("[", rendered.TrimStart());
        }

        private static string Render(FemexModel model)
        {
            var rows = new List<Finding>();
            foreach (ValidationMessage message in model.Validate())
                rows.Add(new Finding { Severity = message.Severity.ToString(), Text = message.Text });

            return JsonSerializer.Serialize(rows, Options);
        }

        private static string? Normalise(string? text)
        {
            return text?.Replace("\r\n", "\n").TrimEnd();
        }

        private static bool UpdatingBaselines
        {
            get
            {
                string? value = Environment.GetEnvironmentVariable(UpdateBaselinesVariable);
                return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// The difference as findings, not as JSON: a finding is five lines of
        /// braces and keys, and what the author needs to see is which sentences
        /// arrived, left or changed severity. A longest-common-subsequence walk, so
        /// an order change shows as one finding leaving and arriving elsewhere.
        /// </summary>
        private static string Describe(string checkedIn, string produced)
        {
            List<string>? before = Findings(checkedIn);
            List<string>? after = Findings(produced);

            if (before is null || after is null)
                return "The checked-in artefact is not a findings array; compare it by hand.";

            int[,] common = new int[before.Count + 1, after.Count + 1];
            for (int i = before.Count - 1; i >= 0; i--)
                for (int j = after.Count - 1; j >= 0; j--)
                    common[i, j] = before[i] == after[j]
                        ? common[i + 1, j + 1] + 1
                        : Math.Max(common[i + 1, j], common[i, j + 1]);

            var lines = new StringBuilder();
            int b = 0, a = 0;
            while (b < before.Count || a < after.Count)
            {
                if (b < before.Count && a < after.Count && before[b] == after[a])
                {
                    b++;
                    a++;
                }
                else if (b < before.Count && (a == after.Count || common[b + 1, a] >= common[b, a + 1]))
                {
                    lines.Append("- ").AppendLine(before[b++]);
                }
                else
                {
                    lines.Append("+ ").AppendLine(after[a++]);
                }
            }

            return lines.Length == 0
                ? "The findings are the same; only the file's formatting differs."
                : lines.ToString().TrimEnd();
        }

        private static List<string>? Findings(string json)
        {
            try
            {
                List<Finding>? rows = JsonSerializer.Deserialize<List<Finding>>(json, Options);
                return rows?.ConvertAll(row => row.Severity + ": " + row.Text);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// The repository's own <c>Examples</c> folder, not the copy in the test
        /// output: this test writes the artefact, and writing it beside the binaries
        /// would produce a file nobody ever commits.
        /// </summary>
        private static string ExamplesDirectory
        {
            get
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory is not null)
                {
                    string candidate = Path.Combine(directory.FullName, "Examples");
                    if (File.Exists(Path.Combine(directory.FullName, "griffel-femex.csproj")) &&
                        Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    directory = directory.Parent;
                }

                throw new DirectoryNotFoundException(
                    "Could not find the repository's Examples folder above " + AppContext.BaseDirectory);
            }
        }

        private sealed class Finding
        {
            public string Severity { get; set; } = string.Empty;

            public string Text { get; set; } = string.Empty;
        }
    }
}
