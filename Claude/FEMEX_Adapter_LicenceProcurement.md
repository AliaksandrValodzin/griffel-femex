# Plan — getting licences and real models for the FEMEX adapters

## Context

`Claude/FEMEX_Assessment.md` §1 names five target programs — Autodesk Robot, Revit, CSI ETABS,
INDUCTA RCB, Dlubal RFEM 6 — plus SAF 2.2 as the benchmark. `Claude/FEMEX_Adapters.md` has already
settled the adapter contract, and `Claude/FEMEX_Interop_Status_16082026.md` confirms the state
plainly: **not one line of adapter code exists**, `griffel-femex.sln` contains one project, and every
occurrence of "Robot", "ETABS", "Revit", "RFEM" or "SAF" in `*.cs` is a doc comment.

Two things are now known that the existing docs do not account for:

1. **This is a commercial product, and no seat of any of the five programs is available today.**
   That removes the cheapest route entirely: Autodesk's and Dlubal's education licences may only be
   used for "learning, teaching, training, research or development that is part of the instructional
   functions performed by an educational institution" and explicitly **not for commercial,
   professional or any other for-profit purposes**. Trials are evaluation-only on the same logic.
   Building a commercial adapter on an education licence is a licence breach, not a shortcut.
2. **`FEMEX_Adapters.md` §7.4 names the licence constraint but does not solve it.** It separates a
   Tier-1 offline suite from a Tier-2 live suite and stops there. There is no design for a captured
   native-API session, no mock of the COM/OAPI surface, and no file corpus. So the round trip that is
   the entire justification for the loss-report design is licence-gated and undesigned.

The intended outcome of this plan is that **adapter work stops being blocked on procurement**: a
sequence where months of real, testable work happen before any licence is needed, procurement runs in
parallel on its own clock, and when a licence does arrive it is spent on capture rather than
exploration.

## The reframing that makes this tractable

"I need a licence" is actually three separate problems with three different answers. Conflating them
is what makes it look hopeless.

| Problem | What it really needs | Can it be solved without a seat? |
|---|---|---|
| **A. Compile against the API** | The SDK assemblies / type libraries / WSDL | **Mostly yes.** CSI publishes the OAPI DLL and docs; Dlubal's WSDL and Python client are public and Apache-licensed; Revit API assemblies come from the RevitAPI NuGet mirrors and ADN. Robot's COM interop is the exception — it needs the install. |
| **B. Run the adapter against a live program** | A running, licensed instance | **No — but only once per adapter, not per developer per CI run.** This is what record/replay solves. |
| **C. Have real models to test against** | Files exported by the real program | **Largely yes**, and this is the surprise. Free corpora exist for four of the six targets. |

Everything below follows from separating those.

## What is actually available, per target

Verified August 2026. Costs are directional; confirm current pricing with each vendor.

