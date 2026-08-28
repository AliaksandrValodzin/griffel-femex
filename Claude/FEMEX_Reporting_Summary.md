# The report and the CLI — Phase C, as built

Implementation record for **Phase C** of `Claude/AdaptersPlans/SAF_Adapter.md`: C1's three
sections, C2's self-contained HTML, C3's provenance, C4's batch mode and exit codes, C5's three
verbs, and C6's wording discipline.

Written 28 August 2026. This is the phase that produces the thing that is actually sold.

---

## 0. What landed, in one screen

| | |
|---|---|
| **New projects** | `griffel-femex.Reporting` (`netstandard2.0;net8.0`), `griffel-femex.Cli` (`net8.0`, binary `femex`), and a test project beside each |
| **Packages** | **none** — the reporting layer references the library and nothing else; the CLI adds only the SAF adapter |
| **Public surface** | `AssuranceReport`, `ReportProvenance`, `SourceFile`, `CheckSection`, `CompareSection`, `TransferSection`/`TransferLeg`, `HtmlReport`, `JsonReport`, `TextReport`, `ReportIndex`, `ReportTool`; `Cli.Run`, `ModelReader`, `ExitCode` |
| **Library change** | `ValidationCategory` — one enum, one property on `ValidationMessage`, and a category stated at each of the 35 call sites in `Validate()` |
| **Adapter change** | one defect found and fixed: `SafLoss.UnplaceableSurfaceSupport` (§4) |
| **Tests** | **542 → 616**; 74 new, all green |
| **Build** | every leg of every project, **0 warnings, 0 errors** |
| **Verified in a browser** | a real report rendered from `file://` in headless Chrome with DNS blackholed |

Two things the plan predicted and three it did not. The predicted: the report is one HTML file with
nothing to fetch, and the CLI is what a migration engagement runs. The unpredicted are §2, §4 and §5
below.

---

## 1. C1 — one report, three sections

`AssuranceReport` is the document **as data**; `HtmlReport`, `JsonReport` and `TextReport` are three
views of it. That is the whole shape, and it is the answer to the obvious alternative — a writer that
walks a model and builds strings as it goes — which would make `--format json` a second
implementation of the same document, free to disagree with the HTML about the same model. The summary
block C1 sketches is `AssuranceReport.Summary()`, so the terminal line, the HTML table and the JSON
all count from one answer.

```
Model Assurance Report · SAF_example_HOUSE_metric_ZYX_220.xlsx · 2026-08-28 · femex 0.1.0 · sha256 5b0f8271

  Check       Validate()                          36 findings — 25 error · 11 warning
  Transfer    SAF → FEMEX                         78 losses
```

Each section is present only when it was run, and each is the engine's own output rather than a
restatement of it: **Check** carries `ValidationMessage` itself, **Compare** carries
`ModelDifference`, **Transfer** carries `TransferMessage`. Nothing is reworded on the way through.

**Transfer keeps decision 4's two sections.** `TransferSection` holds an `Import` leg and an `Export`
leg, each with its own adapter, its own counts and its own success — because a difference appearing
only on export is an exporter bug, and a single flat list of losses would throw away the one fact
that says which half to go and look at.

---

## 2. What this phase found: the report needed a distinction the engine did not make

C1 says the report *"should let a reader see the judgement findings without the referential ones
burying them"*, and `FEMEX_BusinessModel.md` §4 is where that split comes from — half the validator is
referential integrity and is table stakes, half is engineering judgement and is the product.

**The engine did not carry that distinction, and the reporting layer is the wrong place to invent
it.** A classifier living beside the report would be a second statement about which rule is which,
maintained apart from the rules themselves and free to disagree with them — the
mapping-table-in-two-places failure, landed on the flagship claim. Decision 8 settles the direction:
the C# engine is authoritative.

So the library gained `ValidationCategory`, and `Validate()` states one per validator family:

| | |
|---|---|
| **Referential** | ids, uids, parent uids, grids, nodes, sections, materials, bars, plates, load groups, loads, combinations, boundary conditions, mesh, unresolved parents |
| **Judgement** | gravity, grid geometry, coincident nodes, contour planarity, region priorities, projected loads, self-weight, combination usage, load-group usage, load distributions, thermal gradients, and the five completeness families |
| **Provenance** | schema version, migrations, unknown members, uid coverage, name keys |

Three points worth keeping:

- **The third category is a deviation from §4's "two halves", and it is deliberate.** Neither half
  describes *"the model declares schemaVersion 1.8"*, *"this model was written before loads had ids
  and has been migrated"* or *"3 of 11 authored objects carry a uid"*. Those are statements about the
  **file**, not about a structure, and C3 already promotes exactly that material to a section of its
  own. Filing them under either half would have made one of the two halves mean less.
- **The category is orthogonal to severity, and that is the point.** Two regions with equal priority
  and overlapping extents is an *Error* and a *Judgement* finding at once. A report that could only
  sort by severity would file the check §4 quotes as the product beside a dangling section reference.
- **There is no default.** `ValidationMessage.Error(text)` no longer compiles; the category is
  required. §4's consequence for the roadmap is that new rules are added to the judgement half
  *deliberately*, and an omitted argument is not a way to decide that.

The messages themselves are untouched, so `Examples/*.expected.json` and the viewer's JavaScript
mirror are unaffected — A8's parity harness stayed green without an edit.

---

## 3. C2, C3 — the document

**One self-contained HTML file, and the suite proves it fetches nothing.** No stylesheet link, no
script tag, no font request, no image, no CDN — and the test is a list of regular expressions rather
than a search for `http`, because a protocol-relative `//cdn…` and a bare `src="style.css"` beside
the file break the same promise without containing that string.

**No JavaScript at all**, which is stronger than the viewer needs to be and is right here: the viewer
is an application and this is a document. A document that needs a script to show its own contents can
be broken by a browser setting, a mail client's sanitiser or a PDF print. Every finding is in the
markup, expanded, in reading order.

**Provenance is a section.** Both files of a conversion are hashed — the model in and the workbook
out — and the section carries the tool version, the FEMEX schema, the generation time, each adapter's
identity and the schema it was built against, and, per source, its sha256, size, declared schema and
its own `FileMetadata` producer stamp.

`ReportProvenance.GeneratedAt` is **supplied, not read from the clock**, for the reason
`FemexModel.ToJson` stamps a schema version and refuses to stamp a timestamp: a deliverable has to be
reproducible from its inputs, and a section that reached for `DateTime.Now` could not be tested for
its own content.

**And the converted model is not stamped with a producer.** Tempting, and it would have undone the
round-trip determinism the whole of Phase B was spent establishing: the same workbook converted twice
would produce two different files. The provenance of a conversion is in the report, where it can
carry a hash of both sides.

---

## 4. What this phase found: a real defect in the SAF exporter

The plan's own verification asks that `femex convert` round-trip `Examples/Example1.femex` through
SAF. It did not. The SDK's export validator refused the workbook:

```
StructuralSurfaceConnection · Support-bb39e36b: 'Member2 D' must not be empty.
```

**A FEMEX area support may name a plate or a free polygon of nodes**, and `SafExporter` wrote
`Member2D = null` for the second case — a row SAF's own validator refuses, which costs the whole
workbook rather than one support. Phase B never saw it, because **every model Phase B exported had
come from a workbook**, and an area support read out of SAF always names a surface. The first
hand-authored model through the export leg found it in one run.

Fixed the way `UnplaceableEdgeSupport` already handles the same shape of problem: the support is
reported as a `Dropped` loss and not written. New enum member, new catalogue entry, one guard clause.
This is the second time the same principle has paid — Phase B's §5 found seven mandatory columns the
same way — and it is worth stating plainly: **the SDK's export validator is an oracle, and every new
kind of model put through it finds something.**

---

## 5. What this phase found: two writers, not one

`femex check model.femex --format json` produced JSON with a line of progress commentary in front of
it, so nothing could parse it. Obvious in hindsight and not in the plan: **the report and the running
commentary are two streams.** When the report itself is going to stdout — no `--out`, and a format
something is going to read — the commentary goes to `TextWriter.Null`; otherwise it goes to stdout
beside the receipts. A batch driver whose output has to have a preamble stripped off it is a batch
driver nobody pipes twice.

---

## 6. C4, C5 — the verbs, the batch and the exit codes

```
femex check    <file...> [--out DIR] [--format html|json|text]
femex compare  <model> <baseline> [--out DIR] [--format html|json|text]
femex convert  <file...> [--to FILE] [--out DIR] [--format html|json|text]
```

