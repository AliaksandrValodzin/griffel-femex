# The SAF adapter — Phase B, as built

Implementation record for **Phase B** of `Claude/AdaptersPlans/SAF_Adapter.md`: B1's package
references and the licence question, B2's gateway seam, B3's two legs, B4's identity, and B5's
mapping document.

Written 28 August 2026. The mapping itself is `Claude/FEMEX_SAF_Mapping.md`; this is what landed,
what it cost, and what it found.

---

## 0. What landed, in one screen

| | |
|---|---|
| **New projects** | `griffel-femex.Adapters.Saf` (`netstandard2.0;net8.0`), `griffel-femex.Adapters.Saf.Tests` (`net8.0`) |
| **Packages** | `StructuralAnalysisFormat` 1.7.3, its SimpleInjector5 bootstrapper, **`EPPlus [4.5.3.3]` pinned exactly** |
| **Public surface** | `SafImporter`, `SafExporter`, `ISafGateway`/`SafGateway`, `SafLoss`, `SafMessages`, `SafMessageLog`, `SafObjects`, `SafUnits` |
| **Declared losses** | **81**, one enum member each, with category, leg, anchoring rule and text |
| **Tests** | **477 → 542**; 65 new, all green, including **all seven Tier-1 conformance checks against the real adapter** |
| **Corpus** | eleven published workbooks vendored; all eleven import, export and round-trip |
| **Round trip** | the steel-hall frame: **0 differences**. The house: **4**, every one named by a message |
| **Build** | both legs, **0 warnings, 0 errors** |

Three things the plan predicted and four it did not. The predicted: the SDK needed a container
bootstrap, EPPlus stayed at 4.5.3.3, and levels are synthesised on every file. The unpredicted are
§4, §5, §6 and §7 below — and one of them changes what any SAF adapter can claim about provenance.

---

## 1. B1 — the packages, and the licence question closed

```xml
<PackageReference Include="StructuralAnalysisFormat" Version="1.7.3" />
<PackageReference Include="StructuralAnalysisFormat.Bootstrappers.SimpleInjector5" Version="1.7.3" />
<PackageReference Include="EPPlus" Version="[4.5.3.3]" />   <!-- exact, not floating -->
```

**The bracket is a licence control, not decoration.** The SDK declares `EPPlus >= 4.5.3.3`, and
4.5.3.3 is the last LGPL release; version 5 onward is Polyform Noncommercial and needs a paid
commercial licence. A transitive bump would put a commercially-sold product in breach silently.

**The plan's *Still open* entry is closed as the corpus notes proposed and no further.** LGPL 4.5.3.3
consumed as an unmodified NuGet-delivered assembly, never referenced in source by this project, is
the ordinary arrangement for Apache-2.0 code: the relinking freedom survives, because a consumer can
substitute their own build of the same assembly version. What FEMEX inherits is 4.5.3.3's own
constraints — an unmaintained 2019 assembly, and a binding conflict if any host process ever loads
EPPlus 5+. That is a note for the add-in question, not a blocker.

**The ClosedXML/NPOI alternative was not taken and is now cheaper to refuse than to cost.** It would
mean bypassing the SDK's Excel layer entirely and reading the workbook against the SAF specification
— which, given §4 below, means reimplementing the part of the SDK that reads 1.0.0 through 2.3.0 with
per-property version ranges. The dependency is licensed compatibly; the work is not warranted.

**One consequence for the `netstandard2.0` leg.** The SDK is `netstandard2.0`-only, so it loads on
both legs and on `net48`, and decision 5's reach argument is unobstructed. The adapter builds for
both.

---

## 2. B2 — the gateway seam, and the one thing it turned out to be for

`ISafGateway` is `Read(Stream) → SafReadResult` and `Write(Stream, ExcelModel) → SafWriteResult`. The
plan gave three reasons for it: the SimpleInjector bootstrap should not leak into mapping code, it is
the same shape record/replay will want, and it is where a permissively-licensed Excel reader would
go.