| Target | API route | Licence route for a **commercial** developer | Free real-file source |
|---|---|---|---|
| **SAF 2.2** | Official **`StructuralAnalysisFormat` C# SDK**, NuGet, **Apache-2.0**, **targets `netstandard2.0`**, v1.7.3 (Apr 2025). Deps: EPPlus 4.5.3.3 (LGPL — the pre-5.0 line, so commercially usable), FluentValidation, UnitsNet | **None needed.** Open, royalty-free, no install, no seat | `StructuralAnalysisFormat/StructuralAnalysisFormat-Examples` on GitHub; Graphisoft's published SAF data files and cross-section catalogues |
| **Dlubal RFEM 6** | SOAP Web Services; `RFEM_Python_Client` is public and documents the object model one-for-one | 90-day full trial (evaluation terms). **API access is separately gated** — trial users hit `UNAUTHENTICATED – API access has been restricted`; Dlubal support enables it on request. For commercial dev, ask Dlubal directly for a **developer/partner licence** | Dlubal's public **"Models to Download"** library (free, free account). **RFEM reads and writes SAF** — so RFEM is reachable through the SAF adapter before any RFEM licence exists |
| **CSI ETABS** | OAPI, documented, cross-product since v18; has `FrameObj.SetGUID` for identity | 30-day trial, single machine. No public developer tier — **request an NFR / developer licence from CSI directly**; CSI runs a developer programme and this is a normal ask for someone shipping an interop plugin | `.e2k` is text and widely shared publicly, so a corpus is obtainable — but the review is explicit that its `.e2k` grammar is **reconstruction, not specification**, so a parser built on it is unverified until an export is read |
| **Autodesk Revit** | .NET API add-in; `AnalyticalMember`/`AnalyticalPanel`/`AnalyticalLink` | **ADN membership** is the correct answer. ADN **Open** tier is no-cost; **start-ups qualify for free membership up to 3 years**; Standard/Professional are paid. ADN licences are "strictly for development purposes and not for production work" — which is exactly this use. Plus **APS Design Automation runs headless Revit in the cloud with no desktop licence at all**, metered in Flex tokens, with a free tier for dev/test | Autodesk's official sample projects, incl. `rst_advanced_sample_project.rvt`, free download |
| **Autodesk Robot** | COM / RobotOM — needs the install; `.str` is explicitly ruled out by the review (frozen, cannot carry panels) | Same ADN route as Revit. No cloud-headless equivalent | Robot ships example projects with the install — so this one is genuinely licence-gated end to end |
| **INDUCTA RCB** | **Nothing public.** No API, no schema, no neutral text format. DXF or a per-version Revit Link, or nothing | Free, fully functional **30-day trial** with a licence key on request. Small vendor — a direct conversation is likely to go further than with Autodesk or CSI | None. Formats are proprietary zipped containers |

**Read across the table:** SAF costs nothing and reaches RFEM plus SCIA, Archicad, ALLPLAN, RISA,
FRILO, StruSoft, AxisVM, SOFiSTiK, ConSteel, IDEA StatiCa and Prota. Revit is cheap via ADN. ETABS
and Robot are one procurement conversation each. RCB is genuinely hard and belongs last.

## The plan

### Phase 0 — Contract and offline harness (no licence, no vendor software)

This is the work `FEMEX_Adapters.md` §7.6 already deferred, and none of it is blocked.

1. **`Interop/` folder in `griffel-femex`**, per §3.7 — `IFemexAdapter`, `IFemexImporter`,
   `IFemexExporter`, `TransferResult<T>`, `AdapterInfo`, `AdapterCapabilities`, `FemexEntity`,
   `TransferDirection`, `TransferMessage`, `ObjectRef`, `LossCategory`, `ImportRequest`,
   `ExportRequest`, `ExportReceipt`, `TransferProgress`.
2. **Multi-target `griffel-femex.csproj` to `netstandard2.0;net8.0`** — §3.7 flagged this as the one
   build change the contract implies and did not make. It is now doubly justified: the SAF SDK is
   `netstandard2.0`, and Revit 2023/2024 and the OAPI hosts are `net48`. Cost is real — the
   `netstandard2.0` leg needs an explicit `System.Text.Json` package reference for `[JsonPolymorphic]`,
   `[JsonDerivedType]` and `IJsonOnDeserialized`, which breaks the project's current zero-package
   record and is an assembly-binding hazard inside Revit's `net48` AppDomain.
3. **Golden fixture file.** Serialise `SampleModels.Build()` once to `Examples/Conformance1.femex`
   and treat the file as the baseline, per §7.7. Remember the per-file `<None Include>` line in the
   csproj — the glob is deliberately absent and a missing line fails with `FileNotFoundException`.
4. **Model-diff utility** implementing §7.2 equivalence: match by `Uid` never `Id`, lists as sets,
   geometry within `GetCoincidenceTolerance()`, everything left over is a difference.
5. **Lossy in-memory reference adapter** (§7.5) that exercises all five `LossCategory` values.
6. **Conformance base class** with the six Tier-1 tests, so adapter #5 inherits them and cannot skip
   a rule by not writing its test.