- **`check`** takes a `.femex` **or a SAF `.xlsx`**. A workbook is imported first, so the report says
  both what the crossing cost and what the resulting model looks like — which is the only way to tell
  a finding about the structure from a finding about what SAF could not carry.
- **`compare`** takes exactly two files, in `diff`'s order: the model, then the baseline.
- **`convert`** goes `.xlsx → .femex` or `.femex → .xlsx`, and **always produces a report as well as
  a file**, because §4.3's warning is that from inside an adapter an invention does not feel like a
  loss — it feels like success. Conversion is not gated; decision 2 says it is the giveaway.
- **Wildcards are expanded by `femex` itself.** C4's own example is `femex check *.femex`, and the
  shell this repository is developed and used on hands the pattern through untouched.
- **`--out` holds everything a run produced** — reports, the index, and whatever `convert` converted —
  so the folder travels as a unit. The index is written when more than one model was reported on;
  one report plus an index pointing only at it is a folder with a redundant file in it.

**Exit codes: 0 clean · 1 findings · 2 the tool could not run.** The distinction is the whole reason
there are three. A model with findings is the tool working, so 1 is not a failure; 2 is reserved for
a verb that does not exist, a file that is not there, an output folder that cannot be written. **A bad
input file is never a 2** — and per decision 10, declared losses never raise the code at all, because
a tool whose exit code punished an adapter for reporting fourteen losses would teach every adapter
author to report fewer.

`compare` exits 1 when the models differ, matching `diff` and `git diff --exit-code`.

---

## 7. P4, answered for the driver

The plan has carried P4 as open since Phase 0: every enum in the library throws on an unrecognised
value, so a `.femex` from a later schema carrying one `"lengthUnit": "Furlong"` is fatal to the file —
where `IExtensible`'s whole design says an unknown member is preserved, re-emitted and named.

**The format question is still open. The driver's half is closed**, at the minimum scope the plan
fixed: `ModelReader` never surfaces a read failure as an unhandled exception, and an unreadable
`.femex` becomes an Error-severity finding in the report's Check section, in the
`ValidationCategory.Provenance` category — because that is precisely what it is, a statement about the
file, there being no structure to make one about. It exits **1**, not 2, so a batch run over forty
client models does not stop at the seventh.

The catch is deliberately broad, and the boundary is what makes it legitimate rather than lazy:
everything below it is parsing a file the user chose, and no exception arriving from that should be
shown to a user as a crash. `Cli.Run` has a second one around every verb, which is the last resort
and the only place a 2 is produced from an exception.

---

## 8. Verification performed

- **Build:** `dotnet build` at the repo root — both legs of the library, the adapter and the
  reporting layer, plus the CLI: **0 warnings, 0 errors**.
- **Tests: 542 → 616, all green.** 8 new in `griffel-femex.Tests` (the category split), 33 in
  `griffel-femex.Reporting.Tests`, 33 in `griffel-femex.Cli.Tests`; `dotnet test` at the repo root
  finds all four projects, which now all sit in the solution.
- **C's stated proof, end to end on the examples:** for **every** file in `Examples/`, `femex check
  --format json` reports exactly what `Validate()` said — message for message, in the engine's own
  order — and exits 0 where the model is clean and 1 where it is not.
- **The round trip:** `femex convert` takes the steel-hall workbook to `.femex`, back to `.xlsx` and
  back to `.femex` again through three separate invocations, and the two FEMEX models are **equivalent
  under §7.2 with zero differences**. The written `.femex` satisfies the repository's byte-identity
  assertion, `File.ReadAllText(path) == FemexModel.Load(path).ToJson()`.
- **Batch mode on the SAF corpus:** all **eleven** published workbooks checked in one run, eleven
  reports and one index, every row linking to the report beside it.
- **The report opens with the network disabled** — asserted in the suite by pattern, and confirmed by
  hand: a real report rendered from `file://` in headless Chrome with `--host-resolver-rules="MAP *
  ~NOTFOUND"`, screenshotted, and read.
- **Wording (C6, decision 9):** every rendering — HTML, JSON, text, index — and the usage text are
  asserted to contain none of *certif\**, *guarantee*, *we confirm*, *safe to use*, *fit for purpose*;
  and the document says in as many words that it is not an engineering opinion.
- **Escaping:** model text — a load case label somebody typed, a native handle out of a workbook —
  cannot reach the markup unescaped. A report whose contents depended on what a user called a load
  case would be a report that could be steered by the file it is about.