All three hold. A fourth arrived unplanned and is the one that earned it: **the seam is where the
provenance repair of §4 lives.** Nothing above `ISafGateway` knows that the workbook is read twice,
and nothing below it knows why.

Implementation notes worth keeping:

- The bootstrapper is built **once per gateway** and verified; a scope is per call. Building it per
  call wires the SDK's whole object configuration each time.
- The SDK reports through `IEventService.Subscribe<LogEvent>`, so the gateway subscribes for the
  duration of a call and unsubscribes after — a subscription that outlives the call attributes the
  next transfer's commentary to this one.
- `SafWriteResult.ValidationErrors` carries the SDK's own verdict on the workbook it was asked to
  write. That is what §7 is about.

---

## 3. B3, B4 — the two legs

Roughly 6 900 lines across fifteen files, of which the two importers and two exporters are partial
classes split by subject rather than by size. `SafObjects` groups the flat bag once; `SafUnits`
converts; `SafEnums` translates; `SafSectionShapes` handles the one place the evidence runs out;
`SafPosition` handles P2's four spellings.

**`SafLoss` and `SafMessages` are §8.2 as data.** 81 entries — 18 *Approximated* import, 3 export;
17 *Dropped* import, 14 export; 6 *Invented* import, 11 export; 12 *Unmapped*. `SafMessageLog`
enforces the per-concept versus per-object rule from the catalogue rather than from the call site, so
asking for a per-object message about a per-concept loss throws rather than quietly producing
forty-two identical lines about `Layer`.

**Identity (B4)** is SAF name ⇄ `NativeHandle` and `ExportReceipt`, with `AssignMissingUids` and
`NameSynthesis` doing what §5.4 and §5.5 specify. Four sheets — bar, node, support, hinge — have no
FEMEX name at all, so a round trip renames most of the model; that is `SynthesisedNames`, said once.

Everything else about the mapping is in `FEMEX_SAF_Mapping.md`, deliberately.

---

## 4. What this phase found: the SDK invents uids on **read**

`FEMEX_SAF_Corpus_Notes.md` §5 established that the SDK invents 42 GUIDs on *write* for the reference
workbook's blank `Id` cells, and drew the right conclusion for the write leg. **It does the same on
read**, which is worse, because nothing about the object model shows it:

- `ExcelObjectBase.Id` is a non-nullable `Guid`, and after a read it is never `Guid.Empty`.
- So a uid the file stated and a uid the SDK made up are indistinguishable.
- And two reads of the same file give **different** uids for those rows.

The second point is the serious one. It means a naive SAF adapter — including the one this phase
first wrote — produces a different model every time it reads the same workbook, so **§7.2 equivalence
cannot be asserted at all**, the diff cannot match anything, and every uid in the report is a false
provenance claim. It is `FEMEX_Adapters.md` §6.2's "the same native model read in a different order
yields a different node table", arriving through a door nobody was watching.

**What was done.** `SafGateway.Read` reads the same bytes twice and compares each row's `Id`,
addressed by sheet and row number. An Id that moved was invented. The importer replaces those with a
uid **derived from the row address**, so the read is a function of the file, and reports `MintedUids`
with the count — 42 for the reference workbook, exactly the number the corpus notes predicted for the
write leg.

**What it costs.** One extra parse per import, roughly doubling the read. That is the price of being
able to say which uids are provenance, and it is in *Still open*.

Two smaller cases of the same principle were settled the same way, and between them they took the
house round trip from **101 differences to 4**:

- The pieces a SAF row splits into — a chorded chain of bars, the two ends of a `Position = Both`
  hinge, an expanded repeat series — get **derived** uids, not minted ones.
- A synthesised level's uid is derived from its elevation and a surface property's from its
  thickness, because in each case that is all the object is.

---

