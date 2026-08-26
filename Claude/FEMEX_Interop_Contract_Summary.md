# FEMEX interop contract — Phase A, as built

Implementation record for **Phase A** of `Claude/AdaptersPlans/SAF_Adapter.md`: the contract types
of `FEMEX_Adapters.md` §3, the model diff of §7.2, the level-clustering helper of §6.1, the
conformance harness of §7.3 and the lossy reference adapter of §7.5 — plus the multi-target the
whole thing sits on, and the validation parity harness that keeps the free checker and the paid
report saying the same thing.

Written 26 August 2026. Nothing here is licence-gated or web-gated; nothing here touches SAF.

---

## 0. What landed, in one screen

| | |
|---|---|
| **Target frameworks** | `netstandard2.0;net8.0`, both legs building clean, **proven loading into a real `net48` host** |
| **New public surface** | `Interop/` (11 contract types + `NameSynthesis`), `Comparison/` (the diff), `Synthesis/` (two-phase node and level clustering), `Interop/Conformance/` (harness, transport, reference adapter) |
| **Made public** | `FemexModel.EnumerateIdentified()`, now also yielding the `ObjectRef` a message anchors to |
| **New fixtures** | `Examples/Conformance1.femex` (98 objects, **full uid coverage**), `Examples/Parity1.femex` (deliberately defective), five `*.expected.json` artefacts |
| **Tests** | **324 → 390**, all green, and `dotnet test` at the repo root now finds them |
| **Cross-repo** | `griffel-femex-models` retargeted to `net8.0`; `griffel-femex-viewer` gained `parity-check.ps1`, `parity-subset.json` and a written parity rule — **`femex-viewer.html` untouched** |

Three things the plan predicted and one it did not: the multi-target did end the zero-package
record, the tests project did silently need retargeting, `UpdateFemexDll.ps1` did break — and the
`netstandard2.0` leg needed **two source changes** the plan did not list, both recorded in §1.

---

## 1. A1 — the multi-target, and what the `netstandard2.0` leg actually cost

`.NET 8 SDK 8.0.424` installed; `griffel-femex.csproj` is now
`<TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>`.

**The three consequences the plan named, all confirmed:**

- `System.Text.Json` **8.0.5** is a `PackageReference` on the `netstandard2.0` leg only — the
  `net8.0` leg has it in the shared framework and takes no reference. The zero-package record is
  over, deliberately.
- `griffel-femex.Tests` moved `net7.0` → `net8.0`. Left alone it would have silently tested the
  wrong leg.
- `..\griffel-femex-models\UpdateFemexDll.ps1` had `net7.0` hardcoded as a parameter default.
  Updated — **and so was `griffel-femex-models.csproj` itself**, which the plan did not mention:
  the script copies whichever leg it is told to, and a `net7.0` consumer cannot reference a
  `net8.0` assembly. Both moved to `net8.0`; the project builds and the DLL refresh works.

**Two source changes the plan did not anticipate**, both consequences of `netstandard2.0` being a
C# 7.3 target with an unannotated BCL:

- **`Compat/IsExternalInit.cs`.** `Geometry/Vector3d.cs`'s `readonly record struct` has init-only
  members, and the marker the compiler emits for one does not exist before .NET 5. Declared
  `internal`, guarded by `#if NETSTANDARD2_0`. `<LangVersion>latest</LangVersion>` is set for the
  same family of reasons — the SDK would otherwise default that leg to C# 7.3, which has neither
  nullable reference types nor record structs.
- **`list[^1]` → `list[list.Count - 1]`** in `FemexModel.Validation.cs` (two sites), and three
  `!` null-forgiving operators where `string.IsNullOrWhiteSpace` carries no `[NotNullWhen(false)]`
  on that leg. Each is commented with the reason. No behaviour changed; both legs build with **0
  warnings**.

**And one documentation correction the retarget forces.** `FemexModel.cs` and `IExtensible.cs` both
asserted that `UnmappedMemberHandling` "cannot be set" because the project targets `net7.0`. That is
now false — STJ 8 is reachable on both legs. The setting stays unset, and the comments now say so
*by choice rather than by the framework*: it refuses the file, where extension data preserves the
payload and reports it.

