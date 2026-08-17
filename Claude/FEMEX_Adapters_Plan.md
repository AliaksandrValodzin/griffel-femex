# Plan — the FEMEX adapter contract

## Context

FEMEX is a JSON model format for building FE models plus a C# library
(`FemexModel` and its partials), a sample-model builder (`griffel-femex-models`)
and a single-file HTML viewer (`griffel-femex-viewer`). Nine design rounds have
produced a schema at v1.3 and 211 passing xUnit facts, and `Claude/FEMEX_Interop_Review.md`
has assessed it against Robot, Revit, ETABS, INDUCTA RCB and RFEM 6.
`Claude/FEMEX_Interop_Status_16082026.md` then measured the repository against
that review: four of six blocking gaps closed, sections and producer metadata
still open, all nine P1 items untouched.

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

**Where this sits against the status note's order.** That note puts five schema
items, one real ETABS or RFEM export and all nine P1 entities ahead of the first
connector — items 1–7 of its §5 queue — and this pass does not jump that queue: it produces no plugin and no schema change,
consumes none of the schema work, and is cheap to revise once a real file has
been read. What it does is stop the queue's item 8 from being the moment the
contract gets decided by accident. Where a rule below depends on a schema gap the
note lists as open — sections above all — the rule says so and names the item
that supersedes it.

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

- Exporters must tolerate nulls **by construction** — a plate with no
  `SurfacePropertyId` or `MaterialId` (`Geometry/Plate.cs:47,50`, both `int?`),
  zero load cases, an empty `LoadCombinations`, a `Level` with no `Name`. The
  naive shape (assume completeness, throw on null) is what you get if the first
  plugin is written before the contract.
- **The bar is the exception, and the contract must say so out loud.**
  `Bar.SectionId` and `Bar.MaterialId` (`Geometry/Bar.cs:32,35`) are
  non-nullable `int`, and `ValidateBars` (`FemexModel.Validation.cs`) reports an
  unresolvable one as an **Error** — which decision 4 says blocks. So a
  half-drawn bar carries `SectionId = 0` and is, today, un-exportable: the
  founding principle fails on the very first case anyone will meet. Since the
  schema change is out of scope here, the rule is that **no adapter ever leaves an
  unresolvable reference standing** — it synthesises a placeholder section and
  material and reports each as *Invented* (§4). The plate/bar asymmetry itself
  goes to §9 as a schema question, not an adapter one.
- **And that rule binds both directions, not just import.** The tempting phrasing
  — *an importer never writes an unresolvable reference* — is the wrong shape,
  because it misses the case decision 3 is written for. A user who models three
  levels in the viewer-turned-editor and hands off to ETABS has run no importer at
  all: the bars are FEMEX-native, `SectionId` is 0 because nobody has chosen a
  section yet, and it is the **exporter** that meets the Error. An import-only rule
  therefore leaves the founding principle failing on precisely its own example. So
  the rule is stated at the boundary rather than at one end of it: an importer does
  not write such a reference, and an exporter that reads one invents the placeholder
  itself rather than refusing.
- **The placeholder must be recognisable, not merely present**, because inventing it
  turns a loud failure into a quiet one. `TryGetBarSelfWeightPerLength`
  (`FemexModel.SelfWeight.cs:134`) returns false only when the section is *missing*;
  against a placeholder it **succeeds**, returning γ·A for whatever area the
  placeholder happens to have. A bar that was previously un-exportable becomes a bar
  carrying a confident, wrong self-weight. The contract therefore requires the
  invented section and material to be identifiable after the fact — by name, and by
  an *Invented* message anchored to them (§3, §4) — so a downstream consumer can tell
  a `Rectangle` somebody chose from a `Rectangle` the adapter made up to get past a
  validation gate.
- The error/warning gate is `Validate(ValidationSeverity)` (`FemexModel.Validation.cs:59`),
  already present and already severity-filterable. Argue that no adapter defines
  its own notion of "ready".
- The loss report is a return value, not a log line.

### Section 3 — The contract types

The core of the document. Proposed shapes, each with the alternative argued:

- `IFemexImporter` / `IFemexExporter` as separate interfaces, plus a capability
  declaration so a host can ask what a plugin supports before offering it.
