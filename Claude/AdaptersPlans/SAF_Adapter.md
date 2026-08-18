# Plan — the FEMEX↔SAF adapter, and the web shell that drives it

## Context

`Claude/FEMEX_Adapter_LicenceProcurement.md` establishes why SAF is adapter #1: it is the only
one of the six targets that needs **no licence, no install and no seat** — an Apache-2.0 C#
SDK on NuGet, and it reaches RFEM, SCIA, Archicad, ALLPLAN, RISA, StruSoft, AxisVM, SOFiSTiK,
ConSteel, IDEA StatiCa and Prota besides. Its Phase 1 says *what* to build. This plan settles
*what it looks like* — the question that document never asks, and that `FEMEX_Adapters.md` §8
explicitly rules out of scope ("Packaging, installers… Plugin UI… what is drawn against it is
not settled here").

The state today:

- `griffel-femex` is a **`net7.0` class library with zero package references**, one project in
  the solution, 254 xUnit facts in a test project that is *not* in the solution. There is no
  `Interop/` folder and **no adapter, importer or exporter code of any kind** — every
  occurrence of "adapter"/"importer"/"interop" in `*.cs` is a doc comment.
- The ecosystem is three repos: this library, `griffel-femex-models` (a console exe that
  builds sample models, referencing the library as a **prebuilt binary** in `lib\`), and
  `griffel-femex-viewer` (**one self-contained HTML file**, no dependencies, no build step,
  opens from `file://`, with a deliberate *"no build-time link to the C# code"*).
- Only **.NET SDK 7.0.302** is installed, so the `netstandard2.0;net8.0` multi-target that
  `FEMEX_Adapters.md` §3.7 calls for cannot build here today.

The intended outcome is a **subscription web product** — a user opens a SAF file in the
browser, sees the converted FEMEX model rendered, sees exactly what the crossing lost, and
saves back to SAF. The adapter itself stays a plain class library, because that is what lets
the same code later load into Revit and ETABS add-ins where a web app cannot go.

## The shape

Not a standalone app, not the viewer as it stands, and not a web app *instead of* a library —
**a library with a thin web shell, and the viewer promoted into that shell's front end.**

Four layers, each replaceable without touching the others:

```
┌─ 1. Contract ────────────────────────────────────────────────────────┐
│  griffel-femex / Interop/          netstandard2.0 ; net8.0           │
│  IFemexImporter, IFemexExporter, TransferResult<T>, TransferMessage  │
│  — synchronous, in-process, no knowledge of files or HTTP            │
└──────────────────────────────────────────────────────────────────────┘
┌─ 2. Adapter ─────────────────────────────────────────────────────────┐
│  griffel-femex.Adapters.Saf        netstandard2.0                    │
│  SafImporter, SafExporter over the StructuralAnalysisFormat SDK      │
│  — the deliverable; no UI, no web, no process of its own             │
└──────────────────────────────────────────────────────────────────────┘
┌─ 3. Web shell ───────────────────────────────────────────────────────┐
│  griffel-femex-hub                 ASP.NET Core net8.0               │
│  POST /api/import/saf   .xlsx  ─▶  { model, messages }               │
│  POST /api/export/saf   model  ─▶  { file, messages }                │
│  GET  /api/adapters            ─▶  capability declarations           │
│  — stateless: no session, no database, no temp file                  │
└──────────────────────────────────────────────────────────────────────┘
┌─ 4. Front end ───────────────────────────────────────────────────────┐
│  griffel-femex-viewer  +  Open SAF… / Save As SAF / Conversion panel │
│  — still one HTML file, still opens from file:// with SAF hidden     │
└──────────────────────────────────────────────────────────────────────┘
```

**Why the adapter is not the app.** `FEMEX_Adapters.md` §3.6 fixes the call shape as
*synchronous, on the caller's thread*, and argues it from Revit's and ETABS' threading rules.
§3.7 fixes the runtime as `netstandard2.0;net8.0` so `net48` add-in hosts can load it. Both
constraints exist so adapters #2–#5 can live inside someone else's process. An adapter that
*is* a web app throws that away on adapter #1 and has to be rewritten for adapter #2. Keeping
SAF a library costs nothing now and is the whole reason the hub-and-spoke design works.

**Why the shell is web and not desktop.** It is a subscription product, so there must be a
licence gate, and a gate only exists where the code does. Server-side conversion keeps the
mapping — the actual intellectual property — on your infrastructure, and makes the gate real
rather than decorative. It also removes the reason to obfuscate the adapter at all.

**Why not Blazor WASM in the browser.** It ships the mapping to every visitor, makes the
subscription bypassable, risks EPPlus and SimpleInjector under WASM trimming, and adds a
10–30 MB payload plus a build step to a viewer whose founding property is *"self-contained, no
dependencies, no build step, opens from `file://`"*. Server-side conversion keeps that
property intact: the viewer gains buttons, not a toolchain.

**Why the viewer rather than a separate converter page.** §3.5 justifies `ObjectRef` carrying
both `Id` and `Uid` by *"a UI that must highlight the object"*, and the viewer already has the
machinery — an `issues[]` panel with click-to-select, and index maps keyed by id. The loss
report is the same shape as the validation report it already draws. Seeing the converted model
and what it lost in one view is also the fastest way to develop the mapping, and it means
**every customer conversion is a round-trip test** of the kind §7.1 defines.

## Decisions locked in

1. **Conversion runs server-side, in memory, and nothing is persisted.** The source SAF file
   never touches disk; the FEMEX model lives in the browser tab between opening and saving.
2. **The API is two pure functions behind a licence check.** No session id, no expiry, no copy
   of customer data at rest. A scaled-out or restarted server loses nothing.
3. **Account-stored FEMEX models — "FEMEX Hub" — are a later, opt-in layer**, deliberately
   separated from conversion so the "we do not keep your models" claim stays true by default.
4. **The loss report has two sections, Import and Export.** They are different transfers with
   different failure modes (§4: `Invented` is an import category, `Dropped` an export one), and
   a difference appearing only on export is an exporter bug, not an importer one.
5. **Multi-target `netstandard2.0;net8.0` as documented**, which requires installing the .NET 8
   SDK first.
6. **The offline viewer must not regress.** Opened from `file://` with no server, the SAF
   buttons hide and it behaves exactly as it does today.
7. **Do not let authentication and billing block the mapping.** Phases A and B ship no web code
   at all.

## Phase A — Contract and offline harness

This is `FEMEX_Adapter_LicenceProcurement.md` Phase 0, unchanged in substance. Nothing here is
licence-gated or web-gated.

**A1. Install .NET 8 SDK, then multi-target.** `griffel-femex.csproj` becomes
`<TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>`. Three consequences the docs do
not mention, all of which bite immediately:

- The `netstandard2.0` leg needs an explicit `System.Text.Json` (8.x) `PackageReference` —
  `[JsonPolymorphic]`/`[JsonDerivedType]` (`Geometry/Sections/Section.cs`) and
  `IJsonOnDeserialized` (`FemexModel.SelfWeight.cs:27`) are STJ 7+. This breaks the project's
  current **zero-package** record and is an assembly-binding hazard inside Revit's `net48`
  AppDomain later.
- `griffel-femex.Tests/griffel-femex.Tests.csproj` must move from `net7.0` to `net8.0`, or it
  silently tests the `netstandard2.0` leg — the wrong one.
- `..\griffel-femex-models\UpdateFemexDll.ps1` hardcodes `-TargetFramework net7.0` and will
  break. Update its default to `net8.0`.

**A2. `Interop/` folder** in `griffel-femex`, per §3.7 — the types are given verbatim in
§3.1–§3.5 and should be transcribed as written: `IFemexAdapter`, `IFemexImporter`,
`IFemexExporter`, `TransferResult<T>`, `AdapterInfo`, `AdapterCapabilities`, `FemexEntity`,
`TransferDirection`, `TransferMessage`, `ObjectRef`, `LossCategory`.

**A3. Design the four types the contract names but never defines.** `ImportRequest`,
`ExportRequest`, `ExportReceipt` and `TransferProgress` appear only in the §3.1 signatures.
This is the seam that decides whether a web shell is cheap or expensive, and the SAF SDK
settles it: its services are **stream-based** (`IExcelImportService.Import(stream)`,
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
sidecar path** — a stateless web server has nowhere to put a file, and this is where the docs'
desktop framing shows. `TransferProgress` is a readonly struct of `FemexEntity? Entity`,
`int Completed`, `int Total`, `string? Text`.

**A4. Golden fixture** — `Examples/Conformance1.femex`, per §7.7. Remember the per-file
`<None Include>` line in `griffel-femex.Tests.csproj`; the glob is deliberately absent and a
missing line fails with `FileNotFoundException`.

**A5. Model-diff utility** implementing §7.2: match by `Uid` never `Id`, lists as sets under
that key, geometry within `GetCoincidenceTolerance()` (`FemexModel.Nodes.cs`), everything left
over is a difference. This wants `EnumerateIdentified()` (`FemexModel.Identity.cs:70`, private
today) made public — §9 already calls it *"the cheapest additive change on this list."*

**A6. Lossy in-memory reference adapter** (§7.5) exercising all five `LossCategory` values.

**A7. Conformance base class** with the six Tier-1 tests of §7.4, so adapter #5 inherits them
and cannot skip a rule by not writing its test.

**A8. Add `griffel-femex.Tests` and the new adapter project to `griffel-femex.sln`**, which
currently lists one project.

Follow the existing style throughout: `namespace griffel_femex` block-scoped, 4-space indent,
`{` on its own line, `Subject_Behaviour` test names, XML-doc summaries stating the failure the
test removes, and the byte-identity assertion
`Assert.Equal(File.ReadAllText(path), FemexModel.Load(path).ToJson())`.

## Phase B — The SAF adapter

New project **`griffel-femex.Adapters.Saf`**, `netstandard2.0`, project-referencing
`griffel-femex`. No UI, no web, no console.

**B1. Reference the SDK, and pin EPPlus exactly.**

```xml
<PackageReference Include="StructuralAnalysisFormat" Version="1.7.3" />
<PackageReference Include="EPPlus" Version="[4.5.3.3]" />   <!-- exact, not floating -->
```

The SDK declares `EPPlus >= 4.5.3.3`. **4.5.3.3 is the last LGPL release; version 5 onward is
Polyform Noncommercial and requires a paid commercial licence.** A transitive bump would put a
commercial product in breach silently, so the version bracket is not decoration. Server-side
hosting resolves the LGPL question that the licence doc says *"should be settled before Phase 1
starts"* — LGPL obligations attach to distribution, and a SaaS backend distributes nothing —
but the pin still matters, and the answer does **not** extend to any future desktop or on-prem
build.

**B2. Put the SDK behind a one-page seam** — `ISafGateway` with `Read(Stream) → ExcelModel` and
`Write(Stream, ExcelModel)` — for three reasons: the SDK requires a SimpleInjector bootstrapper
(`new SimpleInjectorBootstrapper()` → `CreateScope()` → `GetService<IExcelImportService>()`)
that should not leak into mapping code; the ClosedXML/NPOI fallback stays reachable if the
licence answer ever changes; and it is the **same seam shape** Phase 3 of the licence doc wants
for record/replay against Robot and ETABS, so the pattern is established on the free target
first.

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

Better news than §9 feared: SAF *does* have storeys, openings and surface regions, so the
plate model maps structurally. The predicted losses to design for from the start —

- FEMEX `Grid`/`Gridline` → **Dropped** (SAF has no architectural grid concept).
- FEMEX plate-region **priority** → **Approximated** (SAF regions carry no priority; this is
  the one place review §3.2 says FEMEX is ahead, and the reason SAF-as-permanent-hub is still
  an open question).
- FEMEX `Mesh` → **Dropped**.
- SAF `StructuralProxyElement` → **Unmapped**, reported *per concept, not per object* (§4.4).
- `Bar.SectionId == 0` on export → **Invented** placeholder, recognisable by name (§2.2), so a
  `Rectangle` somebody chose stays distinguishable from one the adapter made up.

**B4. Identity.** SAF keys objects by **Name**, a string, so a SAF name is the natural
`TransferMessage.NativeHandle`. On import, call `AssignMissingUids()`
(`FemexModel.Identity.cs`) and record uid↔SAF-name in `ExportReceipt`, since §7.2 makes uid
coverage *"a precondition of the test suite, not a nicety."*

**B5. Write `Claude/FEMEX_SAF_Mapping.md`** — the first per-program mapping document, which §8
requires be written **after** a real file has been read, not before.

## Phase C — The web shell

New repo **`griffel-femex-hub`**, ASP.NET Core `net8.0`, project- or binary-referencing the
adapter. Stand this up only once Phase B passes against the corpus.

**C1. Three minimal-API endpoints.**

```
POST /api/import/saf   multipart .xlsx  →  { model, messages[] }
POST /api/export/saf   FEMEX JSON       →  { file (base64), messages[] }
GET  /api/adapters                      →  AdapterCapabilities per adapter
```

`/api/adapters` exists because §3.3 says *"a host needs to know what a plugin supports before
offering it, so a user is not shown 'Export to X' for a program that cannot receive plates."*
The front end greys buttons from that response rather than hardcoding what SAF can do.

**C2. Wire the request stream straight into `StreamImportRequest`** and
`HttpContext.RequestAborted` into the contract's `CancellationToken` — the parameter §3.6
insists on *"from the first version or… never"* pays for itself immediately. No temp file, no
`IFormFile.CopyToAsync` to disk.

**C3. A native API failure returns, it does not throw** (§3.6). Map `TransferResult` to HTTP:
`Value is not null` → 200 with messages; `Value is null` → 422 with the Error-severity messages
as the body. A 500 means a bug, never a bad input file.

**C4. Subscription gate** as auth middleware on the conversion endpoints only; the viewer
itself stays freely servable.

**C5. Guardrails:** max upload size, request timeout, and a test asserting the process writes
no file during a conversion.

`IProgress<TransferProgress>` has nowhere to go over a synchronous HTTP request — v1 passes
`null` and says so. If conversions turn out to be slow, that is when SSE or a job queue is
justified, not before.

## Phase D — The front end

`griffel-femex-viewer`, still one HTML file, still no build step, still no dependencies.

**D1. `Open SAF…`** POSTs the file, and on 200 hands `result.model` to the existing
`parseFemex()`. The model, camera, scene and UI layers do not change at all — this is the
payoff of the viewer's existing four-layer split.

**D2. `Save As SAF`** POSTs the untouched `raw` object and downloads the returned workbook.
`Open`/`Save As FEMEX` come free and need no server.

**D3. Conversion panel**, two sections, built on the existing warnings-panel code path:

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

**D4. Degrade to today's behaviour.** Probe `/api/adapters` on load; on failure hide the SAF
buttons. The `file://` tool keeps working unchanged.

**D5. Later, and separately: FEMEX Hub storage** — opt-in per-account saving of FEMEX models.
Not conversion state, and explicitly a different promise to the user.

## Files touched

- **New in `griffel-femex`:** `Interop/*.cs` (contract types), `Interop/Conformance/` (base
  class, diff utility, lossy reference adapter), `Examples/Conformance1.femex`,
  `Claude/FEMEX_SAF_Mapping.md`.
- **New projects:** `griffel-femex.Adapters.Saf/`, `griffel-femex.Adapters.Saf.Tests/`.
- **New repo:** `griffel-femex-hub/`.
- **Modified:** `griffel-femex.csproj` (multi-target, `System.Text.Json` on the netstandard
  leg), `griffel-femex.Tests/griffel-femex.Tests.csproj` (→ `net8.0`, plus the
  `<None Include>` line for the fixture), `griffel-femex.sln` (add tests + adapter),
  `FemexModel.Identity.cs` (`EnumerateIdentified()` → public),
  `../griffel-femex-models/UpdateFemexDll.ps1` (`net7.0` → `net8.0`),
  `../griffel-femex-viewer/femex-viewer.html` + `FEMEXViewer.md`.
- **Reuse rather than reinvent:** `GetOrAddNode` / `GetCoincidenceTolerance`
  (`FemexModel.Nodes.cs`), `TryGetBarLocalAxes` / `TryGetPlateLocalAxes` /
  `TryGetLoadDirection` (`FemexModel.LocalAxes.cs`), `AssignMissingUids`
  (`FemexModel.Identity.cs`), `GetGravityDirection` / `GetWeightDensity` /
  `TryGetBarSelfWeightPerLength` (`FemexModel.SelfWeight.cs`), `GetTotalFactor`
  (`FemexModel.LoadCombinations.cs`), `Validate(ValidationSeverity)`, `FileMetadata` for the
  producer stamp, and on the JS side `parseFemex()` and the `issues[]` panel.

## Verification

- **Build:** .NET 8 SDK installed; `dotnet build` produces both legs; `dotnet test` green and
  the suite grows from its current 254 facts rather than being restructured. The
  `netstandard2.0` leg loads into a `net48` console host — proven before any Revit add-in
  depends on it.
- **Phase A** is proven by the lossy reference adapter **failing** the conformance tests in
  exactly the ways it is designed to fail, and passing once its declarations match its
  behaviour. A harness that cannot tell a compliant plugin from a non-compliant one is not
  doing its job.
- **Phase B** is proven by importing **every file in the SAF examples corpus**, round-tripping
  SAF → FEMEX → SAF, and asserting under §7.2 equivalence that every difference is named by a
  `TransferMessage`. This is the one place to introduce `[Theory]`/`MemberData` — the repo is
  all `[Fact]` today, and a corpus test is the honest exception.
- **Independent oracle, free:** SAF publishes its own web-based viewer for testing SAF imports.
  Feeding it our exported `.xlsx` checks the writer against the *specification* rather than
  against our own reader — the failure mode a self-round-trip cannot catch.
- **Phase C** is proven by converting `Examples/Example1.femex` → SAF → back with the existing
  byte-identity assertion style on the FEMEX side, a 422 (not a 500) for a corrupt upload, and
  a test asserting the server writes nothing to disk.
- **Phase D** is proven the way the viewer already verifies itself — headless Chrome
  (`--dump-dom` for assertions, `--screenshot` for the overlay), per *Verification performed* in
  `FEMEXViewer.md` — plus opening the file from `file://` and confirming the SAF controls are
  absent and nothing else changed.

## Still open

- **The corpus is thinner than assumed.** `StructuralAnalysisFormat-Examples` holds **11 `.xlsx`
  files in total** across versions 1.0.5→2.2.0, only one or two of them at 2.2.0 — not the broad
  corpus Phase 1 implies. Broaden it with Graphisoft's and SCIA's published SAF files early;
  RFEM's free model library only helps once RFEM can re-export, which is licence-gated.
- **Whether SAF becomes a permanent intermediate or only a proving ground.** Plate-region
  priority is the deciding evidence, and Phase B produces it.
- **Host-side request discovery.** `StreamImportRequest` answers the seam for SAF, but how a
  host learns which `ImportRequest` subclass a live-session adapter wants is unanswered.
  Deferred to adapter #2, where it is a real problem rather than a hypothetical.
- **Progress reporting.** `IProgress<TransferProgress>` is in the signature and unused over a
  synchronous HTTP request.
- **Confidentiality in writing.** Models transit the server even though nothing is persisted.
  Engineering practices will ask; the terms should say it before they do.
- **EPPlus if a desktop or on-prem build ever ships.** The SaaS answer does not cover a product
  the customer installs, and neither does the obfuscation pipeline next door.
- **Whether Hub storage weakens the "we do not keep your models" promise**, and how that is
  presented once it is opt-in rather than absent.
- **Nothing here has been tested against a real exported file yet.** `FEMEX_Adapters.md` §9 is
  explicit: *"The contract is a hypothesis until plugin #1 either confirms it or breaks it."*
  This plan is the attempt to break it.
