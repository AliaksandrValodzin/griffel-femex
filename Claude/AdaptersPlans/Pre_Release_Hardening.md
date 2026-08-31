# Plan — pre-release hardening, before the first engineer sees anything

## Context

Phases 0 → D of `SAF_Adapter.md` have all landed. Verified against the working tree at
`d2fa11e` plus the uncommitted Phase D work: eight projects, both legs clean, **616 tests
green**, the CLI converts all eleven corpus workbooks to eleven self-contained reports and
an index, and `parity-check.ps1` is 5 of 5.

Four things stand between that and putting the tool in front of a small group of engineers.
Three of them were found by pointing the existing machinery at the real SAF corpus rather
than at `Examples/`; the fourth is that the repository has never had a licence file.

1. **Nothing is distributable.** There is no `LICENSE` in any of the three repositories,
   though Apache-2.0 is asserted in `FEMEX_BusinessModel.md` §6 and in a
   `griffel-femex.csproj` comment. And `femex.exe` ships **EPPlus 4.5.3.3, LGPL-3.0** — the
   csproj reasons the compliance correctly and then nothing carries the licence text. That
   is legal rather than cosmetic, and it is the one item that blocks handing over a binary
   or a repository link at all.
2. **The importer manufactures 11 of the house workbook's 25 errors.** A line load hosted on
   a plate edge is given bar-only properties it cannot resolve. The first thing an engineer
   will do is convert their own file, and errors that are the tool's own fault are the one
   thing that cannot happen when confidence is the product.
3. **The viewer's mirror has drifted and the harness cannot see it.** One message differs by
   punctuation; the five-example corpus produces eight messages and never reaches it.
4. **The Check section's first screen reads as an accusation** about the engineer's model,
   when the Transfer section three inches below already explains every one of those
   findings.

The intended outcome is a build that can be handed to a named engineer with a licence
attached, a report whose errors are all about their structure, and a parity harness with
enough corpus behind it to be worth trusting.

**The order is 1 → 2 → 4 → 3.** Items 2 and 4 both change what a converted model and its
findings contain, so item 3's checked-in fixtures must be generated last or they are born
stale.

---

## Item 1 — `LICENSE` and `NOTICE` in all three repositories

Apache-2.0 across `griffel-femex`, `griffel-femex-viewer` and `griffel-femex-models`, per
`FEMEX_BusinessModel.md` §6: the format, the library, the SAF adapter, the conformance suite
and the `LossCategory` taxonomy are given away; the hosted service, the report, the
cross-model matching heuristics and the judgement check rules are what is kept. **The
licence file is what makes the first half of that sentence true.** Until it exists, the
open-core position is an assertion in a planning document.

Each repository gets:

- `LICENSE` — the Apache-2.0 text, verbatim.
- `NOTICE` — the Apache-2.0 attribution notice, plus the third-party section below.
- `README.md` in `griffel-femex` — the other two already have one. What the library is, the
  three CLI verbs, the licence, and, explicitly, that the report states findings and
  provenance and is not an engineering opinion. Decision 9 keeps *certify* out of the
  product; the README is where a reader first looks for the claim and should find its
  absence stated rather than merely observed.

### The third-party section

Verified from the resolved graph — `dotnet list package --include-transitive` against
`griffel-femex.Cli`, with each licence read from the `.nuspec` in the local package cache
rather than assumed. Only `griffel-femex.Adapters.Saf` and the CLI carry any of this;
`griffel-femex.Reporting` and the viewer carry none, which is the property both were built
for.

| Package | Version | Licence |
|---|---|---|
| **EPPlus** | 4.5.3.3 | **LGPL-3.0** — the last LGPL release; 5.x is Polyform Noncommercial |
| StructuralAnalysisFormat, `.Bootstrappers.SimpleInjector5` | 1.7.3 | Apache-2.0 |
| FluentValidation | 10.2.3 | Apache-2.0 |
| SimpleInjector | 5.2.1 | MIT |
| UnitsNet | 4.72.0 | MIT-0 |
| System.Text.Json (`netstandard2.0` leg only) | 8.0.5 | MIT |
| `Microsoft.Extensions.*`, `System.*`, `runtime.*` | — | MIT / Apache-2.0, .NET Foundation |

**The EPPlus entry is the one that has to be right**, and note the version: the package
declares **LGPL-3.0**, not the 2.1 a reader might assume, and carries the text as
`lgpl-3.0.txt`. LGPL-3.0 consumed as an unmodified NuGet-delivered assembly, never
referenced in source by our own code, is the ordinary arrangement for Apache-2.0 code — but
the relinking freedom only survives if the notice and the licence text travel with the
binary. So:

- `NOTICE` names EPPlus, its version and its licence, and states that the assembly is
  unmodified and that a consumer may replace `EPPlus.dll` with their own build of the same
  version.
- The LGPL-3.0 text ships beside the binary as `licenses/LGPL-3.0.txt`. Copy it out of the
  package; do not retype it.
- The exact-version bracket `[4.5.3.3]` in
  `griffel-femex.Adapters.Saf/griffel-femex.Adapters.Saf.csproj` stays, and `NOTICE` says
  why: a transitive bump would put a commercially sold product in breach silently. The
  bracket is a licence control, and a control whose reason lives only in a code comment is
  one nobody outside the repository can see.

### Packaging metadata

So that `dotnet pack` can produce something publishable and the distribution-reach argument
for choosing SAF first is actually available rather than notional: `PackageId`, `Authors`,
`Description`, `RepositoryUrl`, `PackageLicenseExpression` and `PackageReadmeFile` on
`griffel-femex.csproj`, `griffel-femex.Adapters.Saf.csproj` and
`griffel-femex.Reporting.csproj`. Nothing is published in this pass — this makes publishing
possible, not done.

### Files

Three new `LICENSE`, three new `NOTICE`, one new `README.md`, one `licenses/LGPL-3.0.txt`
carried into the CLI's output, and four `.csproj` edits.

---

## Item 2 — line loads on plates, and the 11 manufactured errors

### What is actually wrong

`femex convert SAF_example_HOUSE_metric_ZYX_220.xlsx` produces a model in which load 39 is:

```json
{ "type": "linear", "startNode": 70, "endNode": 72,
  "startPosition": 0, "endPosition": 1,
  "coordinateSystem": "Local", "direction": "Z",
  "magnitudeStart": -3000, "label": "LFS1" }
```

No `barId`. `Validate()` rightly returns three errors for it, and eleven across the file.
Two independent causes, both in
`griffel-femex.Adapters.Saf/SafImporter.Loads.cs`:

- **`:405–416`** — for an edge-hosted curve action `bar` is null, so `length = 0.0`, and the
  positions are computed from it anyway. `0` and `1` is the full extent: no information, and
  invalid.
- **`:344`** — `load.CoordinateSystem` is assigned unconditionally, so `Local` lands on a
  load whose host FEMEX cannot name.

Nothing in the transfer report mentions either. **The gap that allowed it is the more
important half**: Phase B's round-trip test asserts *equivalence modulo declared losses* and
never asserts that the imported model is **valid**. An adapter can emit an internally
inconsistent model and still round-trip perfectly.

### Why not mint nodes

The instinct is reasonable — SAF places these loads freely and FEMEX addresses them by node,
so minting the two ends closes the gap in one move. The repository has already answered it
three times, against itself, in writing:

- `Loads/PointLoad.cs`'s own doc-comment: *"minting a node and splitting the member changes
  topology, element count and member identity, which breaks the round-trip equivalence
  definition outright"*. That is decision **P2** of `SAF_Adapter.md`, taken against the
  reference workbook rather than in the abstract.
- The adapter already **refuses** to mint for `StructuralSurfaceActionFree` and
  `StructuralPointActionFree`, with the reason in the shipped message: *"importing one would
  consume model nodes that the source did not have."* Minting for line loads and not for the
  other two would make the three free-load families inconsistent with each other, and the
  inconsistency would be invisible to anyone reading only one of them.
- A minted node is not free in FEMEX specifically. `Node.LevelNumber` is a required foreign
  key enforced as an **error** (`FemexModel.Validation.cs:419`), so every minted node forces
  a level decision too; and `GetCoincidenceTolerance` clustering means one minted near an
  existing node silently merges, moving the load. Both are the failure class the product
  exists to catch, manufactured by the product.
- It breaks §7.2 equivalence, so **A2's diff — Claim 2 of the business model — would report
  phantom differences between two exports of the same model.**

### What to do instead — schema 1.11, `LinearLoad.PlateId`

The format already contains this exact shape, on `Support`:

```csharp
["Support.BarId"]    = new Reference(RefTarget.Bar),
["Support.PlateId"]  = new Reference(RefTarget.Plate),
["Support.RegionId"] = new Reference(RefTarget.Region, scope: "PlateId"),
```
`Comparison/MemberComparer.cs:110–112`