- **What the capability declaration actually enumerates**, which cannot be left to
  the plugin author if §7's tier-1 test is to mean anything: a declaration with no
  fixed vocabulary is not checkable against what the plugin does. Argue for the
  natural axis being FEMEX's own root entity lists (`FemexModel.cs:78-105` —
  grids, levels, nodes, sections, surface properties, bars, plates, materials, load
  cases, loads, combinations, supports, hinges, mesh), declared per entity *and*
  per direction, since a program that can read plates but not write them is the
  common case rather than the exotic one.
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
- **The call shape — threading, cancellation and failure**, three decisions that
  are absent from the obvious design and ruinous to change once five plugins
  implement it. Revit's API may only be touched from its own main thread and ETABS'
  OAPI is no better, so whether the contract's methods are synchronous, `async`, or
  synchronous-but-marshalled is a decision the *host* has to live with. A whole-model
  transfer is minutes long, so a `CancellationToken` and a progress sink are either
  in the signature from the start or never. And the contract has to say what a native
  API failure does: throw, or return a `TransferResult` whose messages carry an
  Error. Argue for the latter — a plugin that throws gives the host no uniform
  behaviour, and the loss report already exists to carry exactly this kind of news.
  These belong here rather than in §8 precisely because they look like plumbing and
  are not.
- **Where the contract types live.** Argue for the core library (a new `Interop/`
  folder in `griffel-femex`) so every plugin takes exactly one dependency, versus
  a separate contract assembly.
- **Which runtime the contract targets — the hardest decision in the pass, and
  not a footnote to the one above.** `griffel-femex.csproj` targets **`net7.0`**,
  which is out of support and loadable by none of the .NET Framework hosts:
  Revit 2023 and 2024 are `net48`, Robot add-ins are COM against .NET Framework,
  and most ETABS OAPI clients are the same. Revit 2025+ is `net8.0`. "One
  dependency per plugin" is therefore impossible today for at least three of the
  five targets, whichever folder the types live in. The contract states the
  answer — multi-target `netstandard2.0;net8.0` for whatever assembly plugins
  reference — and flags it as the **one place this pass implies a build change**,
  to be made in a later pass rather than here.
- **That build change is about reach, and — as of schema 1.4 — about nothing else.**
  `JsonSerializerOptions.UnmappedMemberHandling` is System.Text.Json **8.0** API,
  and `griffel-femex.csproj` carries no `PackageReference` at all, so on `net7.0`
  that particular setting cannot be written; that much stands. This plan went on to
  claim the status note's item 1 sat behind the same retarget, **and that was
  wrong**: 1.4 closed it with `[JsonExtensionData]`, which needs no package, no SDK
  and no csproj change, and which preserves the unknown payload instead of refusing
  the file. §3's runtime question and §4's *Stale* loss are therefore two decisions,
  not one wearing two hats, and the retarget is now argued on reach alone.
- **What the `netstandard2.0` leg costs.** It is not free: `JsonPolymorphic` and
  `JsonDerivedType` (`Geometry/Sections/Section.cs`) and `IJsonOnDeserialized`
  (`FemexModel.SelfWeight.cs:27`) are all System.Text.Json 7+, which
  `netstandard2.0` does not carry, so that leg needs an explicit package reference —
  and shipping a System.Text.Json into Revit's `net48` AppDomain, beside whatever
  version Revit itself has already loaded, is an assembly-binding hazard. This,
  rather than the target framework on its own, is the real reason "one dependency
  per plugin" is hard, and the contract is more honest for saying it.

### Section 4 — The lossy-mapping taxonomy

One shared vocabulary, so five adapters report comparably:

- **Dropped** — FEMEX said something the target cannot express (plate region
  priority into a program with no region concept).
- **Approximated** — expressible, but not exactly (a finite `Restraint.Stiffness`
  into a fixed/free-only target; a curved edge polygonised). Note deliberately
  that **sections are not an example of this yet**: `Geometry/Sections/` is
  `Rectangle`, `Circle` and `TSection` with no profile designation and no numeric
  escape hatch, so a steel member crossing FEMEX today is *Dropped*, not
  approximated. Sections are the largest single loss channel in the taxonomy, and
  they stay that way until review §4.4 lands.
  > **Superseded by `Claude/FEMEX_StandardSections.md`:** review §4.4 landed as
  > schema 1.5 and 1.6 — the `SectionProperties` escape hatch and the `generic`
  > discriminator, then five parametric shapes and the `catalogue` block. Sections
  > are *Approximated* now, not *Dropped*, and `FEMEX_Adapters.md` §4.2 carries the
  > corrected ruling.