## 5. What this phase found: the SDK's validator is the oracle, and it works

The plan's *Verification* proposed feeding an exported workbook to SAF's published web viewer as an
independent oracle — the one check a self-round-trip cannot make — and noted that P5's invention
policy gates that step.

**A cheaper oracle was already in the package.** `IExcelExportService.Export` validates and refuses,
and `SafWriteResult.ValidationErrors` carries the verdict. It found **five mandatory columns P5's
table does not list**:

| What the validator refused | What was written instead |
|---|---|
| `LCS Adjustment` type and vector, on every member and every surface | SAF's own default for the form, with FEMEX's angle on top |
| A `Profile` on any non-parametric cross-section | the section's own name |
| A `Material` on every cross-section — FEMEX puts it on the member | the member's material, or the model's first |
| Pasternak `C2x`/`C2y` on a subsoil connection | zero |
| A stiffness column wherever the type is `Flexible` | the stated stiffness, in the sheet's own units |

And two values it refused outright: **`Flexible compression only` on the line and edge support
sheets**, which accept five of SAF's eight constraint types; and **`Direction = Vector` beside a
value**, whose alternative vector columns throw in the SDK's own writer. Both are now reductions with
messages — `NarrowedLineRestraint` and `FlattenedLoadDirection` — rather than a workbook that will not
open.

Each of those seven would have shipped as a silent wrong answer or a refused file. The published web
viewer remains the check against the *specification* rather than against the SDK, and is still worth
running; it is no longer the only oracle available.

---

## 6. What this phase found: the conformance harness catches real adapters too

Phase A proved the harness could tell a compliant reference adapter from seven deliberately broken
ones. Pointed at an adapter that talks to something real, it failed **three** checks on the first
run, and every one was a genuine defect:

- **Two-phase synthesis.** The same workbook presented with each sheet's rows reversed produced a
  different level table. `GeometrySynthesis` clusters canonically, but the order candidates are
  *declared* in is the order they are created and therefore numbered in. Fixed by declaring storeys
  sorted by elevation and points sorted by coordinate — the adapter's half of §6.2, which the helper
  cannot do for it.
- **Loss coverage, units.** A model exported in kilonewtons comes back in newtons, and the only
  message about it was anchored to the model, leaving every rescaled load looking like an
  unexplained change of value. `RestatedInSiUnits` now says it once per entity kind.
- **Loss coverage, two undeclared columns.** `LoadCombination.IncludeInDesignEnvelope` has no SAF
  column at all, and a load case attached to a group the exporter invented is itself an invention.

The harness's transport needed one piece of thought, as §7.5 said it would: reversing
`ExcelModel.Objects` wholesale puts referrers before the rows they name and the SDK's writer resolves
that differently, so the reordering is **within each sheet**, where rows are unordered by
construction.

All seven checks now pass.

---

## 7. Verification performed

- **Build:** `dotnet build` at the repo root — both legs of the library, both legs of the adapter,
  **0 warnings, 0 errors**. The adapter's `netstandard2.0` leg builds against the SDK, which is
  itself `netstandard2.0`-only.
- **Tests: 477 → 542, all green.** 65 new in `griffel-femex.Adapters.Saf.Tests`; `dotnet test` at the
  repo root finds both projects.
- **The corpus, all eleven workbooks:** each imports, each exports to a workbook the SDK's own
  validator accepts, each round-trips with every difference named by a message, and **each read twice
  gives the identical model** — which is the assertion §4 exists to make true.
- **The steel-hall frame round-trips with zero differences** — 47 members, 45 nodes, ten supports,
  sixteen hinges — which is what shows the four differences on the house are the house's.
- **All seven Tier-1 conformance checks pass** against the real adapter, over `Conformance1.femex`,
  through a SAF transport that writes and re-reads an actual workbook.