---

## 2. A3, A4 — the contract types

`Interop/`, transcribed from §3.1–§3.5 with two deviations, both argued below.

`IFemexAdapter` · `IFemexImporter` · `IFemexExporter` · `TransferResult<T>` · `TransferMessage` ·
`ObjectRef` · `LossCategory` · `FemexEntity` · `TransferDirection` · `AdapterCapabilities` ·
`AdapterInfo` — and the four types §3.1 names but never defines: `ImportRequest` /
`StreamImportRequest`, `ExportRequest` / `StreamExportRequest`, `ExportReceipt`,
`TransferProgress`.

**The request seam went the way the plan predicted, for the reason it gave.** Requests are abstract
and carry no vendor type, so a Revit adapter subclasses `ImportRequest` in *its own* assembly and
the core never references a vendor assembly; the stream pair is the file case, which is what SAF's
stream-based SDK will use. `ExportReceipt` carries the §5.3 uid ↔ native-handle mapping **as data**,
because a batch run over forty models has nowhere sensible to scatter forty sidecars.

### Deviation 1 — `TransferMessage.Subject` is nullable

§3.5 writes it as a bare `ObjectRef`. It is `ObjectRef?`, and the deviation is forced by two of the
contract's own rules rather than chosen for convenience:

- §3.3 keeps `Units` and `Gravity` out of `FemexEntity` **deliberately**, so that no adapter can
  declare its way out of §6.5 and §6.6.
- §6.5, §6.6 and §4.5 then *require* an adapter to report exactly those three — an assumed unit
  system, an invented gravity, a stale schema — as losses.

A non-nullable struct makes each of those claim to be about grid 0, which is worse than saying
nothing. So the case §3.5 argues still cannot go unanchored by accident: **`Loss` takes a
non-nullable subject**, and the only door to a null one is `ModelLoss`, named for what it is and
documented as being for those three facts and nothing else. §4.4's per-concept report — "142 members
carried stiffness modifiers" — stays anchored, to the entity kind with a null `Id`, which is the
shape `ObjectRef` already had.

### Deviation 2 — `FemexEntity` has no `PlateRegion`

Transcribed verbatim, which means a region anchors to `FemexEntity.Plate` carrying the region's own
id and uid. The uid tells a region from its plate, which is what §7.2 matches on anyway. Recorded in
*Still open* below rather than fixed, because §3.3 fixes the vocabulary to the root lists on purpose
and widening it is a decision, not a detail.

### `EnumerateIdentified()` is public, and yields three things

§5.2 called it "the one walk over every `IIdentified` declaration site" and noted that leaving it
private means thirteen sites hand-listed in at least two more places, going stale independently. It
is now `public` and yields `(IIdentified Entity, ObjectRef Ref, string Owner)` — one statement, three
consumers: `AssignMissingUids`, `Validate()`, and now the diff and the anchoring test. A second
enumeration beside it would have been the drift it exists to prevent.

### `NameSynthesis` — §5.4 and §5.5, implemented once

`{Kind}-{first 8 hex of the uid}` — `Section-3f9a2c14` — over **six** families, not the validator's
four: `Section`, `SurfaceProperty`, `Material`, `LoadCase`, **`Level`** and **`Plate`**, because a
storey is name-keyed in ETABS and Robot every bit as much as a section is. `Apply()` calls
`AssignMissingUids()` first (§5.4's rule, and it mutates the model on purpose — an exporter that
synthesises names without stamping is deriving a name from nothing) and returns one *Invented*
message per name filled.

---

## 3. A2 — the model diff

`Comparison/`, in a **product namespace** rather than under `Interop/Conformance/`, because
`FEMEX_BusinessModel.md` §7 is right that it is a product surface (Claim 2) and promoting it later
should not be a move.

**The architecture is exceptions-first, and that is the load-bearing decision.** The default for any
member is to compare its *serialized form*, which is total: a member added by 1.9 is compared the
day it is added, with no table to remember to update. The tables in `MemberComparer` hold only the
members for which serialized equality is the *wrong* answer:

| Table | What it holds | Why |
|---|---|---|
| `Skipped` | the 13 keys, the uids, `Plate.Regions`, a node's three coordinates | §7.2 matches on uid and never on id, so an id is *allowed* to differ |
| `Geometric` | 14 coordinate members | §7.2 grants `GetCoincidenceTolerance` to geometry and to nothing else |
| `References` | **29 integer members that name another object** | `Bar.StartNodeId` is an integer that names a node; comparing two of them across a crossing compares two numbering schemes, not two structures |
| `KeyedLists` | `Grid.Lines` (by label), `LoadCombination.Terms` (by resolved case) | sub-objects with no uid but a natural key |

A diff built the other way round — comparing only what a table lists — silently stops covering the
format the first time the format grows. This one cannot.

**Reference resolution is what makes §7.2 executable.** `EntityIndex` resolves each id to the uid of
what it points at, with four distinguishable answers: a uid, `?` (resolved to something with no uid,
so not comparable — *not* the same as equal), `missing:N` (dangling, a real fact about the model),
and `none`. Region references resolve within their sibling plate id.

**A node's geometry is where it is**, not the three numbers that say so: nodes compare on their
absolute point, and their level compares as the reference it also is. That is what lets an importer
re-express the same structure over synthesised storeys without every node reading as moved.

**Uid coverage is loud.** §7.2 says a model with partial coverage "cannot be round-trip-tested at
all". Rather than compare clean and say nothing, the diff emits `DifferenceKind.Unkeyed`, one per
entity kind per side with a count.

**What is deliberately absent:** cross-program matching heuristics. §7.2 needs none, B4 gives SAF
full coverage via uid ↔ Name, and the geometric-and-topological matching Claim 2 needs is, in the
business model's own words, the largest piece of new engineering in the plan and wants its own
document. Absent rather than half-built.

---

## 4. A2b — two-phase node and level synthesis

`Synthesis/GeometrySynthesis`, the helper §6.1 mandated and then admitted did not exist.

Collect every candidate point and elevation; **cluster once against the finished extent**; then
create. The bug this removes is the one §6.2 describes and that every plugin author would otherwise
rediscover: `GetCoincidenceTolerance` is 1e-6 of the model's *current* diagonal, so an import from an
empty model starts at the 1e-9 floor and grows as it fills — the first nodes matched against a far
tighter test than the last, and the same native model read in a different order yielding a different
node table.

Three properties worth stating:

- **Order-independence is engineered, not hoped for.** Candidates are clustered in a canonical order
  of their own — sorted by coordinate, not by arrival — so node *numbering* is a function of the
  geometry alone. `SamePoints_InAnyOrder_GiveTheSameNodeTable` and
  `TheTolerance_IsDerivedOnce_NotAsTheModelGrows` are that made executable.
- **The tolerance is a shape, not a number.** Relative to the model's own vertical extent for levels
  and its bounding diagonal for nodes, floored at 1e-9, per §6.1 — never an absolute millimetre,
  which means something different in a metre model and a millimetre one. `SynthesisOptions` lets a
  caller who has measured a real program override it and own the consequence. **The number is still
  unfixed**; nothing in the SAF corpus bears on it, because SAF has no storeys.
- **§6.1's other half is not optional.** `SynthesisResult.Messages` arrives with the *Invented*
  messages already written, one per level the native model did not have. An adapter cannot forget
  them by not thinking of them.

Building twice throws, because a second pass would cluster against a different extent — the exact
order-dependence the class exists to remove.

---

## 5. A5, A6, A7 — the fixture, the reference adapter, the harness

### `Examples/Conformance1.femex` (A5)

`SampleModel.Build()` serialised once, with `AssignMissingUids()` applied: **98 objects, every one
carrying a uid**, which §7.2 makes a precondition of the suite rather than a nicety. A fixed file is
the better baseline on its own merits — `Build()` changes under the suite every time the sample
grows, and a baseline that moves is not one. The fourth `<None Include>` line is in the test csproj;
the glob is still deliberately absent.

### `ReferenceAdapter` (A6)

FEMEX in, FEMEX out, over a `ReferenceDocument` invented for the purpose and **deliberately
impoverished**, each limitation forcing one §4 category into the open:

| Limitation | Category |
|---|---|
| no region model on a panel | *Dropped*, per region |
| supports are six booleans — no stiffness, no sense | *Approximated*, per support |
| a section is a name and an area | *Approximated*, per concept |
| mandatory unit system and gravity | *Invented*, model-level |
| a per-member stiffness modifier FEMEX has no noun for | *Unmapped*, per concept |
| no storeys at all | *Invented*, per synthesised level |
| its `AdapterInfo` schema version | *Stale*, model-level |

It obeys every §6 rule it can: two-phase synthesis, `NameSynthesis`, §2.2's recognisable placeholder
(area **zero**, so `TryGetBarSelfWeightPerLength` cannot return a confident wrong answer against it),
no second gate, and a corrupt file that **returns rather than throws**. It calls
`AssignMissingUids()` on import and reports how many it minted, because a silent mint is a false
provenance claim.

### `ConformanceHarness` (A7) — and the seventh check

A **framework-agnostic** base class in the core library — it returns `ConformanceCheck` results and
has no test-framework dependency, so nothing test-shaped travels with the shipped assembly. One
entry point, `RunTier1()`, deliberately: an adapter cannot skip a rule by not writing its test,
because there is no per-rule test to leave unwritten.

Six checks are §7.3's. The seventh is **§7.1 itself** — round-trip the golden model and assert that
*every* difference is covered by a reported message. It is here, in Tier 1, only because the
reference adapter is offline; for a live adapter it is Tier 2. What counts as covered is stated
rather than left to the comparison loop: a message whose stated keys agree with the difference's, or
one anchored to the entity kind with no id (§4.4's per-concept report), or — for a difference about
the model — any message about the model.

**A `Skip` is not a `Pass`.** `ConformanceCheck` has three states, because a suite that quietly
reports green for what it never ran is the failure the two tiers exist to prevent. `§6.2`'s check
skips honestly when a transport cannot reorder its source.

`ConformanceTransport` is the seam that keeps this usable by a live adapter: the harness never
assumes a stream, because `ImportRequest` is abstract precisely so Revit can subclass it.
`ReferenceTransport` is the worked example, and its `TryBeginReorderedImport` is the only part that
takes thought.

### The harness was shown to fail

Seven adapters in `BrokenAdapters.cs`, each the smallest deviation that breaks one rule, and each a
mistake somebody will make for real:

| Adapter | Breaks | Caught by |
|---|---|---|
| `BrittleAdapter` | dereferences `Units` | Null tolerance |
| `FussyAdapter` | refuses a model with no sections | No second gate |
| `MisanchoringAdapter` | reports a loss against a bar that is not there | Message anchoring |
| `CountingAdapter` | names `Section1`, `Section2` | Name stability |
| `DishonestAdapter` | drops grids and says nothing | Capability honesty |
| `StreamingAdapter` | imports one node at a time through `GetOrAddNode` | Two-phase synthesis |
| `SilentAdapter` | approximates every support silently | Loss coverage |

`StreamingAdapter` is the instructive one: **nothing about it looks wrong.** It uses `GetOrAddNode`,
which is the helper the contract mandates, and creates levels on demand. What it does not do is
collect first and cluster once.

The compliant adapter passes all seven. The golden fixture round-trips to **119 differences**, every
one of them named by a message.

---

## 6. A8 — the validation parity harness

**The premise changed in the direction that makes this cheaper and more urgent at once.** The plan
predicted drift between `FemexModel.Validate()` and the viewer's JavaScript mirror. Measured: drift
has *not* happened. 1.7 and 1.8 each landed on both sides in step. `femex-viewer.html` needed **no
change at all**.

That is the argument for the harness, not against it. A practice that has survived twice on
discipline alone, with no stated rule and no automated check, is not a guarantee — and the day it
fails, the free checker and the paid report disagree about the same file.

**The rule, now written down** (`FEMEXViewer.md`, *The validation parity rule*), is the third of the
plan's three options — an **explicitly labelled subset** — because that is what the measurement
showed: the viewer mirrors **fifteen of the engine's twenty-six** validator families. So:

1. The C# engine is authoritative; the panel is a preview and never leads it.
2. The viewer never reports something `Validate()` does not.
3. The subset is declared, in `parity-subset.json`, with reasons. Anything unlisted must be mirrored.
4. A new judgement check goes into C# first, then into the viewer before release *or* into that file
   with a reason.

**The seam keeps the viewer's independence.** `griffel-femex.Tests` owns the artefact —
`Examples/<name>.expected.json`, written from `Validate()`, failing when the checked-in copy
disagrees so the change is a reviewed diff rather than a number copied by hand.
`griffel-femex-viewer/parity-check.ps1` loads the same example into the real viewer under headless
Edge and compares its `issues[]` against that file. Neither repository references the other; both
reference a file.

**The two directions are not symmetrical**, and the script says so. A message the viewer emits and
the engine does not is *always* a failure. A message the engine emits and the viewer does not is a
failure *unless* `parity-subset.json` names it — which is what stops a deleted JS check from quietly
widening the subset.

**`Examples/Parity1.femex` exists only for this.** The other four examples validate clean, so
against them the harness could only catch a check firing when it should not. Parity1 is deliberately
defective — a dangling load case, a dangling node reference, a duplicated material name, a nameless
section, half-stamped uids, an unrecognised schema version, a member from a future schema and a
coincident node pair — and produces **eight** messages across six families, so the other direction
goes red too.

Two implementation notes worth keeping: the model is **injected, not fetched** (`file://` fetch is
blocked by CORS, and a flag that turns that off would be testing a browser nobody ships to), and
**`msedge.exe` does not attach to the caller's console**, so `& msedge --dump-dom` captures nothing
at all — silently, which is the shape of bug that makes a harness report green forever.

---

## 7. Verification performed

- **Build:** .NET 8 SDK 8.0.424 installed. `dotnet build` produces both legs with **0 warnings, 0
  errors**.
- **`netstandard2.0` into `net48`, proven before anything depends on it.** A .NET Framework 4.8
  console referencing `bin\Debug\netstandard2.0\griffel-femex.dll` reads
  `Conformance1.femex` (22 nodes, 14 bars, 0 validation messages), runs `GeometrySynthesis`, runs a
  full `ReferenceAdapter` round trip (21 export + 6 import messages) and runs `ModelDiff` (119
  differences against the round trip, **0** against itself) — under `mscorlib 4.0.0.0`.
- **Tests: 324 → 390, all green.** The suite grew rather than being restructured, and `dotnet test`
  at the repo root now finds it — before A9 it ran zero tests silently.
- **A2b's determinism**, per the plan: the same points in two orders yield identical node and level
  tables, including numbering, and a pair a hair inside the finished tolerance resolves the same way
  whether it arrives first or last.
- **Phase A's own stated proof**: the lossy reference adapter fails the conformance tests in exactly
  the ways it is designed to fail, and passes once its declarations match its behaviour. Seven
  broken adapters, seven catches, one per rule.
- **A8's own stated proof**: the detector was shown to detect, both ways, on throwaway copies of the
  viewer — one mirrored message reworded → FAIL with both lines quoted; one mirrored check silently
  disabled → FAIL naming what the subset does not excuse. Clean run: 5 examples agree.
  `femex-viewer.html` is byte-identical to what it was.
- **`griffel-femex-models` builds and runs** against the refreshed `net8.0` assembly.

---

## 8. Still open

- **`FemexEntity` has no `PlateRegion`**, so a region's messages anchor to `Plate` with the region's
  own id. The uid disambiguates and §7.2 matches on it, so nothing is broken — but a UI reading only
  `Entity` and `Id` would highlight the wrong thing. Widening §3.3's vocabulary is a decision the
  contract should take, not the implementation.
- **The level tolerance is still a shape, not a number.** §6.1 wanted it measured against a real
  Robot or RFEM model; the SAF corpus cannot supply it, because SAF has no storeys. Adapter #2 or
  the first engagement is where the evidence comes from.
- **Loss coverage is checked at entity granularity, not member granularity.** A per-concept message
  about `Material` covers every difference on any material, including one it was not about. That is
  §4.4's own shape and the alternative — tagging each message with the members it explains — is a
  design nobody has asked for yet. It is loose in the direction of accepting a lossy adapter, never
  in the direction of rejecting a good one.
- **`ModelDiffOptions.RelativeTolerance` defaults to exact**, which is §7.2 as written. A round trip
  through a real program that rounds a modulus will need it loosened, and the first adapter to do so
  should say why in its own summary rather than changing the default here.
- **The validation parity artefact carries no family tag**, so `parity-subset.json` matches on
  message text. Threading a family through `Validate()` would make the subset exact; it would also
  change a public API the report depends on, which is a Phase C conversation.
- **`Interop/Conformance/` ships in the product assembly.** The reference adapter and the harness
  are useful to adapter authors and are dead weight to everybody else. Splitting them into a
  `griffel-femex.Conformance` package is a packaging question, and §8 of `FEMEX_Adapters.md` puts
  packaging out of scope.
- **`IProgress<TransferProgress>` is in every signature and reported once per transfer.** A batch run
  over forty models is the first place it would earn its keep, which is Phase C.

---

## 9. Files

**New in `griffel-femex`:**
`Interop/{FemexEntity,TransferDirection,LossCategory,ObjectRef,TransferMessage,TransferResult,AdapterCapabilities,AdapterInfo,TransferProgress,TransferRequest,ExportReceipt,IFemexAdapter,NameSynthesis}.cs`
· `Interop/Conformance/{ReferenceDocument,ReferenceAdapter,ReferenceTransport,ConformanceCheck,ConformanceTransport,ConformanceHarness}.cs`
· `Comparison/{ModelDifference,ModelDiffOptions,EntityIndex,MemberComparer,ModelDiff}.cs`
· `Synthesis/{SynthesisOptions,GeometrySynthesis,SynthesisResult}.cs`
· `Compat/IsExternalInit.cs`
· `Examples/{Conformance1,Parity1}.femex`, `Examples/*.expected.json`
· `griffel-femex.Tests/{InteropContractTests,ModelDiffTests,GeometrySynthesisTests,ConformanceTests,BrokenAdapters,ValidationParityTests}.cs`

**Modified in `griffel-femex`:** `griffel-femex.csproj` (multi-target, STJ on the netstandard leg,
`LangVersion`), `griffel-femex.Tests.csproj` (`net8.0`, two new `<None Include>` lines),
`griffel-femex.sln` (the test project, so `dotnet test` finds it), `FemexModel.Identity.cs`
(`EnumerateIdentified` public, yielding `ObjectRef`), `FemexModel.Validation.cs` and
`FemexModel.Units.cs` (netstandard compatibility, four sites), `FemexModel.cs` and `IExtensible.cs`
(the `UnmappedMemberHandling` comments the retarget made false).

**Modified outside it:** `griffel-femex-models/{UpdateFemexDll.ps1,griffel-femex-models.csproj}`
(→ `net8.0`); `griffel-femex-viewer/FEMEXViewer.md` (the parity rule and an *As built* section),
`griffel-femex-viewer/{parity-check.ps1,parity-subset.json}` (new). **`femex-viewer.html` is
unchanged.**

---

## 10. What this makes stale

- **`FEMEX_Adapters.md` §3.7's runtime table**, which says the project targets `net7.0`. It targets
  `netstandard2.0;net8.0`, and the `net48` row's "No" is now "Yes, proven".
- **`FEMEX_Adapters.md` §5.2**, which says `EnumerateIdentified()` is private and that making it
  public "is not made here". It is public, and it yields more than it did.
- **`FEMEX_Adapters.md` §7.6's "three things, all later"** — the diff, the reference adapter and the
  level-clustering helper. All three exist.
- **`FEMEX_Adapters.md` §4.5's version list**, which quotes `CurrentSchemaVersion` as `"1.6"`. It is
  `"1.8"`.
- **`SAF_Adapter.md`'s *Verification*, the test-count line** — the suite is 390, not 324.
- **`SAF_Adapter.md` A8's premise**, which reads as if the viewer would need editing. It did not; the
  rule and the check did.