7. **Level-clustering helper** (§6.1), including the two-phase synthesis §6.2 requires.

Follow the existing test style — `RoundTripTests.cs`, `RoundTripIdentityTests.cs`,
`Subject_Behaviour` naming, and the byte-identity assertion
`Assert.Equal(File.ReadAllText(path), FemexModel.Load(path).ToJson())`.

Add both `griffel-femex.Tests` and any adapter projects to `griffel-femex.sln`, which currently
lists one project.

### Phase 1 — SAF adapter first (still no licence)

The status note's item 6 — "one real ETABS or RFEM export, round-tripped" — has been waiting on a
licence. It does not have to. RFEM exports SAF, and SAF files can be read with an Apache-2.0 SDK on a
machine with nothing installed.

1. Reference `StructuralAnalysisFormat` 1.7.3. **Check EPPlus 4.5.3.3's LGPL terms against your
   distribution model before committing** — dynamic linking is fine, static/ILMerge is not. If it is a
   problem, the fallback is reading the SAF worksheets directly with an MIT/Apache xlsx library
   (ClosedXML or NPOI) rather than through the SDK.
2. Build `SafImporter` / `SafExporter` against the contract from Phase 0.
3. Test against the `StructuralAnalysisFormat-Examples` corpus and the Graphisoft SAF data files.
   These are **real files written by real programs**, which is precisely what §9 says the contract has
   never been tested against.
4. Write `Claude/FEMEX_SAF_Mapping.md` — the first of the per-program mapping documents §8 says must
   be written *after* a real file has been read.

What this buys beyond an adapter: it is the first honest test of the contract, it settles the status
note's open question "whether FEMEX should target SAF as an intermediate" with evidence rather than
argument, and it produces the round-trip data that tells you which of the nine untouched P1 entities
actually matter. It also puts real pressure on the one place FEMEX is *ahead* of SAF — the
priority-based plate region model of review §3.2 — which is exactly where you want early evidence.

### Phase 2 — Procurement, started now and running in parallel

These have lead times measured in weeks, so start them at the same time as Phase 0, not after it.

1. **ADN application** — Open tier is no-cost, start-ups qualify for free membership for up to three
   years. This covers **both** Revit and Robot for development, legitimately, under a commercial
   entity. Highest value per unit of effort of anything in this plan.
2. **CSI** — write to them describing the FEMEX interop plugin and request a developer/NFR licence.
   Being able to say "here is the adapter contract and here is a working SAF adapter" makes this a
   much easier conversation than asking cold.
3. **Dlubal** — request API-enabled access. Their own community threads show support enabling the API
   for non-full licences on request; ask for developer terms rather than burning the 90-day trial.
4. **INDUCTA** — direct contact. Small vendor, no public API, so the only realistic outcomes are a
   conversation about DXF/Revit-Link or a decision to drop RCB from scope. Ask early enough that the
   answer can inform scope.
5. **In every one of these conversations, ask for two things**: a development licence, and
   **exported sample models** in whatever format they will give you. Vendors give away sample files far
   more readily than licences.

### Phase 3 — Record/replay, so a licence is spent once and not repeatedly

This is the missing piece in `FEMEX_Adapters.md` §7.4 and it is what makes Tier 2 survivable.

- Put a **thin seam interface** between each adapter and its vendor API — the narrow set of calls the
  adapter actually makes, nothing more. The adapter talks to the seam; the seam talks to COM/OAPI/SOAP.
- Build a **recording decorator** that serialises every call and its return value to a JSON transcript.
- Run the live round trip **once** on the licensed machine, commit the transcript, and let CI replay it
  against a fake seam.
- Result: Tier 2 becomes runnable on every developer machine and in CI. The licence is needed to
  *capture*, not to *test*. Re-capture only when the vendor API or the adapter's call set changes.

