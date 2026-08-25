# Plan — the SAF adapter, and the assurance surface it feeds

## Context

`Claude/FEMEX_Adapter_LicenceProcurement.md` establishes why SAF is adapter #1: it is the only
one of the six targets that needs **no licence, no install and no seat** — an Apache-2.0 C#
SDK on NuGet, and it reaches RFEM, SCIA, Archicad, ALLPLAN, RISA, StruSoft, AxisVM, SOFiSTiK,
ConSteel, IDEA StatiCa and Prota besides. Its Phase 1 says *what* to build. This plan settles
*what it looks like* — the question that document never asks, and that `FEMEX_Adapters.md` §8
explicitly rules out of scope ("Packaging, installers… Plugin UI… what is drawn against it is
not settled here").

**This is the third draft.** The second was written on 21 August 2026 against schema **1.6**, a
254-fact suite, and a repository in which `FEMEX_SAF_Fit.md` did not yet exist. Four documents and
two schema bumps have landed since, and each of them lands on this plan:

- **`FEMEX_SAF_Fit.md`** measured FEMEX object-by-object against SAF 2.2.0. Its §9 names **§B3 of
  this plan stale by name** — *"the mapping table's nine rows and five predicted losses are correct
  as far as they go and cover roughly a fifth of the surface"* — and its §2 and §8.2 replace them
  with a sheet-by-sheet walk and a **28-entry loss catalogue**. B3 below is rewritten around that
  catalogue rather than patched.
- **`FEMEX_SAF_Fit_Update_Plan.md`** landed **1.7** (materials) and **1.8** (units, restraint sense,
  bedding semantics), **held** two further bumps that were already designed, and introduced
  **Step 0' — one real SAF 2.2.0 workbook**, which the second draft did not contain in any form.
  Phase 0 below is that step, promoted to the front of this plan.
- **`FEMEX_MaterialCompleteness_Summary.md`** and **`FEMEX_UnitsAndRestraints_Summary.md`** are the
  implementation record, and between them they leave two deferred decisions that this plan must own
  because the adapter and the CLI are where they bite.
- **`FEMEX.md`** gained two "Extended by" blockquotes, and `FEMEX_Interop_Status_16082026.md` §3 and
  §5 were brought current: its items 1–5 are all done, and **item 6 — one real file — is the next
  thing**, which is the same conclusion this plan now reaches from the adapter side.

The architecture stands, again. What changed is the *facts*, the *mapping*, the *ordering* and three
of the *arguments*.

The state today, verified against the working tree at commit `14de087`:

- `griffel-femex` is a **`net7.0` class library with zero package references and zero project
  references** (`griffel-femex.csproj:4`), one project in the solution, and a test project that is
  **not** in the solution and holds **307 `[Fact]` plus 2 `[Theory]` over 17 `[InlineData]` rows —
  324 tests**, up from 254 across the two bumps.
- There is no `Interop/` folder and **no adapter, importer or exporter code of any kind.**
  `IFemexImporter`, `IFemexExporter`, `TransferResult`, `LossCategory`, `TransferMessage` and
  `ObjectRef` return **zero hits in `*.cs`** — not even as doc comments. They exist only in
  `Claude/*.md`.
- The library emits schema **`1.8`** (`FemexModel.cs:77`). There are **three** examples;
  `Examples/Example3.femex` landed with 1.8 as the end-to-end fixture, with `Example3Tests.cs`
  beside it.
- The ecosystem is three repos: this library, `griffel-femex-models` (a console exe that builds
  sample models, referencing the library as a **prebuilt binary** in `lib\`), and
  `griffel-femex-viewer` (**one self-contained HTML file**, 6 182 lines, one `<script>` tag, zero
  external `src=`/`href=`, opens from `file://`, with a deliberate *"no build-time link to the C#
  code"*).
- That viewer **mirrors fifteen C# validator families and two migrations in JavaScript**, and tracks
  `CURRENT_SCHEMA_VERSION = '1.8'` (`femex-viewer.html:502`). A8 below is what that costs.
- Only **.NET SDK 7.0.302** is installed, so the `netstandard2.0;net8.0` multi-target that
  `FEMEX_Adapters.md` §3.7 calls for still cannot build here today. The `net8.0` *runtime* is
  present; the SDK is not.

The intended outcome is a **model assurance tool**: something that reads a structural model and says
what is wrong with it, what changed in it, and what a transfer did to it, producing a report an
engineer can put in a project file and stand behind. SAF is how a model gets in. The adapter stays a
plain class library, because that is what lets the same code later load into a Revit or ETABS add-in
where a web app cannot go.

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
mapping, and it means **every conversion is a round-trip test** — with the qualification
decision 10 now attaches to that phrase.

## Decisions locked in

1. **Conversion and checking run in memory, and nothing is persisted.** Locally this is
   trivially true. It stays true if and when Phase E ships: no session id, no expiry, no copy
   of customer data at rest.
2. **The API is two pure functions.** No licence check. Conversion is the giveaway, and gating it
   would gate the free tier that §5 wants as the top of the funnel. If anything is metered it is the
   report, and the business model's own *Still open* says that has not been thought through. It is
   not decided here.
3. **Account-stored FEMEX models — "FEMEX Hub" — are a later, opt-in layer**, deliberately
   separated from conversion so the "we do not keep your models" claim stays true by default.
   Note the claim is about the *product*; an audit engagement necessarily holds client models,
   and the two promises need separate wording.
4. **The transfer report has two sections, Import and Export.** They are different transfers
   with different failure modes (`FEMEX_Adapters.md` §4 defines the categories; its §1 notes `Invented` is
   overwhelmingly an import one and `Dropped` an export one), and a difference appearing only
   on export is an exporter bug, not an importer one. This is one section of three.
5. **Multi-target `netstandard2.0;net8.0` as documented**, which requires installing the .NET 8
   SDK first. The reason is reach, not add-in hosting: §7 of the business model builds no native
   connector until one is funded, so that consumer does not exist yet, but a library given away
   under Apache-2.0 to buy distribution must not exclude every .NET Framework consumer in the market
   it is aimed at.
6. **The offline viewer must not regress.** Opened from `file://` with no server, the SAF
   buttons hide and it behaves exactly as it does today. §5 wants *"a genuinely useful free tier —
   the checker running locally on your own file"*, and the viewer already is one.
7. **Do not let authentication and billing block the mapping.** Phases 0–D ship no web code at
   all, which is a stronger version of what this decision originally asked for.
8. **The C# engine is authoritative.** Any report that is sold or handed to a client is produced by
   `FemexModel.Validate()`, never by the viewer's JavaScript. The viewer panel is a preview and says
   so.
9. **The word "certify" does not appear in any user-facing string** until the professional
   indemnity question in the business model's *Still open* has an answer. The report states
   findings and provenance; it does not offer an engineering opinion.
10. **Round-trip equivalence is asserted modulo declared losses.** *(New.)* The second draft leaned
    on *"every conversion is a round-trip test"* twice, and `FEMEX_SAF_Fit.md` §0 names that reliance
    as the thing its curved-geometry finding damages: chorded curves are *"the only [loss] in this
    document that is non-reversible on every round trip"*. The claim survives restated — SAF → FEMEX
    → SAF must produce a model in which **every** difference is named by a `TransferMessage`, and one
    class, chorded curves, is reversible only if Phase 0 decides to carry `ParentUid`. A round trip
    that is silently unequal is a bug; one that is loudly unequal is the product working.
11. **The order is Phase 0 → the two held bumps → Phase B.** *(New.)* Four of `FEMEX_SAF_Fit.md`
    §4's eight silent wrong answers are still open — member behaviour, load-panel spanning direction,
    analysis eccentricity, varying members — and they are exactly what the two held bumps close.
    Mapping them against 1.8 first means writing that mapping twice, and shipping an adapter that
    knowingly manufactures the failure class `FEMEX_BusinessModel.md` §4 says the product exists to
    catch. The held bumps are already designed; what they lack is a real file, which is Phase 0.
12. **Every invented mandatory column is an *Invented* message, never a silent default.** *(New —
    see D4 of Phase 0.)* `FEMEX_Adapters.md` §4.3 calls *Invented* *"the important category, and the
    one naive adapters never report, because from inside the adapter an invention does not feel like
    a loss — it feels like success."* SAF's mandatory columns are where that temptation is strongest,
    because the workbook will not validate without them.

## Phase 0 — one real workbook, and four decisions before any mapping code

> **Done, 25 August 2026 — `Claude/FEMEX_SAF_Corpus_Notes.md`.** All eleven published workbooks read
> with the SDK; `SAF_example_HOUSE_metric_ZYX_220.xlsx` read sheet by sheet. **P2** decided (carry the
> position on the load, never mint a node and never snap; joins the held bumps), **P3** decided
> (`Guid? ParentUid` on `IIdentified`, scoped as provenance), **P4** decided (boundary catch now,
> tolerant enum parsing designed and deferred with the reason recorded), **P5** decided — and three
> rows of its table below changed. Four things in this plan are now stale: P5's table, the round-trip
> line under *Verification*, the EPPlus and corpus entries under *Still open*, and every "2.2.0" on
> the write leg — **the SDK writes 2.3.0**. See §10 of the notes.

**New in this draft, and first.** `FEMEX_SAF_Fit_Update_Plan.md`'s *Step 0'* argues this is about a
day rather than a blocked dependency: eleven SAF workbooks are published, the SDK is an ordinary
NuGet reference, and reading four sheets is an afternoon. It needs no multi-target and no solution
change — a throwaway console referencing `StructuralAnalysisFormat` is enough — so it does **not**
wait on A1.

**P1. Read one real SAF 2.2.0 workbook.** Fetch one; read `StructuralLoadGroup`,
`StructuralSurfaceActionDistribution`, `StructuralCurveMemberVarying` and `StructuralMaterial`;
record what was actually found — column names, enum spellings, which mandatory columns real files
leave blank, whether varying members use one span or several — in **`Claude/FEMEX_SAF_Corpus_Notes.md`**.
That confirms 1.7's `MaterialType` spellings retroactively, corrects both held bumps' invented
shapes, and corrects B3's mapping before B3 is written.

The four decisions below are what the workbook is being read to settle. None of them should be made
by whoever writes the importer first — `FEMEX_Adapters.md` §7.2 fixed the equivalence definition
before the diff was written, for exactly this reason.

**P2. Position along a member.** `FEMEX_SAF_Fit.md` §7.1 calls this *"the largest single omission,
and the one most likely to be forced by the first real file"*. SAF addresses six classes by position
along a member — `StructuralPointAction`, `StructuralPointMoment`, `StructuralCurveAction`,
`StructuralCurveMoment`, `StructuralCurveConnection`, and 2.2's on-beam `StructuralPointSupport` —
through `Position x`, `Start point`/`End point`, `Origin`, `Coordinate definition` and `Extent`.
FEMEX addresses all six by node id: `PointLoad.NodeNumber`, `LinearLoad.StartNode`/`EndNode`,
`Support`/`Hinge.NodeIds`. Three answers, each wrong differently:

| Answer | What it costs |
|---|---|
| Mint a node and split the member | changes topology, element count and member identity; **breaks §7.2 equivalence**, and therefore breaks this plan's own Phase B verification |
| Snap to the nearer end | changes the answer, silently — the failure class the product exists to catch |
| Refuse and report *Dropped* | loses a load that is on nearly every real file, honestly |

`LinearLoad` is closest to a fix: it already carries an optional `BarId` for local-direction
resolution, so relative start and end positions along that bar would be additive. `PointLoad` would
need the host reference it does not have. **Decide after the workbook**; if the answer is a schema
change it joins the held bumps rather than Phase B.

**P3. `ParentUid`.** `FEMEX_SAF_Fit.md` §6.1 recommends one nullable Guid beside `Uid` on
`IIdentified`: it costs nothing to anyone who does not use it, and it lets a chorded arc's pieces
point at the object they came from — so a FEMEX → SAF write can re-emit the arc, and A2's diff can
tell that eight bars are one member. That second consumer is `FEMEX_BusinessModel.md` §3 Claim 2
territory, which makes this the cheapest item on this list with two independent arguments behind it.
Against: §6.1 leaves the *scope* open — one field, or the thin end of a derivation-tracking design
nobody has thought about. Decision 10 depends on the answer.

**P4. What a `.femex` read does with an unrecognised enum value.** 1.7 and 1.8 each left the same
note — `"type": "Composite"` and `"lengthUnit": "Furlong"` **throw on read**, as every enum in the
repository does, *"consistent, and worth a decision of its own"*. From the adapter and the CLI it
stops being a style question:

- `FEMEX_Adapters.md` §3.6's rule is that a failure **returns, it does not throw**, and A7's Tier-1
  conformance tests include null tolerance and no second gate.
- Phase C's exit codes are `0 clean / 1 findings / 2 tool failure`. A file from a *later* schema
  carrying one unknown enum value lands on **2 — a crash** — where `IExtensible`'s whole design says
  an unknown member is preserved, re-emitted and named.
- It is asymmetric with `IExtensible`: an unknown *member* survives a round trip; an unknown *enum
  value* is fatal to the file.

Minimum scope, whatever is decided about the format: **the CLI and the adapter never surface a
`.femex` read failure as an unhandled exception.**

**P5. The export-leg invention policy.** FEMEX **cannot write a workbook SAF's own validator will
accept**, and no previous draft of this plan said so. Of `FEMEX_SAF_Fit.md` §3's five mandatory
columns, 1.7 closed two. Four remain, and two conditionals sit beside them:

| Mandatory SAF column | State | What the exporter must do |
|---|---|---|
| `StructuralMaterial.Type` / `.Quality` | **closed at 1.7** | pass through |
| `StructuralLoadCase.Load group` | held bump | synthesise one group per nature, invent `Relation` |
| `Model.System of units` | 1.8 **reports, does not fill** — five per-quantity enums are not one `Metric \| Imperial` flag, and they permit `Metre` with `Kip` | assume, report *Invented* |
| `Model.National code` | deliberately out of the format (§8.1) | write `EC-Standard-EN`, report *Invented* |
| `Model.LCS of cross-section` | derivable from FEMEX's fixed convention, stated nowhere | assert it, report *Invented* |
| `StructuralCrossSection.Form code` *(conditional)* | codes 1–8 are FEMEX's eight shapes exactly; 9–23 have no discriminator | write `0` (`-`, provisional) so the receiver knows the shape is unknown |
| `Analysis Y/Z Eccentricity` + `System line` *(conditional)* | held bump — the update plan's *Context* calls it a **sixth** un-writable mandatory column | invent zero eccentricity, report *Invented* |

This is not housekeeping. This plan's *Verification* proposes feeding an exported `.xlsx` to SAF's
own published web viewer as an **independent oracle** — the one check a self-round-trip cannot make —
and that oracle rejects a workbook missing mandatory columns. The invention policy is a precondition
of a verification step this plan already depends on.

## Phase A — Contract, diff, level clustering, and the offline harness

This is `FEMEX_Adapter_LicenceProcurement.md` Phase 0, re-ordered. Nothing here is licence-gated
or web-gated.

**A1. Install .NET 8 SDK, then multi-target.** `griffel-femex.csproj` becomes
`<TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>`. Three consequences the docs do
not mention, all of which bite immediately:

- The `netstandard2.0` leg needs an explicit `System.Text.Json` (8.x) `PackageReference` —
  `[JsonPolymorphic]`/`[JsonDerivedType]` (`Geometry/Sections/Section.cs:29-30`) and
  `IJsonOnDeserialized` (`FemexModel.SelfWeight.cs:27`, the hook at `:49`) are STJ 7+. This ends the
  project's **zero-package** record deliberately rather than by accident, and it is an
  assembly-binding hazard inside a `net48` AppDomain if an add-in is ever funded. 1.8's
  `[JsonPropertyName]` legacy shims and `[EditorBrowsable]` add nothing to that bill.
- `griffel-femex.Tests/griffel-femex.Tests.csproj` must move from `net7.0` to `net8.0`, or it
  silently tests the `netstandard2.0` leg — the wrong one.
- `..\griffel-femex-models\UpdateFemexDll.ps1` hardcodes `-TargetFramework net7.0` as a parameter
  default and will break. Update it to `net8.0`. `griffel-femex-models` consumes the library as a
  binary `<Reference>` with a `HintPath` into `lib\`, so nothing else catches this.

**A2. Model-diff utility** implementing §7.2 — **at the front of this phase**, because
`FEMEX_BusinessModel.md` §7 is right that it is a product surface (Claim 2) rather than test
infrastructure. Match by `Uid` never `Id`, lists as sets under that key, geometry within
`GetCoincidenceTolerance()` (`FemexModel.Nodes.cs:34`), everything left over is a difference. This
wants `EnumerateIdentified()` (`FemexModel.Identity.cs:70`, private today) made public — §9
already calls it *"the cheapest additive change on this list."*

Three things the business model does not say, and this plan should:

- **It lives in a product namespace, not in `Interop/Conformance/`.** Promoting it later should
  not be a move.
- **It stays uid-keyed here. Do not build matching heuristics in this phase.** §7.2 is explicit
  that a model with partial uid coverage *"cannot be round-trip-tested at all"*, and B4 gives
  SAF full coverage via uid↔Name — so conformance needs no fallback and gets none. The
  geometric-and-topological matching that Claim 2 needs for two models from different programs
  is, in the business model's own words, the largest piece of new engineering in the plan and
  *"should get its own document before any of it is written"*. It is gated on §8 question 3.
  Two consumers, one utility, one of them built.
- **It is where FEMEX's one lead over SAF is worth money.** `FEMEX_SAF_Fit.md` §5 confirms against
  the current spec that `StructuralSurfaceMemberRegion` and `StructuralSurfaceMemberOpening` carry
  **no precedence field of any kind**, so overlapping SAF regions are undefined behaviour, while
  FEMEX's rule — highest `Priority` wins, base panel `int.MinValue`, ties broken
  `Opening > LoadOnly > Structural`, then list order — is total and deterministic. That is *"the one
  place `FEMEX_BusinessModel.md` §6 can point at when asked why a format of one's own exists"*, and a
  diff is where a reader sees it.

**A2b. The level-clustering helper.** *(New, and it belongs here rather than in the adapter.)*
`FEMEX_Adapters.md` §6.1 settles the *policy* — *"snap an incoming elevation to an existing `Level`
within tolerance; otherwise create one. Always emit an *Invented* message for a level the native
model did not have"* — and then says outright that **it names no existing helper, because none
exists**; §7.6 counts it as implied code beside the diff and the reference adapter. The repository
has `GetCoincidenceTolerance` (`FemexModel.Nodes.cs:34`), a three-dimensional *node* tolerance, and
nothing at all for matching a `Level.AbsoluteElevation`.

Build it to §6.1's stated shape: a **relative** tolerance derived from the model's own vertical
extent with a floor, in the manner of `GetCoincidenceTolerance`'s `1e-6 × diagonal` over a `1e-9`
floor (`FemexModel.Nodes.cs:65`) — never an absolute millimetre, which means something different in a
metre model and a millimetre one. The number itself is still deliberately unfixed; Phase 0's workbook
is the first evidence anyone has had.

And carry §6.2's rule with it, because it is the half that is easy to skip: **node and level
synthesis are two-phase.** `GetCoincidenceTolerance` is `1e-6` of the model's *current* bounding
diagonal, so an import that starts from an empty model begins at the floor and grows as the model
fills — *"the same native model read in a different order yields a different node table"*, which is
fatal to §7.2 equivalence and therefore to Phase B's verification. Collect every candidate coordinate
and elevation first, cluster once against the finished extent, then create. Never stream one element
at a time.

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
`<None Include>` line in `griffel-femex.Tests.csproj` — there are now **three** of them, at `:34`,
`:36` and `:38`; the glob is deliberately absent and a missing line fails with
`FileNotFoundException`.

**A6. Lossy in-memory reference adapter** (§7.5) exercising all five `LossCategory` values.

**A7. Conformance base class** with the six Tier-1 tests of **§7.3** — null tolerance, message
anchoring, name stability, capability honesty, no second gate, two-phase synthesis — so a later
adapter inherits them and cannot skip a rule by not writing its test. *(§7.4 is Tier 2, which is
licence-gated and out of reach here.)* Note that the null-tolerance and no-second-gate tests are what
Phase 0's P4 decision has to hold up.

**A8. The validation parity harness.** *(The premise has changed since the second draft, and in the
direction that makes this cheaper and more urgent at once.)*

`femex-viewer.html` independently mirrors the C# engine in JavaScript — **fifteen** validator
families named in its section comments (`ValidateGravity`, `ValidateSections`, `ValidateMaterials`,
`ValidateLoadOrientation`, `ValidateGrids`, `ValidateGridGeometry`, `ValidateSchemaVersion`,
`ValidateUids`, `ValidateUidCoverage`, `ValidateNameKeys`, `ValidateSectionCompleteness`,
`ValidateMaterialCompleteness`, `ValidateSupportCompleteness`, `ValidateSelfWeight`,
`ValidateProjectedLoads`) plus both migrations, `MigrateLegacyLoadIds` and `MigrateLegacyUnits` —
deliberately, per `FEMEXViewer.md`: *"reads `.femex` files handed to it by the user and has no
build-time link to the C# code."*

**The second draft predicted drift. Drift has not happened.** Three of those fifteen —
`ValidateMaterials`, `ValidateMaterialCompleteness`, `ValidateSupportCompleteness` — arrived with 1.7
and 1.8, along with the unit migration and `Restraint.Sense`; the viewer tracks
`CURRENT_SCHEMA_VERSION = '1.8'` and `SELF_WEIGHT_VERSIONS`, and its header comments cite the two
implementation summaries by filename. Two bumps, mirrored by hand, in step.

That is the argument for A8, not against it. A practice that has survived twice **on discipline
alone, with no stated rule and no automated check**, is exactly the thing to lock down before §4 of
the business model starts adding judgement rules from paying engagements — because the day it fails,
**the free checker and the paid report disagree about the same file**, which is the *confidently
incorrect* failure class the business model reserves for the diff, landed on the flagship claim.

The fix must not reintroduce a build-time link, because the viewer's independence is worth keeping.
So, in this order:

1. **Write the rule down first**, in `FEMEXViewer.md`, because it is nearly free and it is the thing
   currently missing: C# first, JS mirror before release, or the panel is an explicitly labelled
   subset. Pick one. An unstated rule is a practice, not a guarantee.
2. A test in `griffel-femex.Tests` writes `Examples/<name>.expected.json` for each
   `Examples/*.femex` — the `Validate()` message set, ordered — and fails if the checked-in
   file differs. The C# engine owns the artefact. None of these files exists today.
3. The viewer's existing headless-Chrome verification (`--dump-dom`, per *Verification
   performed* in `FEMEXViewer.md`) loads each example, extracts `issues[]`, and asserts it
   matches the artefact. Neither repo references the other at build time; both reference a file.

**A9. Add `griffel-femex.Tests` and the new projects to `griffel-femex.sln`**, which currently
lists one project. Until then a bare `dotnet test` at the repo root runs zero tests silently.

Follow the existing style throughout: `namespace griffel_femex` block-scoped, 4-space indent,
`{` on its own line, `Subject_Behaviour` test names, XML-doc summaries stating the failure the
test removes, and the byte-identity assertion
`Assert.Equal(File.ReadAllText(path), FemexModel.Load(path).ToJson())`.

## Phase A′ — the two held bumps, 1.9 and 1.10

**A pointer, not a redesign.** Both are already specified in `FEMEX_SAF_Fit_Update_Plan.md`'s *Held*
sections, and each carries a defect found in review that must be fixed when it resumes. Decision 11
puts them here, between Phase 0 and Phase B, because they are the four remaining silent wrong answers
Phase B would otherwise map twice.

- **Held — loads.** `LoadGroup` + `LoadGroupType` + `LoadGroupRelation` (SAF's mandatory `Load group`
  reference), `SurfaceLoadSpanning` + `LoadDistribution` on the *panel* rather than the load, and
  signed `GradientY`/`GradientZ` on `TemperatureLoad`. Two recorded defects: the `gradientPerDepth`
  migration **assigns meaning to a real number** — `Examples/Example1.femex:3545` carries
  `"gradientPerDepth": 30` and the 1.6 field had no sign convention at all, so the decision must be
  made deliberately and recorded, and `ReportMigrations()` must call it a *reinterpretation*; and
  `LoadNature` and `LoadGroupType` become **two sources of truth** for load category that can
  disagree, which needs the stated compatibility map and a warning designed in rather than discovered.
- **Held — members.** `BarBehaviour` (SAF's four values exactly), `BarAlignment` (the nine system
  lines), `BarEccentricity` (the Structural/Analysis split SAF gets right and everyone else fuses),
  and `Bar.EndSectionId` for a single linear taper. Recorded correction: a single taper **downgrades**
  §4 item 5 to *Approximated*, it does not close it — a rafter haunched at both ends still arrives
  with the wrong moment distribution, now with a message attached. Phase 0's workbook says whether
  real files use one span or several.

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
is confirmed before Phase B ships anything, not after. (Phase 0's spike distributes nothing and is
not gated on this.)

**B2. Put the SDK behind a one-page seam** — `ISafGateway` with `Read(Stream) → ExcelModel` and
`Write(Stream, ExcelModel)` — for three reasons: the SDK requires a SimpleInjector bootstrapper
(`new SimpleInjectorBootstrapper()` → `CreateScope()` → `GetService<IExcelImportService>()`)
that should not leak into mapping code; it is the **same seam shape** Phase 3 of the licence doc
wants for record/replay, so the pattern is established on the free target first; and it is where
a permissively-licensed Excel reader would go.

That third reason is an **evaluated option**, not insurance: ClosedXML (MIT) or NPOI (Apache-2.0)
would remove the EPPlus question from an open-core product entirely. Scope it honestly before
assuming it — the SAF SDK itself depends on EPPlus, so this means bypassing the SDK's Excel layer and
reading the workbook directly against the SAF specification, not swapping a package reference. That
is real work. Cost it in Phase B; do not commit to it here.

**B3. `SafImporter : IFemexImporter` and `SafExporter : IFemexExporter`.**

**The mapping is `FEMEX_SAF_Fit.md` §2, and the message catalogue is its §8.2.** The second draft's
nine-row table and five predicted losses are withdrawn — §9 of that document names them stale, and it
is right: they covered roughly a fifth of the surface. §2 walks the workbook sheet by sheet in the
SAF specification's own chapter order, which is the order an implementer works in; §8.2 gives
**twenty-eight** declared losses already sorted by `LossCategory`, with the per-concept versus
per-object distinction already made. Do not restate either here; a mapping table maintained in two
places is a mapping table that disagrees with itself. §8.2 is an **acceptance criterion**: a Phase B
test asserts the importer emits at least the enumerated per-concept messages.

What this draft adds beyond those two sections:

- **The SDK returns `ExcelModel.Objects` — a flat heterogeneous bag, not a typed graph** — so the
  importer groups by type and dispatches.
- **Levels are synthesised on every file, and that is the highest-traffic *Invented* the adapter will
  ever produce.** The second draft called SAF's storeys *"better news than `FEMEX_Adapters.md` §9
  feared"*. They are not. `StructuralStorey` is `Name`, `Height level [m]`, `Id` — and
  `FEMEX_SAF_Fit.md` §1.1 establishes that **nothing in SAF references a storey**, while
  `Node.LevelNumber` is a required foreign key enforced as an **error**
  (`FemexModel.Validation.cs:419`). So every SAF file arrives as a bag of free coordinates, levels
  must be synthesised before one node can be written, and a model with no storey meaning at all — a
  truss, a ramp, a transfer structure — acquires levels the source never had. Use A2b, obey the
  two-phase rule, and emit one *Invented* per synthesised level.
- **SAF's own edge indexing is inconsistent, and the adapter must handle both.**
  `StructuralEdgeConnection.Edge`, `StructuralCurveAction.Edge`, `RelConnectsRigidMember.Edges` and
  `ResultInternalForce2DEdge.Edge` are **1-based**; `RelConnectsSurfaceEdge.Edge` and
  `StructuralProxyElement`'s vertex and face indices are **0-based** (§5). FEMEX naming an edge by its
  two nodes is what makes that safe on this side.
- **The numeric layer is the one place the export leg loses nothing.** SAF's optional
  `A, Iy, Iz, It, Iw, Wply, Wplz` are seven; `SectionProperties` carries all seven — `J` is SAF's
  `It` — **plus** `ShearAreaY`, `ShearAreaZ`, `Wely` and `Welz` (§5). Worth knowing, because every
  other document in this set records sections as FEMEX's weakest area.
- **One self-weight wrinkle** (§2.5). SAF has no self-weight load object: it is a load case with
  `Load type = Self weight`, generated by the receiver and scaled through the combination's
  `Multiplier`. A FEMEX `SelfWeightFactor` other than 0 or 1 therefore has no SAF home *on the case*
  and must be pushed into the `Multiplier` of every combination naming it — equivalent only when the
  case appears in combinations at all. Report *Approximated*; do not assume the arithmetic works out.
- **The export leg obeys decision 12 and Phase 0's P5 table.** Every invented mandatory column
  produces an *Invented* message.

**B4. Identity.** SAF keys objects by **Name**, a string, so a SAF name is the natural
`TransferMessage.NativeHandle`. On import, call `AssignMissingUids()`
(`FemexModel.Identity.cs:44`) and record uid↔SAF-name in `ExportReceipt`, since §7.2 makes uid
coverage *"a precondition of the test suite, not a nicety."* This is also why A2's diff needs no
matching fallback to do its conformance job.

Two asymmetries §1.3 records and the adapter must handle rather than assume away: FEMEX names are
`string?` with a blank or duplicate reported as a **warning**, where SAF treats a duplicate name
within a sheet as fatal; and **`Bar`, `Node`, `Support` and `Hinge` carry no name property at all**,
while their SAF counterparts all require one. So a name is synthesised on export for four of the
largest sheets in the file, in `FEMEX_Adapters.md` §5.4's `{Kind}-{8 hex}` form — **a round trip
through FEMEX renames most of the model**. Legal, visible, and worth telling a user once rather than
per object.

**B5. Write `Claude/FEMEX_SAF_Mapping.md`** — the first per-program mapping document, which §8
requires be written **after** a real file has been read. `FEMEX_SAF_Fit.md` is the prior step, not
this one; Phase 0's `FEMEX_SAF_Corpus_Notes.md` is the raw material.

## Phase C — The report

New projects **`griffel-femex.Reporting`** (multi-targeted, no dependencies beyond the library)
and **`griffel-femex.Cli`** (`net8.0`). This is the phase that produces the thing that is actually
sold.

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
CLI rather than a service. Exit codes: 0 clean, 1 findings, 2 tool failure — and per Phase 0's P4,
a `.femex` this build cannot fully read is a **1 with a finding**, not a 2 with a stack trace.

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
    ⚠ Invented    Level 3   synthesised from elevation 9.60
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
  bug, never a bad input file. Phase 0's P4 is what makes that true for a `.femex` as well as for a
  workbook.
- Guardrails: max upload size, request timeout, and a test asserting the process writes no file
  during a conversion.
- Whatever is metered, it is not conversion. That question is open and belongs to whoever has
  had the §8 conversations.

`IProgress<TransferProgress>` has nowhere to go over a synchronous HTTP request — v1 passes
`null` and says so. If conversions turn out to be slow, that is when SSE or a job queue is
justified, not before.

## Files touched

- **New docs:** `Claude/FEMEX_SAF_Corpus_Notes.md` (Phase 0), `Claude/FEMEX_SAF_Mapping.md` (B5).
  Per the repository's pair convention, each phase that ships code ships a summary beside its plan:
  `FEMEX_Interop_Contract_Summary.md` (Phase A), `FEMEX_SAF_Adapter_Summary.md` (Phase B),
  `FEMEX_Reporting_Summary.md` (Phase C).
- **New in `griffel-femex`:** `Interop/*.cs` (contract types), the model-diff utility and the
  level-clustering helper in a product namespace, `Interop/Conformance/` (base class, lossy reference
  adapter), `Examples/Conformance1.femex`, `Examples/*.expected.json`.
- **New projects:** `griffel-femex.Adapters.Saf/`, `griffel-femex.Adapters.Saf.Tests/`,
  `griffel-femex.Reporting/`, `griffel-femex.Cli/`.
- **New repo, deferred:** `griffel-femex-hub/`.
- **Modified:** `griffel-femex.csproj` (multi-target, `System.Text.Json` on the netstandard
  leg), `griffel-femex.Tests/griffel-femex.Tests.csproj` (→ `net8.0`, plus a fourth
  `<None Include>` line for `Conformance1.femex`), `griffel-femex.sln` (add every project),
  `FemexModel.Identity.cs:70` (`EnumerateIdentified()` → public),
  `../griffel-femex-models/UpdateFemexDll.ps1` (`net7.0` → `net8.0`),
  `../griffel-femex-viewer/femex-viewer.html` + `FEMEXViewer.md` (SAF buttons, panel labelling,
  the parity check, and the stated rule for new judgement checks).
- **Reuse rather than reinvent:** `Validate(ValidationSeverity)` (`FemexModel.Validation.cs:67`),
  `GetOrAddNode` (`FemexModel.Nodes.cs:101`) / `GetCoincidenceTolerance` (`:34`),
  `TryGetBarLocalAxes` / `TryGetPlateLocalAxes` / `TryGetLoadDirection` (`FemexModel.LocalAxes.cs`),
  `AssignMissingUids` (`FemexModel.Identity.cs:44`), `GetGravityDirection` / `GetWeightDensity` /
  `TryGetBarSelfWeightPerLength` (`FemexModel.SelfWeight.cs`), `GetShearModulus`
  (`Materials/Material.cs:183`, stated wins over derived), `GetTotalFactor`
  (`FemexModel.LoadCombinations.cs`), `FileMetadata` for the producer stamp, and on the JS side
  `parseFemex()` and the `issues[]` panel.

## Verification

- **Phase 0** is proven by a file on disk: `Claude/FEMEX_SAF_Corpus_Notes.md`, recording what one
  real 2.2.0 workbook actually contains, with the four decisions of P2–P5 answered or explicitly
  deferred with a reason.
- **Build:** .NET 8 SDK installed; `dotnet build` produces both legs; `dotnet test` green and
  the suite grows from its current **324** tests rather than being restructured. Note the test
  project is not in the solution, so `dotnet test griffel-femex.Tests\griffel-femex.Tests.csproj`
  until A9 lands. The `netstandard2.0` leg loads into a `net48` console host — proven before anything
  depends on it.
- **Phase A** is proven by the lossy reference adapter **failing** the conformance tests in
  exactly the ways it is designed to fail, and passing once its declarations match its
  behaviour. A harness that cannot tell a compliant plugin from a non-compliant one is not
  doing its job.
- **A2b specifically** is proven by a determinism test: the same workbook read twice with two
  different traversal orders yields **identical node and level tables**. That is §6.2's rule made
  executable, and it is the assertion that catches the growing-tolerance trap before Phase B's
  round-trip test blames the mapping for it.
- **A8 specifically** is proven by editing one JS check to disagree with its C# counterpart and
  confirming the parity run goes red. A drift detector that has never detected drift is a
  decoration — and since the two engines are currently *in* step, this is the only way to know the
  detector works at all.
- **Phase B** is proven by importing **every file in the SAF examples corpus**, round-tripping
  SAF → FEMEX → SAF, and asserting under §7.2 equivalence **modulo declared losses** (decision 10)
  that every difference is named by a `TransferMessage`. `[Theory]`/`[MemberData]` is the right shape
  for a corpus test and needs no special pleading: 1.8 already introduced `[Theory]`/`[InlineData]`
  in `UnitsTests.cs:133,151`.
- **Phase B, message coverage:** a test asserting that `FEMEX_SAF_Fit.md` §8.2's per-concept
  messages are all emitted for a workbook exercising them — the catalogue is the specification, so it
  is also the checklist.
- **Independent oracle, free:** SAF publishes its own web-based viewer for testing SAF imports.
  Feeding it our exported `.xlsx` checks the writer against the *specification* rather than
  against our own reader — the failure mode a self-round-trip cannot catch. **This step depends on
  P5**: without the invention policy the workbook is missing mandatory columns and the oracle rejects
  it before it reads anything.
- **Phase C** is proven end-to-end on `Examples/Example1.femex`: `femex check` produces a report
  whose findings match `Validate()`, `femex convert` round-trips through SAF and back under the
  existing byte-identity assertion style on the FEMEX side, and the report opens correctly from
  `file://` in a browser with the network disabled. Batch mode is proven on the SAF corpus, and one
  case asserts that a `.femex` carrying an unrecognised enum value exits **1**, not 2.
- **Phase D** is proven the way the viewer already verifies itself — headless Chrome
  (`--dump-dom` for assertions, `--screenshot` for the overlay), per *Verification performed* in
  `FEMEXViewer.md` — plus opening the file from `file://` and confirming the SAF controls are
  absent and everything else, Check included, is unchanged.
- **Phase E**, if reached, adds a 422 (not a 500) for a corrupt upload and a test asserting the
  server writes nothing to disk.

## Still open

- **The corpus is thinner than assumed.** `StructuralAnalysisFormat-Examples` holds **11 `.xlsx`
  files in total** across versions 1.0.5→2.2.0, only one or two of them at 2.2.0 — not the broad
  corpus Phase 1 implies. Phase 0 needs only one of them; Phase B needs more. Broaden it with
  Graphisoft's and SCIA's published SAF files early. The durable answer is
  `FEMEX_BusinessModel.md` §5: every engagement is a corpus of real exported files, which is the
  thing that has never existed.
- **Adapter #2, which is where the revenue actually is.** SAF does not reach ETABS, Robot or
  RCB, so Claim 1 has no input path to this network until something else exists. Per §7 of the
  business model it is a **file reader** rather than an API client, and it is funded by an
  engagement — most plausibly ETABS `.e2k`, which the customer exports and which needs no
  licence to read. Not designed here, and deliberately not started here.
- **EPPlus.** B1's original answer has been withdrawn; the replacement is stated but not
  confirmed, and the ClosedXML/NPOI alternative is scoped but not costed.
- **`ParentUid`'s scope** (P3). One nullable Guid, or the thin end of a derivation-tracking design.
  Decision 10 depends on the answer, and so does whether a chorded arc can ever be written back.
- **Position along a member** (P2). Three answers, all wrong differently, and the one most likely to
  be forced by the first real file. If the answer is a schema change it joins the held bumps.
- **What a `.femex` read does with an unrecognised enum value** (P4). Noted twice by the schema
  summaries as *"worth a decision of its own"*, and never taken.
- **Four mandatory SAF columns FEMEX still cannot fill** (P5), of which `Model.System of units` is
  the interesting one: 1.8's five typed enums are the right shape and deliberately do **not** supply
  SAF's single `Metric | Imperial` flag.
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
  Phase 0 is the first draft of this plan to schedule the attempt rather than assume it.