`LinearLoad` gains the same two fields beside its existing `BarId`, so the load names its
host the way a support already does. The edge itself is named by `StartNode`/`EndNode` —
exactly as `Hinge.EdgeStartNodeId`/`EdgeEndNodeId` names a hinged edge — and **their order
is not cosmetic**, for precisely the reason `Hinge`'s doc-comment gives about its own pair:
it is what local x runs along, and writing the same edge the other way round reverses x and
y.

A local direction then resolves through machinery that already exists and is already the
stated convention: **`TryGetEdgeLocalAxes`** (`FemexModel.LocalAxes.cs:126`), which
`BoundaryConditions/Hinge.cs` documents at length — *local x along the edge, local z the
panel's normal, local y = ẑ × x̂* — and which is the same frame SAF's own
`RelConnectsSurfaceEdge` uses. **No new geometry is written anywhere in this item.**

`TryGetHostAxes` (private, in `FemexModel.LocalAxes.cs`, immediately below
`TryGetLoadDirection`) gains one case beside the three it has:

```csharp
case LinearLoad line when line.PlateId.HasValue:
    return TryGetEdgeLocalAxes(line.PlateId.Value, line.StartNode, line.EndNode,
                               out x, out y, out z);
```

A second, independent payoff: `SafExporter.Loads.cs:704–715` currently **searches every
plate** for a contour containing the load's two nodes and takes the first match. Two plates
sharing an edge have opposite normals, so today the exported local direction is decided by
list order. With `PlateId` the exporter names the plate the load says it is on, and the
guess disappears.

### Scope, stated plainly

**In, and closing:** partial-extent and locally-directed line loads on a **plate contour
edge** — `ExcelCurveForceAction.OnEdge` — which is the whole of the 11 errors.

**Out, and staying declared losses, unchanged:** `StructuralCurveActionFree`, a line load
along a free polyline (1 in the house file); loads on `StructuralCurveEdge` internal edges
(3); and the other free load families. Closing those wants **coordinate-addressed loads** —
a load bounded by absolute points rather than by nodes, which is what SAF itself does, and
which would close four Unmapped families at once without inventing topology. That is a
format expansion and it deserves its own document and its own bump, the way 1.9 and 1.10
were held and designed before they landed. Recorded under *Still open*; not started here.

### Changes

**Library — schema 1.11, additive only, no migration.**

- `Loads/LinearLoad.cs` — `int? PlateId`, `int? RegionId`, with a doc-comment stating: at
  most one host; the edge is named by the two nodes and their order decides local x; the
  positions are measured along the bar when there is a bar and along the edge segment when
  there is a plate. Point at `Hinge` for the frame rather than restating it — a convention
  stated twice is a convention that can disagree with itself.
- `FemexModel.cs:98` — `CurrentSchemaVersion` → `"1.11"`, `"1.10"` appended to
  `ReadableSchemaVersions`, and the ledger clause in the doc-comment.
- `FemexModel.Validation.cs` — `"1.10"` appended to `SelfWeightVersions`. Reword the two
  rules that fire today (*"has a local direction but no barId"*, *"states a startPosition but
  names no bar"*) so they name either host. New rules, mirroring the hinge's: both hosts
  stated at once; a `plateId` naming an unknown element or a non-plate; a `regionId` not
  within that plate; the two nodes **not adjacent** in the named contour — reuse
  `AreAdjacent(contour, a, b)` at `:2124`, which the hinge rule at `:2119` already calls.
- `FemexModel.LocalAxes.cs` — the one `TryGetHostAxes` case above.
- `Comparison/MemberComparer.cs` — two entries in the reference table, copying
  `Support.PlateId` and `Support.RegionId` including the `scope:` argument.

**Adapter.**

- `SafImporter.Loads.cs`, `Placed()` — set `PlateId` and `RegionId` on the edge-hosted
  branch; measure `from`/`to` along the edge segment instead of against `length = 0`; and
  assign `CoordinateSystem` unconditionally, which is now correct because both hosts can
  carry it.
- `SafExporter.Loads.cs:700–726` — use `load.PlateId` when it is stated. Keep the plate
  search only as the fallback for a 1.10-or-earlier file, and declare an *Approximated* loss
  on the occasions it still has to guess.

**Conformance — the rule that would have caught this.**

`Interop/Conformance/` gains a Tier-1 check: **an imported model carries no Error-severity
`Validate()` finding that no `TransferMessage` names.** This belongs beside the six existing
Tier-1 tests rather than in the SAF suite, because it is a property of every adapter, and
because §7.3's whole design is that a later adapter inherits the rules and cannot skip one
by not writing its test. Expect it to fail against the lossy reference adapter until that
adapter is taught to declare — which is the harness doing its job, and is the same shape as
the seven deliberately broken adapters Phase A used to prove it worked.

**Docs.** `Claude/FEMEX_EdgeHostedLoads_Summary.md` beside this plan, per the repository's
pair convention. `Examples/Example3.femex` gains a line load on a plate edge with a local
direction and a partial extent, so the new rules have a fixture that is not SAF-derived and
does not move when the adapter does.

**Viewer.** `parseFemex` reads the two new members; `PROPS` shows the host; the new
validation rules are mirrored or declared in `parity-subset.json` with a reason;
`CURRENT_SCHEMA_VERSION` → `'1.11'`. Follow *Adding a schema block* in `FEMEXViewer.md`,
which lists the six places a block lands and the order they land in.

---

## Item 4 — the generic-section finding

Do this before item 3, because it changes every parity artefact.

Fourteen of the house file's errors say *"nothing can be built from it"* about ordinary
sections. The finding is not wrong. What makes it read badly is that the **Transfer section
already explains every one of them** — *"The shape is outside FEMEX's eight parametric
discriminators. It arrives as a generic section carrying whatever stiffness the workbook
stated, and no shape"* (Oval, TTee, DoubleRectangle, TripleRectangle, ISectionWithHaunch, an
unequal-flange I), and *"A General cross-section is defined by a CompositeShapeDef polygon…
the stated stiffness survives on a generic section; the geometry does not."* The Check
section restates the same fact as a verdict, with none of the explanation, and it is the
first thing on the page.

