namespace griffel_femex.Interop.Conformance
{
    /// <summary>What one Tier-1 rule found.</summary>
    public sealed class ConformanceCheck
    {
        private static readonly string[] Nothing = new string[0];

        private ConformanceCheck(string name, string rule, bool passed, bool skipped,
                                 IReadOnlyList<string> findings)
        {
            Name = name;
            Rule = rule;
            Passed = passed;
            Skipped = skipped;
            Findings = findings;
        }

        /// <summary>The rule's short name, as §7.3 lists it.</summary>
        public string Name { get; }

        /// <summary>What the rule says, in one line, so a failure explains itself.</summary>
        public string Rule { get; }

        public bool Passed { get; }

        /// <summary>
        /// The check could not run against this adapter, with <see cref="Findings"/>
        /// saying why. Skipped is <b>not</b> passed: a rule that cannot be checked is
        /// recorded as unchecked, because a suite that quietly reports green for
        /// what it never ran is the failure the two tiers exist to prevent.
        /// </summary>
        public bool Skipped { get; }

        /// <summary>One line per thing wrong, or per reason for skipping.</summary>
        public IReadOnlyList<string> Findings { get; }

        public static ConformanceCheck Pass(string name, string rule)
        {
            return new ConformanceCheck(name, rule, passed: true, skipped: false, Nothing);
        }

        public static ConformanceCheck Fail(string name, string rule, IReadOnlyList<string> findings)
        {
            return new ConformanceCheck(name, rule, passed: false, skipped: false, findings);
        }

        public static ConformanceCheck Skip(string name, string rule, string why)
        {
            return new ConformanceCheck(name, rule, passed: false, skipped: true, new[] { why });
        }

        public override string ToString()
        {
            string verdict = Skipped ? "SKIP" : Passed ? "PASS" : "FAIL";
            string findings = Findings.Count == 0 ? string.Empty : "\n    " + string.Join("\n    ", Findings);
            return $"{verdict}  {Name} — {Rule}{findings}";
        }
    }
}