- **Invented** — the target requires something FEMEX does not say, so the adapter
  supplied a default. **The important category, and the one naive adapters never
  report**, because from inside the adapter an invention looks like a success.
- **Unmapped** — on import, a native concept with no FEMEX home (diaphragms,
  stiffness modifiers, pier/spandrel labels — the interop review's §5 list).
- **Stale** — the one category that is not about the native boundary at all, but
  about the FEMEX one. `FemexModel.cs:110-120` still does not set
  `UnmappedMemberHandling` — and, as §3 establishes, *cannot* while the library
  targets `net7.0` — so a file written by a newer schema used to lose its unknown
  members in silence when an older build read it: status §2.2's example is a
  1.3 file read by a 1.2 build dropping its uids without a word. **Schema 1.4 closed
  that without the retarget**, via `IExtensible` and `[JsonExtensionData]` on every
  serializable type: the members survive the read, are written back on save, and
  `Validate()` names them. It closes the loss class forwards only — a 1.2 build is
  already written — and an adapter is not a FEMEX build, so this stays an adapter
  concern and the reporting rule below still holds. Every adapter
  therefore declares the schema version it was built against, and reading a
  higher `SchemaVersion` is a reported loss rather than a shrug. Program-agnostic,
  cheap, and the only loss in the list that no per-program mapping document would
  ever catch.

### Section 5 — Identity, re-import and merge

The section with the most leverage, because `IIdentified` (`IIdentified.cs`)
already specifies the *intent* — *"Assigned by the exporting application, which
remembers the mapping to its own native handle — Revit's `UniqueId`, an ETABS
GUID, a Robot label"* — without specifying the mechanism. Settle:

- Who mints uids, and when. `AssignMissingUids()` (`FemexModel.Identity.cs:44`)
  mints random ones and never overwrites; argue whether an adapter should instead
  derive a uid deterministically from the native handle, which survives losing
  the side-table. If derivation is allowed, the contract must also say what
  happens when both mechanisms meet in one model: a random uid and a derived one
  are indistinguishable at read time, and `ValidateUidCoverage` only warns when
  coverage is *partial*, so a half-derived model passes silently. State the
  derivation (a namespaced v5 GUID over the native handle) and state that an
  adapter never re-derives over a uid that is already there — the same rule
  `AssignMissingUids` already follows.
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
- **Stable from *what*, though** — the rule is empty until it names its source, and
  every obvious candidate has a defect. `Section.Id` renumbers whenever a model is
  rebuilt, so a name derived from it is stable only within one authoring session.
  An ordinal is worse, since it moves when a list is reordered. `Uid` is the right
  answer *if one exists*, but `AssignMissingUids` (`FemexModel.Identity.cs:44`)
  mints `Guid.NewGuid()`, so on a model that has never met an adapter a uid-derived
  name is fresh random text on every export — which is exactly the instability the
  rule is trying to forbid. This is a genuine dependency between this bullet and
  the derivation question above, not a detail: derived uids make uid-keyed names
  work, random ones do not. Settle it here, or carry it to §9 explicitly rather
  than by omission.
- **And the rule covers more than the validator does.** Those four families are
  exactly what `ValidateNameKeys` (`FemexModel.Validation.cs`) checks, but
  `Level.Name` (`Geometry/Level.cs:26`) and `Plate.Name` (`Geometry/Plate.cs:36`)
  are `string?` too — and a *storey* is name-keyed in ETABS and Robot every bit as
  much as a section is. The contract's synthesis rule therefore applies to a
  superset of the validator's list; that the validator has not caught up is a
  note for §9, not a reason to write a narrower rule.

### Section 6 — Pre-decided hard mappings

Decisions that otherwise get made five times, five ways. Each stated as a rule
plus the helper that already implements it:

- **Level synthesis.** Every importer from a non-storey program must invent a
  `Level` for geometry that has no storey meaning — a truss diagonal, a raking
  column, a ramp. The interop review leaves this open; the contract closes it
  with one policy (snap to an existing level within tolerance, else create, always
  emit an *Invented* message) rather than five. **This rule is the exception to the
  section's own discipline: it names no existing helper because none exists.** The
  repository has `GetCoincidenceTolerance` (`FemexModel.Nodes.cs:34`), which is a
  *node* tolerance in three dimensions, and nothing at all for matching a
  `Level.AbsoluteElevation`. So either the rule adopts the node tolerance and says
  it is doing so, or level clustering is a second piece of implied code beside §7's
  model-diff utility — and the contract has to admit which, rather than gesturing at
  "tolerance" and leaving five plugins to pick their own.
- **Node sharing.** Importers go through `GetOrAddNode` (`FemexModel.Nodes.cs:101`)
  and `GetCoincidenceTolerance` (`:34`), never by trusting the native node list —
  otherwise connectivity is silently lost or silently invented, and FEMEX's unit
  of connectivity is the shared node.
- **But `GetOrAddNode` alone is not enough, because the tolerance moves while you
  import.** `GetCoincidenceTolerance` is `1e-6` of the model's *current* bounding
  diagonal (`FemexModel.Nodes.cs:66`), computed from the nodes already added. During
  an import that starts from an empty model it therefore begins at the `1e-9` floor
  and grows as the model fills: the first nodes are matched against a far tighter
  test than the last. Two nodes that `FindNodeAt` kept apart early can be coincident
  by the time the model is finished — `ValidateCoincidentNodes` will say so, after
  the fact, as a warning. The consequence for the contract is that the same native
  model read in a different order yields a different node table, and so does the
  same model read twice if the traversal is not deterministic. Both node and level
  synthesis are therefore **two-phase** — collect every candidate coordinate and
  elevation, cluster once against the finished extent, then create — rather than
  streamed one element at a time. This is the sort of rule that is obvious in
  hindsight and gets rediscovered separately by every plugin author.
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
- **And the larger self-weight trap is `Gravity.Acceleration`, which the
  double-counting rule does not touch.** It defaults to `9.80665`
  (`Gravity.cs:38`), and its own comment says the default is metre-specific and
  that "a millimetre model that accepts it is 1000x light". Every self-weight
  number in the library flows through it — `GetWeightDensity` is
  `Density * Gravity.Acceleration` (`FemexModel.SelfWeight.cs:119`) and both
  `TryGet…SelfWeight…` helpers multiply by that. So an importer from a program
  whose native units are millimetres, feet or inches that simply leaves the default
  alone produces a model that is wrong by three orders of magnitude, with nothing
  in `Validate()` to catch it, which is the same failure as the double-count only
  bigger and quieter. Rule: an adapter sets `Gravity.Acceleration` consistently with
  the unit system it declares below, and reports it as *Invented* whenever the
  native model did not state it — which is nearly always, since most programs carry
  gravity as a preference rather than as model data.
- **Units.** `Units` is unvalidated free text with no temperature, angle or mass
  unit — the word "units" does not appear in `FemexModel.Validation.cs` at all, so
  `"length": "banana"` round-trips clean. The adapter boundary is where units
  actually bite. The tempting rule, *refuse a model whose units you cannot read*,
  has to be rejected: refusing would be an adapter inventing its own notion of
  "ready" outside `Validate()`, which §2 forbids, and it would block exactly the
  half-drawn handoff decision 3 exists to protect. So the rule is that an adapter
  **proceeds on a declared assumption** and reports the assumed system as
  *Invented*. Review §7.2 item 8 (units as enums) supersedes this the day it
  lands, and the contract says so rather than pretending to be permanent.
- **And the contract names the assumption rather than leaving it per-adapter**,
  because "each adapter assumes something and says what" is the five-ways failure
  this whole section exists to prevent, only now with a paper trail. The choice is
  not open anyway: FEMEX has already made it implicitly, in `Gravity.Acceleration`'s
  metre-specific default and in `Examples/Example1.femex`'s `m`/`kN` header. So the
  assumed system is **metres and kilonewtons**, an adapter that assumes anything
  else says so in the message, and the *Invented* report stays either way.
- **A note on cost, not correctness.** Every helper this section mandates —
  `TryGetBarLocalAxes`, `TryGetLoadDirection`, `TryGetBarSelfWeightPerLength`,
  `GetWeightDensity` — resolves its references with a linear `List.Find` over
  `Bars`, `Sections`, `Materials` or `Nodes`. Reusing them is still right, and
  re-deriving the rules per plugin is still wrong; but calling them once per element
  across an imported model is quadratic, and an adapter over a large native model
  will feel it. The answer is an index built once at the boundary, not a second
  implementation of the rules — a note for whoever writes plugin #1, not a reason to
  weaken the rule.

### Section 7 — Testing the contract

The part that makes the rest enforceable — but only if it is split in two, because
half of it cannot run without the program installed:

- **The loss report is the test specification.** Round-trip FEMEX → native →
  FEMEX and assert that every difference between the two models is covered by a
  reported message. An undeclared difference is a bug; a declared one is the
  adapter working as designed. This makes "report your losses" mechanically
  checked rather than a matter of plugin-author diligence.
- **Which means "difference" has to be defined here, in the contract, and not left
  to the diff utility.** As stated the rule is unfalsifiable: a *clean* round-trip
  through any real program differs in ways no adapter should have to report. Ids
  and node numbers are renumbered by the native program and renumbered again on the
  way back; list order is not preserved by anything; coordinates come back through
  the native program's own precision. Deferring the *utility* to a later pass is
  right — deferring the *equivalence definition* means that pass decides it while
  writing a comparison loop, which is precisely the by-accident decision this whole
  document exists to prevent. So the contract states it: objects are matched by
  `Uid` and never by `Id`, lists compare as sets under that key, geometry compares
  within `GetCoincidenceTolerance`, and anything left over is a difference that must
  be named by a message. That definition is also what makes §5's identity rules
  load-bearing rather than decorative — a model whose uid coverage is partial cannot
  be round-trip-tested at all, which is worth saying out loud.
- **Tier 1, offline, runs everywhere.** Nothing here touches a native API, so it
  runs on any machine and in CI: null tolerance across every nullable reference,
  every message anchored to an object rather than free text, name synthesis
  producing the *same* name on a second export, the capability declaration
  matching what the plugin actually does, and no adapter-defined readiness gate
  beside `Validate()`.
- **Tier 2, live, gated on the program.** The FEMEX → native → FEMEX round-trip
  needs Robot's COM server, the ETABS OAPI or an RFEM endpoint — installed and
  licensed. Stating this as a separate tier is what stops the suite being skipped
  wholesale on the machines that do not have them, which is the failure mode a
  single undifferentiated suite guarantees.
- A **conformance test base class** each adapter inherits, so adapter #5 gets both
  tiers for free and cannot quietly skip a rule.
- **And a reference adapter to run it against**, because a base class with no
  implementation in this repository is a suite that has never been shown to fail.
  Tier 1 needs one in-memory fake — FEMEX in, FEMEX out, no native API — written
  deliberately *lossy*: dropping something it declares, inventing a section and a
  unit system, and leaving one native concept unmapped, so that each taxonomy
  category in §4 is exercised by something. Its job is not to be a useful adapter
  but to prove the conformance tests can distinguish a compliant plugin from a
  non-compliant one before any real plugin depends on that distinction. Like the
  diff utility, it is code and therefore a follow-up pass — but naming it here is
  what stops tier 1 shipping as an untested assertion.
- Requires a **model-diff utility** implementing the equivalence defined above —
  uid-keyed matching, order-insensitive list comparison, tolerance on geometry.
  Together with the reference adapter and the level-clustering helper §6 needs,
  these are the three pieces of code this design implies, and all three are a
  **follow-up pass, not this one**. Note the existing round-trip assertions in
  `griffel-femex.Tests/RoundTripTests.cs` and `RoundTripIdentityTests.cs` as the
  style to follow.
- **The golden model is a file, not a call.** `SampleModel.Build()` already
  exercises grids, plates, regions, combinations, self-weight and identity, but it
  lives in `griffel-femex-models`, which consumes this library as a *prebuilt
  DLL* (`<Reference>` plus `lib\griffel-femex.dll`, refreshed by
  `UpdateFemexDll.ps1`) — so a conformance base class shipping beside the contract
  types cannot reference it without inverting that dependency. Serialise it once
  to a `.femex` file in this repository instead, following `Examples/Example1.femex`.
  A fixed file is also the better baseline on its own merits: `Build()` changes
  under the suite every time the sample grows.

### Section 8 — Deliberately out of scope

So the contract is not read as a plugin to-do: packaging and installers, plugin
UI, licensing and obfuscation, per-program API version pinning, and the actual
per-program mappings — each of which is its own later document.

### Section 9 — Still open

House style. Expected to include: whether the mapping store can be required at
all for programs with no document-attached storage; **whether a host may offer a
plugin that declares only one direction** — §3 already separates the interfaces, so
the live question is not whether a read-only adapter can be *written* but whether
the hub is willing to present one, given that a program you can import from but
never export to breaks the round-trip §7 tests everything against; **what a
synthesised name is derived from** when §5's stability rule meets a model whose
uids were minted randomly; **whether `Bar.SectionId` and `Bar.MaterialId` should become `int?` like
their `Plate` counterparts**, which would let a half-drawn bar be honest instead
of forcing an invented placeholder; **whether a contract may imply a build
change**, since the multi-targeting §3 needs is the first time a document-only
pass has reached the csproj — sharpened by the finding that `UnmappedMemberHandling`
is unreachable without it, so the retarget is a schema prerequisite and not only an
adapter convenience; **whether a plugin built against 1.3 may read a 1.4
file at all**, or must decline until `UnmappedMemberHandling` is set; whether
`ValidateNameKeys` should widen to the families §5's synthesis rule covers; and
the fact that, like the interop review before it, **nothing here has been tested
against a real exported file** — the contract is a hypothesis until plugin #1
either confirms it or breaks it.

## Critical files

Read before writing, all as evidence rather than for modification:

- `FemexModel.cs` — root shape, `JsonOptions`, `ToJson`/`FromJson`, version stamping,
  and at `:110-120` the `UnmappedMemberHandling` that is still not set (§4 *Stale*).
- `Geometry/Bar.cs:32,35` and `Geometry/Plate.cs:47,50` — the non-nullable bar
  references against the nullable plate ones, which §2's exception turns on.
- `griffel-femex.csproj` — the `net7.0` target §3 has to answer for, and the
  absence of any `PackageReference`, which is why `UnmappedMemberHandling` is
  unreachable rather than merely unset. Since 1.4 that is a limit on the *strictness*
  available, not on silent loss: see `IExtensible.cs` and `FemexModel.Unknown.cs`.
- `FemexModel.Validation.cs:21,59` — `Validate()` and the severity-filtered overload.
- `ValidationSeverity.cs`, `ValidationMessage.cs` — the error/warning discipline the contract inherits.
- `IIdentified.cs`, `FemexModel.Identity.cs:44` — uid intent and `AssignMissingUids`.
- `FemexModel.Nodes.cs:34,66,101` — coincidence tolerance, the extent-scaled formula
  behind §6's two-phase rule, and `GetOrAddNode`.
- `FemexModel.LocalAxes.cs:38,86,113` — the three resolvers exporters must reuse.
- `FemexModel.SelfWeight.cs:27,107,119,134,161,230` — `IJsonOnDeserialized` (§3's
  netstandard cost), gravity direction, `GetWeightDensity`, and self-weight
  materialisation.
- `Gravity.cs:26,38`, `Geometry/Vector3d.cs` — the Z-up statement, the RFEM sign
  trap, and the metre-specific `Acceleration` default §6 has to close.
- `Units.cs` — two nullable strings, no temperature, angle or mass, validated nowhere.
- `Geometry/Sections/Section.cs` — the three-shape union behind §4's *Dropped*
  ruling, and the `JsonPolymorphic` attributes §3 costs the `netstandard2.0` leg for.
- `Claude/FEMEX_Interop_Review.md` §4, §5, §7.3 — the gap and open-question inventory this contract must not contradict.
- `Claude/FEMEX_Interop_Status_16082026.md` §2, §4, §5 — what has actually landed
  since the review, and the recommended order this pass has to place itself against.

**Nothing in this list is modified.** The pass creates `Claude/FEMEX_Adapters.md` only.

## Verification

Document-only, so verification is review-based, mirroring `FEMEX_Assessment.md`:

1. Every claim about FEMEX cites a file and type in this repository, checked
   directly rather than recalled from the design docs — several of which are
   explicitly superseded in part and would otherwise be quoted wrongly.
2. Every rule in §6 names the existing helper that implements it, so the contract
   cannot silently propose re-implementing something already written — and where no
   such helper exists (level clustering), the rule says so explicitly rather than
   gesturing at one, so the implied code is counted rather than smuggled in.
3. Any claim about an external program either cites the interop review (which
   carries its own URLs) or a new source, and anything reconstructed rather than
   documented is labelled as such.
4. Every rule that leans on a schema gap the status note lists as open — sections,
   units, `UnmappedMemberHandling` — names the review item that supersedes it, so
   the contract cannot quietly harden a workaround into a permanent rule.
5. `dotnet build` and `dotnet test` run once at the end, confirming no code or
   csproj change and still green at **211 facts** — the pass adds
   `Claude/FEMEX_Adapters.md` and nothing else, and in particular does not perform
   the multi-targeting §3 recommends.
