# Plan — the FEMEX adapter contract

## Context

FEMEX is a JSON model format for building FE models plus a C# library
(`FemexModel` and its partials), a sample-model builder (`griffel-femex-models`)
and a single-file HTML viewer (`griffel-femex-viewer`). Nine design rounds have
produced a schema at v1.3 and 211 passing xUnit facts, and `Claude/FEMEX_Interop_Review.md`
has assessed it against Robot, Revit, ETABS, INDUCTA RCB and RFEM 6.

**The gap this pass addresses.** Every FEMEX decision so far has been made from
first principles against vendor documentation. Not one line of adapter code
exists in any of the three repos, and the interop review says so plainly:
*"Nothing here has been tested against a real exported file."* The container is
finished to a high standard; the translation — where the difficulty and the value
live — has not started.

The confirmed direction is a **plugin hub**: a separate import/export plugin per
program, with FEMEX as the pivot, so N adapters replace N² translators and the
viewer grows into an editor at the centre. Before the first plugin is written,
the shared contract every plugin implements needs to be settled — because it is
now carrying real decisions, not just plumbing, and because decisions made
implicitly inside plugin #1 become five inconsistent decisions by plugin #5.

**Deliverable: a written contract document only. No code, no schema changes in
this pass.** Program-agnostic — the mapping for any specific program is a later
document.

## Decisions locked in

1. **Deliverable is the document**, following the house style of `Claude/`:
   argued rather than asserted, every FEMEX claim citing a file and type in the
   repo, closing with an explicit "Still open" section.
2. **Program-agnostic.** No program is chosen as first target in this pass. The
   document may cite programs as evidence for a rule, never assume one.
3. **A half-drawn model is exportable.** Export is a handoff, not a certificate
   of analysability. A user who models three levels here and wants to finish the
   frame in ETABS must not be blocked. This is the pass's founding principle and
   several rules below follow from it.
4. **Errors block, warnings never do.** `ValidationSeverity` (`ValidationSeverity.cs`)
   already draws exactly the right line — *incomplete* (missing data, honest,
   exportable) versus *invalid* (a bar referencing a node that does not resolve,
   genuinely untranslatable). The contract honours it rather than inventing a
   second gate.
5. **Every transfer yields a model *and* a loss report.** This is what makes
   "any-software-in / any-software-out" a property rather than a claim.

## Document to produce

**`Claude/FEMEX_Adapters.md`** — the contract itself, structured as below,
alongside this plan (matching the `FEMEX_Assessment.md` → `FEMEX_Interop_Review.md`
precedent for a document-only pass).

### Section 1 — What an adapter is

Hub-and-spoke stated explicitly: FEMEX is the pivot, the adapter is the *only*
place program-specific knowledge is permitted to live, and the pivot's job is the
intersection every program can round-trip, not the union of what any one can
express. Two directions (native → FEMEX, FEMEX → native), which are not
symmetric and must not share one interface by force.

### Section 2 — The three principles

Decisions 3–5 above, argued rather than asserted, each with its consequence:

- Exporters must tolerate nulls **by construction** — a bar with no section, a
  plate with no `SurfacePropertyId`, zero load cases, an empty `LoadCombinations`.
  The naive shape (assume completeness, throw on null) is what you get if the
  first plugin is written before the contract.
- The error/warning gate is `Validate(ValidationSeverity)` (`FemexModel.Validation.cs:59`),
  already present and already severity-filterable. Argue that no adapter defines
  its own notion of "ready".
- The loss report is a return value, not a log line.

### Section 3 — The contract types

The core of the document. Proposed shapes, each with the alternative argued:

- `IFemexImporter` / `IFemexExporter` as separate interfaces, plus a capability
  declaration so a host can ask what a plugin supports before offering it.
- A `TransferResult` carrying the model *and* the messages, rather than a bare
  `FemexModel` with messages on the side.
- **Whether transfer messages reuse `ValidationMessage`** or get their own type.
  Argue for a distinct type with the same two-severity discipline plus a category
  (§4): validation says *this model is wrong*, a transfer message says *this
  crossing was lossy*, and conflating them means an adapter's honest report of an
  approximation reads as a defect in the model.