**Reword; severity unchanged.** Error stays, so the exit codes, the `ValidationCategory`
split, and `ValidateSections`' own recorded argument — *"Both are errors because in each case
there is nothing for a receiver to fall back on"* — all stand untouched. What changes is that
the sentence says what happened and where the number probably still is, instead of
pronouncing the engineer's model unbuildable.

```
before:  Section 26 is generic and states no area, so it has no geometry
         and no stiffness; nothing can be built from it.

after:   Section 26 states neither dimensions nor stiffness, so nothing
         here can build it. If it came from a program that holds this
         profile in its own library, the properties exist there and did
         not cross.
```

**Files:** `FemexModel.Validation.cs:787` for the string, `femex-viewer.html:1749` for the
mirror — the same edit, and the wording must match character for character or item 3's own
harness goes red on it — and every `Examples/*.expected.json` containing it, regenerated by
the suite.

Leave the companion warning at `:864` alone — *"Section 26 names profile "IPE180" with no
source; the same designation names different profiles in different libraries"*. It says a
different thing, it is right, and it is the finding a reader should be left with.

---

## Item 3 — the em-dash mirror, and a parity corpus worth trusting

Do this last: items 2 and 4 both change what the fixtures contain.

### The drift

`femex-viewer.html:2717` writes ASCII hyphens where `FemexModel.Validation.cs:251–252`
writes em dashes, in `ValidateParentUids`' unresolved-parent warning. A one-character fix on
the JS side; the C# text is authoritative and does not move.

The viewer contains **6** em dashes across 7 649 lines against **54** in
`FemexModel.Validation.cs` alone, so this is more likely a class than an instance. The
widened corpus is what will say.

### Why the harness was green

The corpus is five examples producing **eight** messages, and four of the five validate
clean. Pointing the same harness at the eleven SAF-derived models the repository already
ships as `griffel-femex.Adapters.Saf.Tests/Corpus/` fails on **3 of 11** and finds exactly
this. That run has been done; it is the evidence for this item, and it is the answer to
*"how do you test the model checks in the viewer"*.

The rule in *The validation parity rule* is sound. Its guarantee is simply bounded by the
corpus: `parity-subset.json` declares one unmirrored family, and every other family the
corpus never triggers is unmirrored-and-undeclared without anything going red.

### The change

Add two checked-in examples, converted from the corpus **after items 2 and 4 have landed**:

- **`Examples/Saf-House.femex`** — from `SAF_example_HOUSE_metric_ZYX_220.xlsx`, ~110 KB,
  the broadest single file in the corpus. It reaches `ValidateParentUids`,
  `ValidateSections`, `ValidateSectionCompleteness`, `ValidateLoadGroups` and
  `ValidateLoadGroupUsage`, none of which any current example reaches. It raises the corpus
  from eight mirrored messages to roughly twenty-five once item 2 has removed the eleven.
- **`Examples/Saf-SteelHall.femex`** — from `SAF_example_STEEL_HALL_metrix_ZYX_210.xlsx`.
  The clean counterpart: eight findings, a wholly different structure, and a second opinion
  on the same rules.

