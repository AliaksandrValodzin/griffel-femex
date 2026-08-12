# Load combinations — Implementation Summary

Implemented `Claude/FEMEX_LoadCombinations.md` in full, both open decisions settled. Clean build
(0 warnings, 0 errors); 103 tests pass (was 85).

## New files

| File | What |
| --- | --- |
| `Loads/Combinations/LoadCombination.cs` | `Number`, `Label`, `LimitState`, `CombinationType`, `IncludeInDesignEnvelope`, `Terms` |
| `Loads/Combinations/LoadCombinationTerm.cs` | `(LoadCaseNumber, Factor)` — a value, not an entity: no id, addressed by position |
| `Loads/Combinations/LimitState.cs` | `Unspecified` / `Ultimate` / `Serviceability` / `Accidental` |
| `Loads/Combinations/LoadCombinationType.cs` | `LinearAdd` / `Envelope` / `AbsoluteAdd` / `Srss` |
| `FemexModel.LoadCombinations.cs` | `GetDesignEnvelope`, `FindLoadCombination`, `GetTotalFactor`, `NextLoadCombinationNumber` |
| `griffel-femex.Tests/LoadCombinationTests.cs` | 8 facts: the envelope partition, the flag, repeated terms, the two id helpers |

## Modified

- **`FemexModel.cs`** — gained `LoadCombinations`, placed after `Loads` so the JSON keeps its
  referenced-before-referencer order (`LoadCases` → `Loads` → `LoadCombinations`).
- **`FemexModel.Validation.cs`** — `ValidateLoadCombinations` (errors) immediately after
  `ValidateLoads`, `ValidateLoadCombinationUsage` (warnings) last; one `ReportDuplicates` line for
  the combination number space. `ValidationContext` gained nothing: the only cross-reference is to
  `LoadCaseNumbers`, which it already built.
- **`Examples/Example1.femex`** — a seven-combination `loadCombinations` block after `loads`.
- **`griffel-femex.Tests/SampleModels.cs`** — three combinations and their number consts.
- **`griffel-femex.Tests/RoundTripTests.cs`**, **`ValidationTests.cs`** — 1 and 9 new facts, plus
  two literals and four assertions added to existing ones.

## The two rules the helpers exist to state

Both are things the format would otherwise say only in prose, and that a consumer could therefore
disagree with:

```csharp
model.GetDesignEnvelope(LimitState.Ultimate)   // that limit state, IncludeInDesignEnvelope set, in list order
model.GetTotalFactor(101, 1)                   // repeated terms for one case add
```

`GetDesignEnvelope` never crosses a limit state — an envelope of strength and deflection results
together means nothing — and a limit state nothing uses envelopes to empty rather than to
everything. `GetTotalFactor` implements the ETABS rule that two terms naming case 1 at 0.9 and 0.5
factor it by 1.4.

The entity's XML doc carries both, plus the one this entity makes easy to conflate: `Envelope` the
`CombinationType` and `IncludeInDesignEnvelope` the flag are unrelated. The first says how *this
combination's own terms* combine; the second says whether the combination takes part in the
per-limit-state design envelope. A combination can be `LinearAdd` and in the envelope, or
`Envelope` and out of it.

## Validation

Three errors — a combination that cannot be evaluated:

```
Duplicate load combination number 101.
Load combination 102 has no terms.
Load combination 101 references unknown load case 99.
```

Three warnings — evaluable, but a receiver will probably get it wrong:

```
Load combination 101 includes load case 1 more than once; the factors add.
More than one load combination is labelled "U1". A program that keys combinations by name
cannot tell them apart.
Load combination 102 has no label; a program that keys combinations by name will invent one.
```

The repeat is deliberately a warning rather than the error a repeated node in a contour gets:
repeating a case with two factors is legal in ETABS and sums to something, whereas a repeated
contour node is meaningless. The two label checks are the format's answer to §4.6 of the interop
review — Robot, ETABS and SAF all key combinations by *name*, so both a name they cannot tell
apart and a name they have to invent are collisions waiting on export, even though FEMEX itself
references combinations by number throughout.