- **Message anchoring.** A message must be able to name the object it concerns.
  `FEMEX_Identity.md` added `Load.Id` for precisely this reason — *"it exists so
  a load can be named in a message"* — so the contract should not throw that
  away by reporting losses as free text.
- **Where the contract types live.** Argue for the core library (a new `Interop/`
  folder in `griffel-femex`) so every plugin takes exactly one dependency, versus
  a separate contract assembly. Note the constraint that plugins are loaded by
  five different hosts with five different .NET version ceilings.

### Section 4 — The lossy-mapping taxonomy

One shared vocabulary, so five adapters report comparably:

- **Dropped** — FEMEX said something the target cannot express (plate region
  priority into a program with no region concept).
- **Approximated** — expressible, but not exactly (a `TSection` resolved to a
  catalogue entry; a finite `Restraint.Stiffness` into a fixed/free-only target).
- **Invented** — the target requires something FEMEX does not say, so the adapter
  supplied a default. **The important category, and the one naive adapters never
  report**, because from inside the adapter an invention looks like a success.
- **Unmapped** — on import, a native concept with no FEMEX home (diaphragms,
  stiffness modifiers, pier/spandrel labels — the interop review's §5 list).

### Section 5 — Identity, re-import and merge

The section with the most leverage, because `IIdentified` (`IIdentified.cs`)
already specifies the *intent* — *"Assigned by the exporting application, which
remembers the mapping to its own native handle — Revit's `UniqueId`, an ETABS
GUID, a Robot label"* — without specifying the mechanism. Settle:

- Who mints uids, and when. `AssignMissingUids()` (`FemexModel.Identity.cs:44`)
  mints random ones and never overwrites; argue whether an adapter should instead
  derive a uid deterministically from the native handle, which survives losing
  the side-table.
- **Where the uid ↔ native-handle mapping is stored** — in the native document
  (Revit extensible storage), in a sidecar file, or nowhere because it is
  derivable. Different programs allow different answers; the contract states
  which are acceptable, not which one everybody uses.
- **Name-keyed targets.** Robot properties and ETABS sections/stories key by
  *name*, and Robot's `StoreWithName` silently overwrites. `Section.Name`,
  `SurfaceProperty.Name`, `Material.Name` and `LoadCase.Label` are all `string?`.
  So an exporter must synthesise names when they are null, and must do so
  **stably**, or a second export produces `Section1_2` and a third `Section1_2_2`.
  Name synthesis is a contract rule, not a per-plugin habit.

### Section 6 — Pre-decided hard mappings

Decisions that otherwise get made five times, five ways. Each stated as a rule
plus the helper that already implements it:

- **Level synthesis.** Every importer from a non-storey program must invent a
  `Level` for geometry that has no storey meaning — a truss diagonal, a raking
  column, a ramp. The interop review leaves this open; the contract closes it
  with one policy (snap to an existing level within tolerance, else create, always
  emit an *Invented* message) rather than five.
- **Node sharing.** Importers go through `GetOrAddNode` (`FemexModel.Nodes.cs:101`)
  and `GetCoincidenceTolerance` (`:34`), never by trusting the native node list —
  otherwise connectivity is silently lost or silently invented, and FEMEX's unit
  of connectivity is the shared node.
- **Axis and sign normalisation.** RFEM's global Z points down by default
  (`Gravity.cs`, `Geometry/Vector3d.cs` both flag it). The contract states that
  normalising to right-handed Z-up is the adapter's job at the boundary, never
  the caller's afterwards.
- **Load direction.** Exporters resolve through `TryGetLoadDirection`
  (`FemexModel.LocalAxes.cs:113`), and bar/plate axes through
  `TryGetBarLocalAxes` / `TryGetPlateLocalAxes` — never re-derived per plugin,
  since the ETABS/SAP convention and the vertical-member substitution are
  already encoded there.
- **Self-weight double-counting.** `LoadCase.SelfWeightFactor` versus a target
  that applies self-weight from its own flag. Rule: set the native flag *or*
  materialise loads via `TryGetBarSelfWeightPerLength` /
  `TryGetPlateSelfWeightPerArea` (`FemexModel.SelfWeight.cs`), never both, and
  report which was done. The interop review names this as producing a
  confidently wrong answer rather than an obviously incomplete one.
- **Units.** `Units` is unvalidated free text with no temperature, angle or mass
  unit. The adapter boundary is where units actually bite. State whether adapters
  must normalise to a declared system, or refuse a model whose units they cannot
  read.

### Section 7 — Testing the contract

Program-agnostic, and the part that makes the rest enforceable:

- **The loss report is the test specification.** Round-trip FEMEX → native →
  FEMEX and assert that every difference between the two models is covered by a
  reported message. An undeclared difference is a bug; a declared one is the
  adapter working as designed. This makes "report your losses" mechanically
  checked rather than a matter of plugin-author diligence.
- Requires a **model-diff utility** (new, and the one piece of code this design
  implies) with order-insensitive list comparison — note the existing round-trip
  assertions in `griffel-femex.Tests/RoundTripTests.cs` and
  `RoundTripIdentityTests.cs` as the style to follow.
- A **conformance test base class** each adapter inherits, so adapter #5 gets the
  suite for free and cannot quietly skip a rule.
- `SampleModel.Build()` in `griffel-femex-models/SampleModel.cs` (30 KB, already
  exercising grids, plates, regions, combinations, self-weight and identity) as
  the golden model, rather than a new fixture.

### Section 8 — Deliberately out of scope

So the contract is not read as a plugin to-do: packaging and installers, plugin
UI, licensing and obfuscation, per-program API version pinning, and the actual
per-program mappings — each of which is its own later document.

### Section 9 — Still open

House style. Expected to include: whether the mapping store can be required at
all for programs with no document-attached storage; whether import and export
should be allowed to be implemented independently (a read-only adapter is
useful); and the fact that, like the interop review before it, **nothing here has
been tested against a real exported file** — the contract is a hypothesis until
plugin #1 either confirms it or breaks it.

## Critical files

Read before writing, all as evidence rather than for modification:

- `FemexModel.cs` — root shape, `JsonOptions`, `ToJson`/`FromJson`, version stamping.
- `FemexModel.Validation.cs:21,59` — `Validate()` and the severity-filtered overload.
- `ValidationSeverity.cs`, `ValidationMessage.cs` — the error/warning discipline the contract inherits.
- `IIdentified.cs`, `FemexModel.Identity.cs:44` — uid intent and `AssignMissingUids`.
- `FemexModel.Nodes.cs:34,101` — coincidence tolerance and `GetOrAddNode`.
- `FemexModel.LocalAxes.cs:38,86,113` — the three resolvers exporters must reuse.
- `FemexModel.SelfWeight.cs:107,134,161,230` — gravity direction and self-weight materialisation.
- `Gravity.cs`, `Geometry/Vector3d.cs` — the Z-up statement and the RFEM sign trap.
- `Claude/FEMEX_Interop_Review.md` §4, §5, §7.3 — the gap and open-question inventory this contract must not contradict.

**Nothing in this list is modified.** The pass creates `Claude/FEMEX_Adapters.md` only.

## Verification

Document-only, so verification is review-based, mirroring `FEMEX_Assessment.md`:

1. Every claim about FEMEX cites a file and type in this repository, checked
   directly rather than recalled from the design docs — several of which are
   explicitly superseded in part and would otherwise be quoted wrongly.
2. Every rule in §6 names the existing helper that implements it, so the contract
   cannot silently propose re-implementing something already written.
3. Any claim about an external program either cites the interop review (which
   carries its own URLs) or a new source, and anything reconstructed rather than
   documented is labelled as such.
4. `dotnet build` and `dotnet test` run once at the end, confirming the repo is
   untouched and still green at **211 facts**, since this pass changes no code.