- **Message coverage:** the reference workbook is asserted to emit 38 named import-leg losses, and
  the export leg to declare all seven mandatory-column inventions it makes. The seven synthesised
  levels are asserted by shape.
- **Failure paths:** a stream of text, an empty stream and a request type the adapter cannot serve
  each **return** rather than throw. A model with three levels and nothing else exports. A model
  stating no units, and one mixing metric with imperial, each produce an Error-severity message.
- **Name stability:** the same model exported twice produces an identical uid → name map.

---

## 8. Files

**New:** `griffel-femex.Adapters.Saf/{ISafGateway,SafGateway,SafReadResult,SafLogEntry,SafObjects,SafUnits,SafEnums,SafIdentity,SafNamer,SafPosition,SafSectionShapes,SafLoss,SafMessages,SafMessageLog,SafImporter,SafImporter.Elements,SafImporter.Restraints,SafImporter.Loads,SafExporter,SafExporter.Loads,SafExportContext}.cs`
· `griffel-femex.Adapters.Saf.Tests/{SafCorpus,SafRoundTripTests,SafMessageCoverageTests,SafAdapterTests,SafConformanceTests}.cs`
· `griffel-femex.Adapters.Saf.Tests/Corpus/*.xlsx` (11 files, Apache-2.0)
· `Claude/FEMEX_SAF_Mapping.md`.

**Modified:** `griffel-femex.csproj` (three `<Compile Remove>` lines, so the adapter's sources are not
compiled into the library and the SDK, EPPlus and SimpleInjector do not travel with it);
`griffel-femex.sln` (both new projects).

**Not modified:** anything else. The library, the viewer and the existing suite are untouched.

---

## 9. What this makes stale

- **`SAF_Adapter.md` B1's EPPlus paragraph**, which says the licence answer is stated but not
  confirmed. It is confirmed, and the ClosedXML/NPOI alternative is refused with a reason rather than
  left uncosted.
- **`SAF_Adapter.md` P5's table.** Five more mandatory columns, listed in §5 above.
- **`SAF_Adapter.md`'s *Verification*, the independent-oracle line.** The SDK's own export validator
  is a second oracle, it runs in the test suite, and it found seven things.
- **`SAF_Adapter.md`'s *Verification*, the test count.** The suite is 542, not 390 or 461.
- **`FEMEX_SAF_Corpus_Notes.md` §5's framing of the invented Ids** as a write-leg fact. It is a read
  fact as well, and that is the more serious half — §4.
- **`FEMEX_SAF_Corpus_Notes.md`'s *Still open*, the SDK-log question.** Answered as far as the
  contract allows: only SDK errors cross, because `TransferMessage` has no warning that is not a
  loss. The remaining half is a contract gap, recorded below.

---

## 10. Still open

- **`TransferMessage` has no warning without a loss category**, so the SDK's own warnings have no
  home in the transfer report. Widening the contract is a §3.5 decision, not an adapter one.
- **`Restraint.Stiffness` has no declared unit**, and SAF states three different quantities behind
  it — per support, per unit length, per unit area. The adapter picks correctly per sheet and nothing
  in the format says it was right to.
- **The double read** costs one extra parse per import. Reading the `Id` columns out of the OOXML
  directly would remove it, at the cost of a second Excel reader.
- **The parameter order for five parametric shapes** is inferred from the I-section's rather than
  measured, and every affected section says so.
- **A non-Z-vertical workbook is refused** rather than rotated.
- **A generic section with no stated stiffness fails `Validate()`.** Correctly — but ten of the
  reference workbook's 29 sections are in that state, so a check report on a SAF import leads with
  ten errors that are about SAF's shape library rather than about the structure.
- **`FemexEntity` still has no `PlateRegion`**, so a region's messages anchor to its plate. Carried
  over from Phase A, and this phase produced several such messages.
- **Adapter #2.** SAF does not reach ETABS, Robot or RCB, so Claim 1 still has no input path to that
  network. Unchanged by anything here.
