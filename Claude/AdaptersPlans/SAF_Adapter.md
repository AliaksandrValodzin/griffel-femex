# Plan — the SAF adapter, and the assurance surface it feeds

## Context

`Claude/FEMEX_Adapter_LicenceProcurement.md` establishes why SAF is adapter #1: it is the only
one of the six targets that needs **no licence, no install and no seat** — an Apache-2.0 C#
SDK on NuGet, and it reaches RFEM, SCIA, Archicad, ALLPLAN, RISA, StruSoft, AxisVM, SOFiSTiK,
ConSteel, IDEA StatiCa and Prota besides. Its Phase 1 says *what* to build. This plan settles
*what it looks like* — the question that document never asks, and that `FEMEX_Adapters.md` §8
explicitly rules out of scope ("Packaging, installers… Plugin UI… what is drawn against it is
not settled here").

**This is the second draft.** `FEMEX_BusinessAnalysis.md` and `FEMEX_BusinessModel.md` landed
after the first, and the business model's §9 declares this plan stale on two counts — the
product framing, and the Phase A ordering — while §7 holds that *"the architecture stands"* and
that *"all seven of its locked-in decisions survive unchanged."* The architecture does stand,
and Phases A and B below are substantially the ones first written. The decisions did not all
survive: two of the seven changed, and two more kept their conclusion while losing the argument
under it. §2 of that document gives the mapping away under Apache-2.0, and this plan had
justified its whole shape by keeping the mapping secret.

The state today:

- `griffel-femex` is a **`net7.0` class library with zero package references**, one project in
  the solution, 254 xUnit facts in a test project that is *not* in the solution. There is no
  `Interop/` folder and **no adapter, importer or exporter code of any kind** — every
  occurrence of "adapter"/"importer"/"interop" in `*.cs` is a doc comment.
- The ecosystem is three repos: this library, `griffel-femex-models` (a console exe that
  builds sample models, referencing the library as a **prebuilt binary** in `lib\`), and
  `griffel-femex-viewer` (**one self-contained HTML file**, no dependencies, no build step,
  opens from `file://`, with a deliberate *"no build-time link to the C# code"*).
- That viewer **already reimplements twelve `Validate*` families in JavaScript.** This was a
  detail when validation was a convenience. §3 of the business model makes validation the
  product, and A8 below is what that costs.
- Only **.NET SDK 7.0.302** is installed, so the `netstandard2.0;net8.0` multi-target that
  `FEMEX_Adapters.md` §3.7 calls for cannot build here today.

The intended outcome is no longer a subscription converter. It is a **model assurance tool**:
something that reads a structural model and says what is wrong with it, what changed in it, and
what a transfer did to it, producing a report an engineer can put in a project file and stand
behind. SAF is how a model gets in. The adapter stays a plain class library, because that is
what lets the same code later load into a Revit or ETABS add-in where a web app cannot go.

**What SAF is, and is not.** SAF reaches none of ETABS, Robot, SAP2000, Revit or RCB — the
programs this network actually runs. So SAF is **the proving ground and the distribution-reach
target, not a sales corridor.** It settles the contract against a real file for free, and it
gives a given-away library something to read on day one. It does not put the checker in front
of the people who would pay for it; that needs adapter #2, and §7 of the business model funds
adapter #2 from an engagement rather than from optimism. This resolves an open question the
first draft left open, and it should not be quietly forgotten the moment Phase B goes green.

## The shape

Not a standalone app, not the viewer as it stands, and not a web app *instead of* a library —
**a library, a report it can produce locally, and a web shell only once someone has asked for
one.**

Five layers, each replaceable without touching the others:

```
┌─ 1. Contract ────────────────────────────────────────────────────────┐
│  griffel-femex / Interop/          netstandard2.0 ; net8.0           │
│  IFemexImporter, IFemexExporter, TransferResult<T>, TransferMessage  │
│  — synchronous, in-process, no knowledge of files or HTTP            │
└──────────────────────────────────────────────────────────────────────┘
┌─ 2. Adapter ─────────────────────────────────────────────────────────┐
│  griffel-femex.Adapters.Saf        netstandard2.0 ; net8.0           │
│  SafImporter, SafExporter over the StructuralAnalysisFormat SDK      │
│  — the deliverable; no UI, no web, no process of its own             │
└──────────────────────────────────────────────────────────────────────┘
┌─ 3. Report ──────────────────────────────────────────────────────────┐
│  griffel-femex.Reporting           netstandard2.0 ; net8.0           │
│  Check · Compare · Transfer  ─▶  one self-contained .html            │
│  — the thing that is actually sold; no server required               │
└──────────────────────────────────────────────────────────────────────┘
┌─ 4. Drivers ─────────────────────────────────────────────────────────┐
│  griffel-femex.Cli   net8.0        femex check | compare | convert   │
│  griffel-femex-viewer + Open SAF… / Save As SAF / panels             │
│  — still one HTML file, still opens from file:// with SAF hidden     │
└──────────────────────────────────────────────────────────────────────┘
┌─ 5. Web shell ───── gated on §8 of FEMEX_BusinessModel.md ───────────┐
│  griffel-femex-hub                 ASP.NET Core net8.0               │
│  the same three layers behind HTTP; stateless, nothing persisted     │
└──────────────────────────────────────────────────────────────────────┘
```

**Why the adapter is not the app.** `FEMEX_Adapters.md` §3.6 fixes the call shape as
*synchronous, on the caller's thread*, and argues it from Revit's and ETABS' threading rules.
§3.7 fixes the runtime as `netstandard2.0;net8.0` so `net48` add-in hosts can load it. Both
constraints exist so a later adapter can live inside someone else's process. An adapter that
*is* a web app throws that away on adapter #1 and has to be rewritten for adapter #2. Keeping
SAF a library costs nothing now and is the whole reason the hub-and-spoke design works.

**Why the report is a local file first.** `FEMEX_BusinessModel.md` §5 orders the revenue:
audit engagements, then migration engagements, then subscription. The first two are delivered
by one person running many models through a pipeline on their own machine and handing back
documents — which is a CLI, not a service. Authentication, billing and upload limits are
irrelevant when the only operator is the author and the models arrive by whatever secure
transfer the client already uses. It also disposes of the confidentiality problem the first
draft listed as open: nothing transits anything.

**Why the report is HTML with no dependencies.** Because the viewer already proves that shape
works for exactly this audience: it opens from `file://`, survives being emailed, needs no
install, and cannot rot when a CDN moves. A report that a firm files against a project has to
still open in five years.

**Why the shell is later, and web when it comes.** The mapping is no longer a secret to
protect — §6 of the business model gives away the format, the library, the SAF adapter, the
conformance suite and the `LossCategory` taxonomy. What is kept is the hosted service, the
report, the cross-model matching heuristics and the judgement check rules. So server-side
execution is no longer a way to hide anything; it is only a convenience for people who will
not run a CLI, and it earns its cost once §8's conversations say there are such people.

**Why not Blazor WASM in the browser.** The first draft argued this from bypassable
subscriptions, which is now moot. Two reasons survive on their own and are enough: EPPlus and
SimpleInjector are a real trimming risk under WASM, and a 10–30 MB payload plus a build step
would be levied on a viewer whose founding property is *"self-contained, no dependencies, no
build step, opens from `file://`"*. Keeping conversion out of the browser keeps that intact:
the viewer gains buttons, not a toolchain.

**Why the viewer rather than a separate report page.** This argument gets stronger, not weaker.
§3.5 justifies `ObjectRef` carrying both `Id` and `Uid` by *"a UI that must highlight the
object"*, and the viewer already has the machinery — an `issues[]` panel with click-to-select,
and index maps keyed by id. That panel was built to draw validation messages, which is to say
**it is already the Check report's user interface**, and the transfer report is the same shape.
Seeing the converted model and what it lost in one view is also the fastest way to develop the
mapping, and it means **every conversion is a round-trip test** of the kind §7.1 defines.

## Decisions locked in

1. **Conversion and checking run in memory, and nothing is persisted.** Locally this is
   trivially true. It stays true if and when Phase E ships: no session id, no expiry, no copy
   of customer data at rest. *(Changed from the first draft, which specified server-side.)*
2. **The API is two pure functions.** *(Changed: the licence check is gone. Conversion is the
   giveaway, and gating it would gate the free tier that §5 wants as the top of the funnel. If
   anything is metered it is the report, and the business model's own* Still open *says that
   has not been thought through. It is not decided here.)*
3. **Account-stored FEMEX models — "FEMEX Hub" — are a later, opt-in layer**, deliberately
   separated from conversion so the "we do not keep your models" claim stays true by default.
   Note the claim is about the *product*; an audit engagement necessarily holds client models,
   and the two promises need separate wording.
4. **The transfer report has two sections, Import and Export.** They are different transfers
   with different failure modes (`FEMEX_Adapters.md` §4 defines the categories; its §1 notes `Invented` is
   overwhelmingly an import one and `Dropped` an export one), and a difference appearing only
   on export is an exporter bug, not an importer one. This is now one section of three.
5. **Multi-target `netstandard2.0;net8.0` as documented**, which requires installing the .NET 8
   SDK first. *(The reason is rewritten. It was "so `net48` add-in hosts can load it" — but §7
   of the business model builds no native connector until one is funded, so that consumer does
   not exist yet. It survives on a better argument: a library given away under Apache-2.0 to
   buy distribution must not exclude every .NET Framework consumer in the market it is aimed
   at. Reach is the point.)*
6. **The offline viewer must not regress.** Opened from `file://` with no server, the SAF
   buttons hide and it behaves exactly as it does today. *(Upgraded from courtesy to
   commitment: §5 wants "a genuinely useful free tier — the checker running locally on your own
   file", and the viewer already is one.)*
7. **Do not let authentication and billing block the mapping.** Phases A–D ship no web code at
   all, which is a stronger version of what this decision originally asked for.
8. **The C# engine is authoritative.** *(New — see A8.)* Any report that is sold or handed to a
   client is produced by `FemexModel.Validate()`, never by the viewer's JavaScript. The viewer
   panel is a preview and says so.
9. **The word "certify" does not appear in any user-facing string** until the professional
   indemnity question in the business model's *Still open* has an answer. The report states
   findings and provenance; it does not offer an engineering opinion.

## Phase A — Contract, diff, and the offline harness

This is `FEMEX_Adapter_LicenceProcurement.md` Phase 0, re-ordered. Nothing here is licence-gated
or web-gated.

**A1. Install .NET 8 SDK, then multi-target.** `griffel-femex.csproj` becomes
`<TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>`. Three consequences the docs do
not mention, all of which bite immediately:

- The `netstandard2.0` leg needs an explicit `System.Text.Json` (8.x) `PackageReference` —
  `[JsonPolymorphic]`/`[JsonDerivedType]` (`Geometry/Sections/Section.cs`) and
  `IJsonOnDeserialized` (`FemexModel.SelfWeight.cs:27`) are STJ 7+. This ends the project's
  **zero-package** record deliberately rather than by accident, and it is an assembly-binding
  hazard inside a `net48` AppDomain if an add-in is ever funded.
- `griffel-femex.Tests/griffel-femex.Tests.csproj` must move from `net7.0` to `net8.0`, or it
  silently tests the `netstandard2.0` leg — the wrong one.
- `..\griffel-femex-models\UpdateFemexDll.ps1` hardcodes `-TargetFramework net7.0` and will
  break. Update its default to `net8.0`.

**A2. Model-diff utility** implementing §7.2 — **moved to the front of this phase**, because
`FEMEX_BusinessModel.md` §7 is right that it is a product surface (Claim 2) rather than test
infrastructure. Match by `Uid` never `Id`, lists as sets under that key, geometry within
`GetCoincidenceTolerance()` (`FemexModel.Nodes.cs`), everything left over is a difference. This
wants `EnumerateIdentified()` (`FemexModel.Identity.cs:70`, private today) made public — §9
already calls it *"the cheapest additive change on this list."*

Two things the business model does not say, and this plan should:

- **It lives in a product namespace, not in `Interop/Conformance/`.** Promoting it later should
  not be a move.
- **It stays uid-keyed here. Do not build matching heuristics in this phase.** §7.2 is explicit
  that a model with partial uid coverage *"cannot be round-trip-tested at all"*, and B4 gives
  SAF full coverage via uid↔Name — so conformance needs no fallback and gets none. The
  geometric-and-topological matching that Claim 2 needs for two models from different programs
  is, in the business model's own words, the largest piece of new engineering in the plan and
  *"should get its own document before any of it is written"*. It is gated on §8 question 3.
  Two consumers, one utility, one of them built.

**A3. `Interop/` folder** in `griffel-femex`, per §3.7 — the types are given verbatim in
§3.1–§3.5 and should be transcribed as written: `IFemexAdapter`, `IFemexImporter`,
`IFemexExporter`, `TransferResult<T>`, `AdapterInfo`, `AdapterCapabilities`, `FemexEntity`,
`TransferDirection`, `TransferMessage`, `ObjectRef`, `LossCategory`.

**A4. Design the four types the contract names but never defines.** `ImportRequest`,
`ExportRequest`, `ExportReceipt` and `TransferProgress` appear only in the §3.1 signatures.
This is the seam that decides whether a driver — CLI or web — is cheap or expensive, and the
SAF SDK settles it: its services are **stream-based** (`IExcelImportService.Import(stream)`,
`IExcelExportService.Export(stream, model)`).

```csharp
public abstract class ImportRequest                  // no vendor types in the core library
{
    public string? SourceName { get; init; }         // diagnostics + TransferMessage text
    public IReadOnlyDictionary<string, string>? Options { get; init; }
}

public sealed class StreamImportRequest : ImportRequest
{
    public Stream Source { get; init; }              // SAF, and the §7.5 reference adapter
}
```

`ExportRequest`/`StreamExportRequest` mirror it with a `Destination`. Live-session adapters
(Revit, ETABS) subclass `ImportRequest` in **their own** assembly, so the core never references
a vendor type. `ExportReceipt` carries the §5.3 uid↔native-handle mapping **as data, not as a
sidecar path** — a batch run over forty models has nowhere sensible to scatter forty sidecars,
and this is where the docs' single-desktop framing shows. `TransferProgress` is a readonly
struct of `FemexEntity? Entity`, `int Completed`, `int Total`, `string? Text`.

**A5. Golden fixture** — `Examples/Conformance1.femex`, per §7.7. Remember the per-file
`<None Include>` line in `griffel-femex.Tests.csproj`; the glob is deliberately absent and a
missing line fails with `FileNotFoundException`.

**A6. Lossy in-memory reference adapter** (§7.5) exercising all five `LossCategory` values.

**A7. Conformance base class** with the six Tier-1 tests of **§7.3** — null tolerance, message
anchoring, name stability, capability honesty, no second gate, two-phase synthesis — so a later
adapter inherits them and cannot skip a rule by not writing its test. *(The first draft cited
§7.4; that is Tier 2, which is licence-gated and out of reach here.)*

**A8. The validation parity harness.** *(New, and the most important thing in this phase that
neither business document mentions.)*

`femex-viewer.html` independently reimplements `ValidateGravity`, `ValidateGrids`,
`ValidateGridGeometry`, `ValidateLoadOrientation`, `ValidateNameKeys`, `ValidateProjectedLoads`,
`ValidateSchemaVersion`, `ValidateSectionCompleteness`, `ValidateSections`, `ValidateSelfWeight`,
`ValidateUids` and `ValidateUidCoverage` in JavaScript — deliberately, per `FEMEXViewer.md`:
*"reads `.femex` files handed to it by the user and has no build-time link to the C# code."*

That was a sound trade when validation was a viewing convenience. It is a liability the moment
§3 of the business model makes the judgement half of `Validate()` the product and §4 says new
rules get added from real engagements: every new rule then costs two implementations, and any
drift means **the free checker and the paid report disagree about the same file**. That is the
*confidently incorrect* failure class the business model reserves for the diff, landed instead
on the flagship claim.

The fix must not reintroduce a build-time link, because the viewer's independence is worth
keeping. So route it through a checked-in artefact:

1. A test in `griffel-femex.Tests` writes `Examples/<name>.expected.json` for each
   `Examples/*.femex` — the `Validate()` message set, ordered — and fails if the checked-in
   file differs. The C# engine owns the artefact.
2. The viewer's existing headless-Chrome verification (`--dump-dom`, per *Verification
   performed* in `FEMEXViewer.md`) loads each example, extracts `issues[]`, and asserts it
   matches the artefact. Neither repo references the other at build time; both reference a file.
3. **Write down the rule for new judgement checks** in `FEMEXViewer.md`: C# first, JS mirror
   before release, or the panel is an explicitly labelled subset. Pick one. An unstated rule is
   how the two engines drifted in the first place.

**A9. Add `griffel-femex.Tests` and the new projects to `griffel-femex.sln`**, which currently
lists one project.

Follow the existing style throughout: `namespace griffel_femex` block-scoped, 4-space indent,
`{` on its own line, `Subject_Behaviour` test names, XML-doc summaries stating the failure the
test removes, and the byte-identity assertion
`Assert.Equal(File.ReadAllText(path), FemexModel.Load(path).ToJson())`.

## Phase B — The SAF adapter

New project **`griffel-femex.Adapters.Saf`**, multi-targeted like the library, project-
referencing `griffel-femex`. No UI, no web, no console.

**B1. Reference the SDK, and pin EPPlus exactly.**

```xml
<PackageReference Include="StructuralAnalysisFormat" Version="1.7.3" />
<PackageReference Include="EPPlus" Version="[4.5.3.3]" />   <!-- exact, not floating -->
```

The SDK declares `EPPlus >= 4.5.3.3`. **4.5.3.3 is the last LGPL release; version 5 onward is
Polyform Noncommercial and requires a paid commercial licence.** A transitive bump would put a
commercial product in breach silently, so the version bracket is not decoration.

**The first draft's answer to the LGPL question no longer holds and must be redone.** It read:
*"LGPL obligations attach to distribution, and a SaaS backend distributes nothing."* Under §6's
open-core position the adapter ships as source and as a package, and Phase C ships a CLI — so
**this product distributes**, and the item the first draft deferred as *"EPPlus if a desktop or
on-prem build ever ships"* is now the primary case rather than a hypothetical. It arrives in
Phase C, not never.

The likely answer is benign: an LGPL 4.5.3.3 package referenced from Apache-2.0 code is the
ordinary arrangement, provided the relinking freedom is preserved and the licence carried. But
it must be **confirmed rather than inherited from a framing that has been withdrawn**, and it
is confirmed before Phase B ships anything, not after.

**B2. Put the SDK behind a one-page seam** — `ISafGateway` with `Read(Stream) → ExcelModel` and
`Write(Stream, ExcelModel)` — for three reasons: the SDK requires a SimpleInjector bootstrapper
(`new SimpleInjectorBootstrapper()` → `CreateScope()` → `GetService<IExcelImportService>()`)
that should not leak into mapping code; it is the **same seam shape** Phase 3 of the licence doc
wants for record/replay, so the pattern is established on the free target first; and it is where
a permissively-licensed Excel reader would go.

That third reason is now promoted from insurance to an **evaluated option**: ClosedXML (MIT) or
NPOI (Apache-2.0) would remove the EPPlus question from an open-core product entirely. Scope it
honestly before assuming it — the SAF SDK itself depends on EPPlus, so this means bypassing the
SDK's Excel layer and reading the workbook directly against the SAF specification, not swapping
a package reference. That is real work. Cost it in Phase B; do not commit to it here.

**B3. `SafImporter : IFemexImporter` and `SafExporter : IFemexExporter`.** The SDK returns
`ExcelModel.Objects` — a **flat heterogeneous bag**, not a typed graph — so the importer groups
by type and dispatches. Mapping targets, from the SAF specification:

| SAF | FEMEX |
|---|---|
| `StructuralStorey` | `Level` |
| `StructuralPointConnection` | `Node` |
| `StructuralCurveMember` | `Bar` |
| `StructuralSurfaceMember` | `Plate` |
| `StructuralSurfaceMemberOpening` | `PlateRegion` (`Opening`) |
| `StructuralSurfaceMemberRegion` | `PlateRegion` |
| `StructuralMaterial` / `StructuralCrossSection` | `Material` / `Section` |
| `StructuralPointSupport`, `StructuralCurveConnection`, `StructuralEdgeConnection` | `Support`, `Hinge` |
| load cases, combinations, actions | `LoadCase`, `LoadCombination`, `Load` |

Better news than `FEMEX_Adapters.md` §9 feared: SAF *does* have storeys, openings and surface regions, so the
plate model maps structurally. The predicted losses to design for from the start —

- FEMEX `Grid`/`Gridline` → **Dropped** (SAF has no architectural grid concept).
- FEMEX plate-region **priority** → **Approximated** (SAF regions carry no priority; this is
  the one place review §3.2 says FEMEX is ahead, and the reason it stays worth having a format
  of one's own at all).
- FEMEX `Mesh` → **Dropped**.
- SAF `StructuralProxyElement` → **Unmapped**, reported *per concept, not per object* (§4.4).
- `Bar.SectionId == 0` on export → **Invented** placeholder, recognisable by name (§2.2, §5.4),
  so a `Rectangle` somebody chose stays distinguishable from one the adapter made up.

**B4. Identity.** SAF keys objects by **Name**, a string, so a SAF name is the natural
`TransferMessage.NativeHandle`. On import, call `AssignMissingUids()`
(`FemexModel.Identity.cs`) and record uid↔SAF-name in `ExportReceipt`, since §7.2 makes uid
coverage *"a precondition of the test suite, not a nicety."* This is also why A2's diff needs no
matching fallback to do its conformance job.

**B5. Write `Claude/FEMEX_SAF_Mapping.md`** — the first per-program mapping document, which §8
requires be written **after** a real file has been read, not before.

## Phase C — The report

New projects **`griffel-femex.Reporting`** (multi-targeted, no dependencies beyond the library)
and **`griffel-femex.Cli`** (`net8.0`). This is the phase the first draft did not have, and it
is the one that produces the thing that is actually sold.

**C1. One report, three sections**, mapping to the three claims of `FEMEX_BusinessModel.md` §3:

```
Model Assurance Report · steel-hall.femex · 2026-08-21 · femex 0.1.0 · sha256 3f9a…

  Check       Validate()                          14 findings   2 error · 12 warning
  Compare     vs steel-hall-2026-08-14.femex       6 differences
  Transfer    SAF → FEMEX → SAF                   14 losses
```

- **Check** renders `Validate(ValidationSeverity)`. §4 of the business model is right that the
  referential half is table stakes and the judgement half is the product; the report should let
  a reader see the judgement findings without the referential ones burying them.
- **Compare** renders A2's diff, and is present only when a second model is supplied.
- **Transfer** is the two-section Import/Export loss report of decision 4, present only when a
  conversion produced one.

**C2. Output is one self-contained HTML file** — no dependencies, no build step, opens from
`file://`, survives being emailed, still opens in five years. The same founding property as the
viewer, for the same reason. A `--format json` alternative exists for machine consumption.

**C3. Provenance is a first-class section, not a footer.** Source filenames, content hashes,
schema version, `FileMetadata` producer stamp, adapter version, tool version, date. The business
model argues that provable binding — *"these findings came from this model, this producer, this
version, on this date"* — is the auditable part, and it is worthless if it is a byline.

**C4. Batch mode.** `femex check *.femex --out reports/` produces N reports and one summary
index. This is what a migration engagement actually runs, and it is the reason this layer is a
CLI rather than a service. Exit codes: 0 clean, 1 findings, 2 tool failure.

**C5. The verbs.** `femex check`, `femex compare`, `femex convert` — the third wrapping the SAF
adapter so the same binary is how a conversion is done at all. Conversion is not gated.

**C6. Wording discipline**, per decision 9: findings and provenance, never an engineering
opinion, and never the word *certify*.

## Phase D — The front end

`griffel-femex-viewer`, still one HTML file, still no build step, still no dependencies. Nothing
here needs a server except the two SAF buttons.

**D1. The Check panel is the existing warnings panel**, promoted and labelled. It works today,
offline, from `file://` — this is §5's free tier and it already exists. Per decision 8 it is
labelled a preview, and points at the CLI for a report to keep.

**D2. Conversion panel**, two sections, built on the same code path:

```
Conversion · SAF → FEMEX → SAF

  Import  (steel-hall.xlsx)              8 losses
    ⚠ Invented    Bar 41    placeholder section
    ⚠ Unmapped    —         SAF StructuralProxyElement

  Export  (steel-hall-out.xlsx)          6 losses
    ⚠ Dropped     Grid 2    SAF has no grid concept
    ⚠ Approx.     Plate 7   region priority flattened
```

Each row resolves `ObjectRef.Id`/`Uid` through the existing index maps and selects the object
in 3D, exactly as an `issues[]` row already does.

**D3. `Open SAF…`** POSTs the file to a local or hosted converter and on success hands
`result.model` to the existing `parseFemex()`. The model, camera, scene and UI layers do not
change at all — this is the payoff of the viewer's existing four-layer split. **`Save As SAF`**
POSTs the untouched `raw` object and downloads the returned workbook. `Open`/`Save As FEMEX`
come free and need no server.

**D4. Degrade to today's behaviour.** Probe for a converter on load; on failure hide the SAF
buttons only. Everything else — including Check — keeps working from `file://`.

**D5. Later, and separately: FEMEX Hub storage** — opt-in per-account saving of FEMEX models.
Not conversion state, and explicitly a different promise to the user.

## Phase E — The web shell, if it is wanted

New repo **`griffel-femex-hub`**, ASP.NET Core `net8.0`. **Gated on `FEMEX_BusinessModel.md` §8
questions 1–3 landing** — this is the third revenue layer, and building it first is the mistake
the first draft made. Nothing below is designed further until then.

- Three minimal-API endpoints — `POST /api/import/saf`, `POST /api/export/saf`,
  `GET /api/adapters`. The last exists because §3.3 says *"a host needs to know what a plugin
  supports before offering it"*; the front end greys buttons from that response.
- Wire the request stream straight into `StreamImportRequest`, and `HttpContext.RequestAborted`
  into the contract's `CancellationToken` — the parameter §3.6 insists on *"from the first
  version or… never"*. No temp file, no `IFormFile.CopyToAsync` to disk.
- **A native API failure returns, it does not throw** (§3.6). `Value is not null` → 200 with
  messages; `Value is null` → 422 with the Error-severity messages as the body. A 500 means a
  bug, never a bad input file.
- Guardrails: max upload size, request timeout, and a test asserting the process writes no file
  during a conversion.
- Whatever is metered, it is not conversion. That question is open and belongs to whoever has
  had the §8 conversations.

`IProgress<TransferProgress>` has nowhere to go over a synchronous HTTP request — v1 passes
`null` and says so. If conversions turn out to be slow, that is when SSE or a job queue is
justified, not before.

## Files touched

- **New in `griffel-femex`:** `Interop/*.cs` (contract types), the model-diff utility in a
  product namespace, `Interop/Conformance/` (base class, lossy reference adapter),
  `Examples/Conformance1.femex`, `Examples/*.expected.json`, `Claude/FEMEX_SAF_Mapping.md`.
- **New projects:** `griffel-femex.Adapters.Saf/`, `griffel-femex.Adapters.Saf.Tests/`,
  `griffel-femex.Reporting/`, `griffel-femex.Cli/`.
- **New repo, deferred:** `griffel-femex-hub/`.
- **Modified:** `griffel-femex.csproj` (multi-target, `System.Text.Json` on the netstandard
  leg), `griffel-femex.Tests/griffel-femex.Tests.csproj` (→ `net8.0`, plus the
  `<None Include>` lines for the fixtures), `griffel-femex.sln` (add every project),
  `FemexModel.Identity.cs` (`EnumerateIdentified()` → public),
  `../griffel-femex-models/UpdateFemexDll.ps1` (`net7.0` → `net8.0`),
  `../griffel-femex-viewer/femex-viewer.html` + `FEMEXViewer.md` (SAF buttons, panel labelling,
  the parity check, and the stated rule for new judgement checks).
- **Reuse rather than reinvent:** `Validate(ValidationSeverity)`, `GetOrAddNode` /
  `GetCoincidenceTolerance` (`FemexModel.Nodes.cs`), `TryGetBarLocalAxes` /
  `TryGetPlateLocalAxes` / `TryGetLoadDirection` (`FemexModel.LocalAxes.cs`),
  `AssignMissingUids` (`FemexModel.Identity.cs`), `GetGravityDirection` / `GetWeightDensity` /
  `TryGetBarSelfWeightPerLength` (`FemexModel.SelfWeight.cs`), `GetTotalFactor`
  (`FemexModel.LoadCombinations.cs`), `FileMetadata` for the producer stamp, and on the JS side
  `parseFemex()` and the `issues[]` panel.

## Verification

- **Build:** .NET 8 SDK installed; `dotnet build` produces both legs; `dotnet test` green and
  the suite grows from its current 254 facts rather than being restructured. The
  `netstandard2.0` leg loads into a `net48` console host — proven before anything depends on it.
- **Phase A** is proven by the lossy reference adapter **failing** the conformance tests in
  exactly the ways it is designed to fail, and passing once its declarations match its
  behaviour. A harness that cannot tell a compliant plugin from a non-compliant one is not
  doing its job.
- **A8 specifically** is proven by editing one JS check to disagree with its C# counterpart and
  confirming the parity run goes red. A drift detector that has never detected drift is a
  decoration.
- **Phase B** is proven by importing **every file in the SAF examples corpus**, round-tripping
  SAF → FEMEX → SAF, and asserting under §7.2 equivalence that every difference is named by a
  `TransferMessage`. This is the one place to introduce `[Theory]`/`MemberData` — the repo is
  all `[Fact]` today, and a corpus test is the honest exception.
- **Independent oracle, free:** SAF publishes its own web-based viewer for testing SAF imports.
  Feeding it our exported `.xlsx` checks the writer against the *specification* rather than
  against our own reader — the failure mode a self-round-trip cannot catch.
- **Phase C** is proven end-to-end on `Examples/Example1.femex`: `femex check` produces a report
  whose findings match `Validate()`, `femex convert` round-trips through SAF and back under the
  existing byte-identity assertion style on the FEMEX side, and the report opens correctly from
  `file://` in a browser with the network disabled. Batch mode is proven on the SAF corpus.
- **Phase D** is proven the way the viewer already verifies itself — headless Chrome
  (`--dump-dom` for assertions, `--screenshot` for the overlay), per *Verification performed* in
  `FEMEXViewer.md` — plus opening the file from `file://` and confirming the SAF controls are
  absent and everything else, Check included, is unchanged.
- **Phase E**, if reached, adds a 422 (not a 500) for a corrupt upload and a test asserting the
  server writes nothing to disk.

## Still open

- **The corpus is thinner than assumed.** `StructuralAnalysisFormat-Examples` holds **11 `.xlsx`
  files in total** across versions 1.0.5→2.2.0, only one or two of them at 2.2.0 — not the broad
  corpus Phase 1 implies. Broaden it with Graphisoft's and SCIA's published SAF files early. The
  durable answer is `FEMEX_BusinessModel.md` §5: every engagement is a corpus of real exported
  files, which is the thing that has never existed.
- **Adapter #2, which is where the revenue actually is.** SAF does not reach ETABS, Robot or
  RCB, so Claim 1 has no input path to this network until something else exists. Per §7 of the
  business model it is a **file reader** rather than an API client, and it is funded by an
  engagement — most plausibly ETABS `.e2k`, which the customer exports and which needs no
  licence to read. Not designed here, and deliberately not started here.
- **EPPlus.** B1's original answer has been withdrawn; the replacement is stated but not
  confirmed, and the ClosedXML/NPOI alternative is scoped but not costed.
- **Whether the judgement half of `Validate()` is what engineers want checked.** The whole of
  Phase C assumes it. `FEMEX_BusinessModel.md` §8 question 4 is the test, and it costs nothing
  to ask before Phase C is written.
- **How two models are matched when uids do not survive.** A2 deliberately does not solve this.
  It gates Claim 2's cross-program form and needs its own document first.
- **Progress reporting.** `IProgress<TransferProgress>` is in the signature and unused. A batch
  run over forty models is the first place it would earn its keep.
- **Confidentiality.** Local-first removes it for now — nothing transits anything. It returns
  in full with Phase E, and it exists in a different form for engagements, where client models
  are necessarily held. The terms should say so before an engineer asks.
- **Professional indemnity, and the word "certify".** Decision 9 keeps it out of the product
  until an insurer has been asked. That question has not been asked.
- **Whether Hub storage weakens the "we do not keep your models" promise**, and how that is
  presented once it is opt-in rather than absent.
- **Nothing here has been tested against a real exported file yet.** `FEMEX_Adapters.md` §9 is
  explicit: *"The contract is a hypothesis until plugin #1 either confirms it or breaks it."*
  This plan is the attempt to break it.