The duplicate-label warning is one message per duplicated label, not one per colliding pair, via
the `seen`/`reported` double-`HashSet` idiom `ReportDuplicates` already uses. Both validators skip
a combination whose number was already reported as a duplicate, following `ValidateGrids`.

## Deviations from the plan

1. **Open decision 1 answered with the third warning.** `Label` stays `string?`, matching
   `LoadCase.Label`, and a combination without one is warned about rather than rejected. The
   planned `Accepts_TwoCombinationsWithNullLabels` became `NullLabels_DoNotCollideWithEachOther`,
   because two null labels now raise two warnings — the point it guards is that neither of them is
   the *duplicate*-label warning: an absent name is not a name two combinations share.
2. **Open decision 2 answered id-based.** `GetTotalFactor(int combinationNumber, int loadCaseNumber)`,
   matching `FindGrid(int)` / `GetGridsForLevel(int)` and every other public helper. An unknown
   combination answers `0.0`, the same as a case absent from a known one; the XML doc says so
   outright rather than leaving it to be discovered.
3. **The no-label check is `string.IsNullOrWhiteSpace`, not `is null`.** A blank label cannot be
   keyed by name either, and this is what the grid label checks already do. A blank label is
   therefore reported as "no label" rather than treated as a name two combinations could share.
4. **`FemexModel.Validation.cs` gained no `using`.** The plan expected six files to take one, but
   the validators name no type from `griffel_femex.Loads.Combinations` — they iterate
   `LoadCombinations` with `var` — so the using would have been dead. Five files, not six.
5. **`LoadCombinationTerm` has a convenience constructor and a `ToString()`.** The plan's table
   lists only the two members; `new LoadCombinationTerm(1, 1.2)` keeps the fixture and the tests
   readable, and follows what every other small entity in the repo does.
6. **`Example1.femex`'s fifth ULS combination is `1.2G + Tu + 0.4Q`**, not the plan's
   `1.2G + 1.5Q + 1.0T`. The plan flagged its own version as the loosest of the seven against
   AS/NZS 1170.0, which pairs an other-action with a ψc-factored `Q`; relabelling was the cheaper
   of the two options it offered, and the set stays representative rather than merely illustrative.
7. **One test beyond the plan's list**, `Warns_OnceForThreeCombinationsSharingALabel`, pinning the
   one-message-per-label rule the way the coincident-node suite pins its equivalent.
8. **Verification step 5 is a permanent assertion, not a one-off.**
   `Example1_LoadsAndValidates` now asserts the ULS envelope returns 5, the SLS envelope returns
   only combination 201, and `GetTotalFactor(105, 1)` is 1.2 — the flag, the limit-state partition
   and the term resolution exercised together against the reference file.

## Verified

- `dotnet build`: 0 warnings, 0 errors. `dotnet test`: 103 passed, 0 failed.
- `Assert.Empty(SampleModels.Build().Validate())` still holds with the three combinations in it —
  distinct, non-null labels, so nothing trips the new warnings.
- The emitted JSON matches the planned shape: `"loadCombinations"` after `"loads"`, camelCase keys,
  `"limitState"` and `"combinationType"` as PascalCase strings, `"includeInDesignEnvelope"` on
  every combination including the true ones, and no `"type"` key anywhere in the block.
- The hand-authored `Example1.femex` block is **byte-identical** to what the serializer emits for
  it, `+` escapes included, so the reference file does not drift on the next round-trip.

## Still open

- **Nested envelope combinations are lost.** A combination that envelopes a *named subset* has
  nowhere to land: FEMEX has one envelope per limit state. An importer should drop such a
  combination and report it rather than flatten it into something that means less. The same gap
  seen from the other side is "multiple named envelopes per limit state", which a `DesignEnvelope`
  entity could add later without changing anything built here.
- **No code-generated combinations.** Reserved in `LoadCombination`'s XML doc only, per decision 5
  and §4.1 of the review.
- **`Factor` is not sanity-checked.** NaN, infinity and a factor of zero all pass. The repo
  validates no numeric field anywhere, and starting with this one would be inconsistent.
- **The combinations factor superimposed dead load only**, because FEMEX still has no self-weight
  (review §4.3). That is the next gap, not this one.