Two files rather than eleven because the eight steel halls are near-duplicates of each
other; a ninth copy of the same eight messages buys a minute of headless Edge and nothing
else.

`ValidationParityTests.Examples()` already enumerates `Examples/*.femex`, so both
`.expected.json` artefacts are written and policed with no test change. Each needs a
`<None Include>` line in `griffel-femex.Tests/griffel-femex.Tests.csproj` beside the five at
`:34–:42` — the glob is deliberately absent, and a missing line fails loudly with
`FileNotFoundException`, which is the intended behaviour.

`parity-check.ps1` needs no edit at all.

**Expect the first run to go red on more than the em dash.** Every message that comes up
either gets mirrored or gets an entry in `parity-subset.json` with a reason — rule 3 of the
stated parity rule, and that file's own note that adding an entry should be rarer than
adding a mirror.

Record in `FEMEXViewer.md` that the corpus now includes SAF-derived models, and why: a
harness whose corpus contains only files the author wrote by hand tests the checks the
author remembered.

---

## Verification

Run in this order; each step is the previous one's evidence.

**Item 1**

```bash
dotnet pack griffel-femex.csproj -o ./artifacts        # the metadata resolves
```

- `LICENSE` and `NOTICE` present in all three repositories; `NOTICE` names EPPlus 4.5.3.3 /
  LGPL-3.0 and states the relinking freedom.
- `licenses/LGPL-3.0.txt` lands beside `EPPlus.dll` in the CLI's publish output.
- `dotnet list griffel-femex.Cli package --include-transitive` reconciles against the
  `NOTICE` table with nothing unlisted.

**Item 2**

```bash
dotnet build && dotnet test                            # both legs; 616 + the new tests
femex convert griffel-femex.Adapters.Saf.Tests/Corpus/SAF_example_HOUSE_metric_ZYX_220.xlsx --format text
```

- The eleven `Linear load 'LFS…'` errors are **gone**, and the remaining error count is the
  fourteen section findings and nothing else.
- The eleven-workbook round trip still asserts that every difference is named by a
  `TransferMessage`. The schema change must not cost §7.2 equivalence, and this is where it
  would show.
- The new Tier-1 conformance check passes against `SafImporter` and **fails** against an
  adapter deliberately taught to emit an undeclared invalid load. A check that has never
  failed proves nothing, which is the argument Phase A already made with seven broken
  adapters.
- A 1.10 file with no `plateId` still reads, validates and round-trips byte-identically:
  `Assert.Equal(File.ReadAllText(path), FemexModel.Load(path).ToJson())`.

**Item 4**

- `dotnet test` fails once, rewriting the artefacts, and the diff is reviewed by hand. That
  is `ValidationParityTests`' designed behaviour — it fails when it rewrites, because a test
  that silently regenerated its own baseline would assert nothing.
- `femex check` on the house model: the first screen is about the structure.

**Item 3**

```powershell
cd ..\griffel-femex-viewer ; .\parity-check.ps1        # 7 of 7
```

- Green with `Saf-House` and `Saf-SteelHall` in the corpus.
- **Then break it on purpose**: change one character of one mirrored JS message and confirm
  the run goes red on that message. `SAF_Adapter.md`'s A8 verification asks for exactly this,
  and a drift detector that has never detected drift is a decoration.
- Offline regression, unchanged from Phase D: `femex-viewer.html` opened from `file://` under
  headless Edge with `--dump-dom` makes **zero** `fetch` calls and has zero adapter buttons
  in the DOM.

**Finally**

- Commit and push all three repositories. Phase D's viewer work (`femex-viewer.html`,
  `FEMEXViewer.md`) and this repository's Phase D plan block are currently uncommitted and
  exist only on this machine.

---

## Explicitly not in this pass

- **`femex serve`.** The viewer's `Open SAF…` and `Save As SAF` still have nothing to talk
  to and were verified against a stub only. The wire contract is written out in full in
  `FEMEXViewer.md`, so a localhost implementation is small — and note the Phase E gate is
  about a *hosted* service, not a loopback one. But it is not what stands between here and a
  first demo. Lead with `femex convert`, and say the viewer's SAF buttons are inactive.
- **Coordinate-addressed free loads**, which would close four Unmapped SAF families. Its own
  document, its own bump.
- **Adapter #2.** Unchanged by any of this, and still where the revenue is.
- **`FEMEX_BusinessModel.md` §8 question 4** — *is the judgement half of `Validate()` what
  engineers actually want checked?* Not a code change, and the actual reason to go to this
  group at all. Every phase since C rests on the assumption, it costs nothing to ask, and it
  has still never been asked.