**Trial-clock discipline.** When a 30-day trial is the only route (ETABS, RCB), do not install it
until the adapter is code-complete against the documentation. Install into a **VM with a snapshot**
taken before activation. Spend the trial on capture and verification, not on learning the API. This
turns a 30-day clock into roughly 30 usable days instead of five.

### Phase 4 — Native connectors, in access order

Build in whatever order procurement delivers, since Phase 0's conformance base makes them
interchangeable. If they arrive together, the docs' ordering still holds — **ETABS first**: closest
architectural relative, documented OAPI, and a native GUID field for the identity mapping. Robot and
Revit follow via ADN. RCB last, and only if the vendor conversation produces something.

## Files touched

- New: `Interop/*.cs` in `griffel-femex` (contract types), `Interop/Conformance/` (base class, diff
  utility, reference adapter), `Examples/Conformance1.femex`, a `griffel-femex.Adapters.Saf` project,
  `Claude/FEMEX_SAF_Mapping.md`.
- Modified: `griffel-femex.csproj` (multi-target, `System.Text.Json` on the `netstandard2.0` leg, the
  new `<None Include>` line for the conformance fixture), `griffel-femex.sln` (add `.Tests` and the
  adapter project), `griffel-femex.Tests/griffel-femex.Tests.csproj`.
- Reuse rather than reinvent: `FemexModel.GetOrAddNode` / `GetCoincidenceTolerance`
  (`FemexModel.Nodes.cs`), `TryGetBarLocalAxes` / `TryGetPlateLocalAxes` / `TryGetLoadDirection`
  (`FemexModel.LocalAxes.cs`), `AssignMissingUids` (`FemexModel.Identity.cs`), the self-weight helpers
  (`FemexModel.SelfWeight.cs`), `Validate(ValidationSeverity)` and `FileMetadata` for the producer
  stamp.

## Verification

- `dotnet build` and `dotnet test` green throughout; the suite grows from its current 254 facts rather
  than being restructured.
- Phase 0 is proven by the lossy reference adapter **failing** the conformance tests in exactly the
  ways it is designed to fail, and passing once its declarations match its behaviour. If the harness
  cannot tell a compliant plugin from a non-compliant one, it is not doing its job.
- Phase 1 is proven by loading every file in the SAF examples corpus, round-tripping
  SAF → FEMEX → SAF, and asserting every difference is named by a `TransferMessage` — plus the
  existing byte-identity assertion style on the FEMEX side.
- Phase 3 is proven by the recorded transcript replaying to an identical `TransferResult` on a machine
  with no vendor software installed.
- The multi-target change is proven by building the `netstandard2.0` leg and loading it into a `net48`
  console host, before any Revit add-in depends on it.

## Still open

- **Whether EPPlus 4.5.3.3's LGPL terms are acceptable for a commercial distribution**, and therefore
  whether the SAF adapter uses the official SDK or reads the worksheets directly. This should be
  settled before Phase 1 starts, not during it.
- **Whether SAF becomes a permanent intermediate or only a proving ground.** Using it as a permanent
  hub would inherit SAF's weaker plate model — the one place review §3.2 shows FEMEX is ahead. Phase 1
  produces the evidence; the decision should wait for it.
- **Whether RCB stays in scope.** With no API, no schema and no neutral format, the honest options are
  a DXF/Revit-Link detour or dropping it. The vendor conversation in Phase 2 decides.
- **Whether APS Design Automation is a real CI path for Revit or only a curiosity.** Headless cloud
  Revit with no desktop licence is attractive; whether the analytical model round-trips through it is
  untested.
- **Whether Phase 0 should still wait behind the nine untouched P1 entities** of review §5. This plan
  says no — the status note already argues that building nine more entities against documentation,
  before a single real file has been read, is how a format acquires the wrong vocabulary confidently.
- **Nothing here has been tested against a real exported file yet.** Every licence and pricing claim
  above is from vendor pages read in August 2026 and should be confirmed in writing before money moves.