- **Failure paths:** a missing file, an unknown verb, an unknown option, an unknown format, a
  wildcard matching nothing, `--to` with several inputs, and a conversion that would write over its
  own input each exit **2** with one sentence on stderr and no stack trace. An unreadable `.femex`,
  an unreadable comparison baseline and a file that is not FEMEX at all each exit **1**.
- **Determinism:** the same report renders identically twice, in both HTML and JSON.

---

## 9. Files

**New:** `griffel-femex.Reporting/{AssuranceReport,ReportProvenance,SourceFile,CheckSection,CompareSection,TransferSection,HtmlReport,JsonReport,TextReport,ReportIndex,ReportTool}.cs`
· `griffel-femex.Cli/{Program,Cli,CommandLine,ModelReader,ReportOutput,CheckCommand,CompareCommand,ConvertCommand,ExitCode}.cs`
· `griffel-femex.Reporting.Tests/{Reports,CheckSectionTests,HtmlReportTests,JsonReportTests,ProvenanceTests,ReportIndexTests,WordingTests}.cs`
· `griffel-femex.Cli.Tests/{Run,CheckTests,BatchTests,ConvertTests,CompareAndUsageTests}.cs`
· `ValidationCategory.cs` · `griffel-femex.Tests/ValidationCategoryTests.cs`
· `Claude/FEMEX_Reporting_Summary.md`.

**Modified:** `ValidationMessage.cs` (the category, required); `FemexModel.Validation.cs`
(`Validate()` states one per family, and gained `Validate(ValidationCategory)`);
`griffel-femex.Adapters.Saf/{SafExporter.Loads,SafLoss,SafMessages}.cs` (§4's defect);
`griffel-femex.csproj` (four `<Compile Remove>` pairs, so the new projects are not compiled into the
library); `griffel-femex.sln` (all four new projects).

**Not modified:** the viewer, `FEMEXViewer.md`, the examples, the expected-output artefacts, and
every other adapter file. Phase D is where the viewer changes.

---

## 10. What this makes stale

- **`SAF_Adapter.md`'s *Verification*, the test count.** The suite is 616, not 542.
- **`SAF_Adapter.md`'s *Still open*, P4.** Half of it is answered: the driver's half, at the minimum
  scope the plan set. The format's half is untouched and still open.
- **`FEMEX_BusinessModel.md` §4's "the file splits roughly in half".** It splits three ways, and the
  third pile — what the file says about itself — is small, real, and is the material C3 makes a
  section of.
- **`FEMEX_SAF_Adapter_Summary.md` §7's claim that every model exported cleanly.** True of the corpus,
  and the corpus is all workbooks; a hand-authored model found §4's defect on the first run.

---

## 11. Still open

- **What a `.femex` read does with an unrecognised enum value**, in the *format*. The driver no
  longer crashes; the library still throws, and `IExtensible`'s asymmetry — an unknown member
  survives a round trip, an unknown enum value is fatal to the file — is unchanged.
- **`compare` reports one transfer leg at most.** Comparing two SAF workbooks imports both, and
  `TransferSection`'s Import/Export shape — decision 4, and right for a conversion — has nowhere to
  put the baseline's losses. Today `compare` attaches neither, and names the baseline in the section
  instead. The honest fix is a report that can hold a transfer per source, which is a change to
  decision 4 rather than to this layer.
- **Nothing is metered, and nothing here decides what should be.** Conversion is free by decision 2;
  whether the *report* is the metered thing is the business model's own *Still open*, and it is not
  answered by having built one.
- **The judgement half is assumed to be what engineers want checked.** The whole of this phase rests
  on it. `FEMEX_BusinessModel.md` §8 question 4 is the test, it costs nothing to ask, and it has still
  not been asked — now with a report to put in front of someone while asking.
- **A generic section with no stated stiffness fails `Validate()`**, so a check report on a SAF import
  leads with a screen of errors about SAF's shape library rather than about the structure. Carried
  from Phase B, and much more visible now that the findings are rendered in a document: the house
  workbook reports 25 errors, 14 of them this.
- **`--format text` writes a `.txt` under `--out`.** Deliberate, and it is not a deliverable — C2 is
  that the report is HTML. Nothing stops someone handing one to a client.
- **Progress reporting.** `IProgress<TransferProgress>` is still in the signature and still unused.
  The batch driver now exists, which is the place the plan said it would first earn its keep.
