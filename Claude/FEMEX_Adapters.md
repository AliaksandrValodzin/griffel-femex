# The FEMEX Adapter Contract

*What every FEMEX import/export plugin must implement, and what it is allowed to assume — settled once,
before the first plugin is written.*

> **Scope.** This is a contract, not an implementation. Nothing in the schema or the library is changed by
> this document, and no program is chosen as a first target: programs appear below as evidence for a rule,
> never as an assumption behind one. Where a rule leans on a schema gap the interop review lists as open, it
> says so and names the item that supersedes it. One recommendation here — and only one — implies a build
> change, made in a later pass rather than this one.

---

## 0. Verdict

**The contract is small, and almost all of it follows from one sentence: a half-drawn model is exportable.**

FEMEX is a plugin hub. A separate adapter per program, with FEMEX as the pivot, so that N adapters replace
N² translators. That arrangement only pays off if every adapter agrees on what crossing the boundary means —
otherwise the pivot is a shape and not a standard, and by plugin #5 there are five answers to every question
plugin #1 answered by accident.

Nine sections follow. Three of them carry real decisions rather than plumbing:

- **§3, the types.** Two interfaces, not one. A `TransferResult` carrying the model *and* the loss report,
  because a report on the side is a report nobody reads. A transfer message that is a distinct type from
  `ValidationMessage`, anchored to an object rather than free text. A synchronous call shape, because
  Revit and ETABS own the thread and an `async` signature invites the one thing that kills a Revit add-in.
- **§5, identity.** `IIdentified.cs` already states the *intent* — the exporting application assigns the uid
  and remembers the mapping to its native handle — and specifies no mechanism at all. This document supplies
  one: a v5 GUID derived from the native handle, never re-derived over a uid that already exists, with the
  mapping store declared rather than assumed.
- **§7, testing.** The loss report is the test specification: round-trip a model and assert that every
  difference between before and after is covered by a message. That is what turns "report your losses" from
  a matter of plugin-author diligence into something a test can fail on.

**What this document cannot decide.** Two things, both honestly. The tolerance that clusters native
elevations into FEMEX `Level`s is left open, because review §7.3 is right that the level-based node should
meet a real Robot or RFEM model before that number is fixed, and a tolerance guessed from vendor
documentation is exactly the sort of workaround that hardens into a permanent rule. And the whole document
is a hypothesis: like the interop review before it, **nothing here has been tested against a real exported
file**. Plugin #1 will either confirm the contract or break it, and breaking it is a cheaper outcome than
five plugins quietly disagreeing.

**What it costs to adopt.** Three pieces of code, all in a later pass: a model-diff utility, a deliberately
lossy reference adapter to prove the conformance tests can fail, and a level-clustering helper. Plus one
build change — multi-targeting the library — which turns out not to be an adapter convenience at all but a
prerequisite the schema queue is already waiting on (§3.7).

---

## 1. What an adapter is

**FEMEX is the pivot, and the adapter is the only place program-specific knowledge is permitted to live.**

That sentence is the whole architecture, and its second half is the part that gets violated. It is always
tempting to let one native concept leak inward — a `Bar.EtabsPierLabel`, a `LoadCase.RobotNature`, a
"just this once" field — because the alternative is losing something real. But every leak makes the pivot
one program's dialect, and the second adapter then has to translate through the first program's vocabulary
rather than through a neutral one. That is how a hub silently becomes a spoke.

So the boundary is absolute: below the adapter, native. Above it, FEMEX. Nothing crosses without being
named in FEMEX's own vocabulary or reported as a loss (§4).

**The pivot's job is the intersection, not the union.** FEMEX is not trying to be a superset of Robot,
Revit, ETABS, RCB and RFEM — that container would be enormous, unimplementable, and useless, because a
field only one program can fill is a field every other adapter drops. The review put this exactly right: the
question is not whether FEMEX can describe these programs fully — *"it cannot, and is not meant to"* — but
whether a model can cross without losing something the receiver cannot recover or infer. An adapter is
therefore allowed, and expected, to lose things. What it is not allowed to do is lose them quietly.

**Two directions, and they are not symmetric.** It is tempting to write one `IFemexAdapter` with
`Import` and `Export` on it, because a plugin usually does both and one interface is one file. Resist it,
for three reasons that are not aesthetic:

1. **The failure modes differ.** Import synthesises FEMEX structure that the native model does not have —
   levels, shared nodes, a unit system. Export resolves FEMEX structure the native model cannot hold —
   region priorities, finite restraint stiffnesses. *Invented* is overwhelmingly an import category;
   *Dropped* is overwhelmingly an export one.
2. **Not every program supports both.** A read-only adapter is genuinely useful — a program you can pull a
   model out of but never push one into is still worth having in the hub — and forcing it to implement a
   throwing `Export` is worse than letting it not implement `Export` at all.
3. **The capability surfaces differ per direction.** A program that can read plates but cannot write them is
   the common case, not the exotic one (§3.3).

Hence `IFemexImporter` and `IFemexExporter` as separate interfaces over a shared `IFemexAdapter` base
carrying identity and capabilities. A plugin implementing both is normal; a plugin implementing one is legal.

---

## 2. Three principles

Everything in §3–§7 is downstream of these three. They are stated first because a rule you can derive is a
rule you will not misremember.

### 2.1 A half-drawn model is exportable

**Export is a handoff, not a certificate of analysability.**

The motivating case is not exotic, it is the first one anyone will meet: a user lays out three levels in the
FEMEX editor and wants to finish the frame in ETABS. No sections chosen yet. No load cases. No supports. The
model is *incomplete*, and it is also exactly what the user wants to send. An adapter that refuses it has
misunderstood what the hub is for.

The naive shape — assume completeness, dereference everything, throw on null — is precisely what you get if
plugin #1 is written before this document exists, because from inside a plugin every null looks like a bug in
the caller.

**Consequence: exporters tolerate nulls by construction.** Not defensively, not with a null check bolted on
after the first crash report, but as the assumed shape of the input. The nullable surface an adapter must
handle without complaint includes at least:

| Nullable thing | Where | What it means |
| --- | --- | --- |
| `Plate.SurfacePropertyId`, `Plate.MaterialId` | `Geometry/Plate.cs:47,50` — both `int?` | Panel with no property yet, or an opening that legitimately has none |
| `Plate.Name`, `Level.Name` | `Geometry/Plate.cs:36`, `Geometry/Level.cs:26` | Unnamed geometry; §5.4 synthesises |
| `Section.Name`, `SurfaceProperty.Name`, `Material.Name`, `LoadCase.Label` | all `string?` | The four name-keyed families; §5.4 synthesises |
| `FemexModel.Units` | `FemexModel.cs:63` — `Units?` | No declared unit convention; §6.6 assumes one |
| `FemexModel.Mesh` | `FemexModel.cs:105` — `FemexMesh?` | Not meshed. Normal, not an error |
| `Guid? Uid` | every `IIdentified` | No round-trip identity. `IIdentified.cs` calls this *"a real answer, not a gap"* |
| Empty `LoadCases`, `Loads`, `LoadCombinations`, `Supports` | root lists | Geometry authored, analysis not set up |

### 2.2 The bar is the exception, and the contract says so out loud

The principle above has one place where it fails today, and it fails on its own motivating example.

`Bar.SectionId` and `Bar.MaterialId` (`Geometry/Bar.cs:32,35`) are non-nullable `int`, unlike their `Plate`
counterparts. `ValidateBars` (`FemexModel.Validation.cs`) reports an unresolvable one as an **Error**, and
by §2.3 an Error blocks. So a bar drawn before a section has been chosen carries `SectionId = 0`, resolves
to nothing, and is un-exportable.

That is not a corner case. That is the three-levels-then-ETABS scenario, blocked.

The schema fix — making both `int?` like `Plate`'s — is out of scope here and goes to §9 as a schema
question. What is in scope is the adapter rule:

> **No adapter ever leaves an unresolvable reference standing.** It synthesises a placeholder section and
> material, and reports each as *Invented* (§4.3).

**And the rule binds both directions.** The tempting phrasing — *an importer never writes an unresolvable
reference* — is the wrong shape, and wrong in a way worth spelling out, because it is the phrasing that
occurs to you first. A user who models three levels in the editor and hands off to ETABS has run no importer
at all. The bars are FEMEX-native. `SectionId` is `0` because nobody has chosen a section yet. It is the
**exporter** that meets the Error. An import-only rule therefore leaves the founding principle failing on
precisely its own example, which is a good sign the rule was stated at one end of the boundary instead of at
the boundary.

**And the placeholder must be recognisable, not merely present**, because inventing it converts a loud
failure into a quiet one. `TryGetBarSelfWeightPerLength` (`FemexModel.SelfWeight.cs:134`) returns `false`
only when the section is *missing*. Against a placeholder it **succeeds**, and returns γ·A for whatever area
the placeholder happens to have. A bar that was previously un-exportable becomes a bar carrying a confident,
wrong self-weight — which is a strictly worse outcome than the one the rule was written to fix.

So the placeholder carries a synthesised name in the §5.4 form, and an *Invented* message anchored to it, so
that a downstream consumer can tell a `Rectangle` somebody chose from a `Rectangle` an adapter made up to get
past a validation gate.

### 2.3 Errors block, warnings never do — and no adapter defines its own gate

`ValidationSeverity` (`ValidationSeverity.cs`) already draws exactly the line this contract needs, and draws
it in the right place:

- **Error** — *"The model is inconsistent: a referenced id does not resolve, or a rule the format guarantees
  is broken. A consumer cannot be expected to make sense of it."*
- **Warning** — *"The model is well formed and a consumer can read it, but it says something that is more
  often an oversight than a decision."*

That is *invalid* versus *incomplete*, which is the same distinction §2.1 turns on. Incomplete is honest and
exportable. Invalid is untranslatable.

`Validate(ValidationSeverity)` (`FemexModel.Validation.cs:59`) is already the filtered gate, so the rule is
mechanical: **an adapter's readiness check is `model.Validate(ValidationSeverity.Error).Any()`, and nothing
else.**

The prohibition matters more than the rule. An adapter must not invent a second notion of "ready" — no
"I refuse models without load cases", no "I require named sections", no "I will not export an unmeshed
model". Each of those is defensible in isolation and each one breaks §2.1 for somebody. If a rule genuinely
belongs to the format, it belongs in `Validate()` where every adapter inherits it; if it belongs to one
program, it is a *Dropped* or *Invented* message, not a refusal. There is exactly one gate, and it is
already written.

### 2.4 Every transfer yields a model *and* a loss report

This is what makes "any-software-in / any-software-out" a property rather than a slogan. A transfer that
produces only a model is a transfer whose losses are invisible, and invisible losses are the entire failure
mode of every exchange format that has ever disappointed anyone.

**The loss report is a return value, not a log line.** Logging is the shape this takes if nobody decides, and
it is the wrong one for three reasons: a log is not addressable by a caller, not assertable by a test, and
not presentable in a UI. §7.1 makes the report the test specification, which is impossible if it went to a
`TraceListener`.

---

## 3. The contract types

The core of the document. Shapes are proposed concretely, because "a result type carrying messages" is not a
contract until somebody writes down which messages and what they are attached to.

### 3.1 The interfaces

```csharp
namespace griffel_femex.Interop;

public interface IFemexAdapter
{
    AdapterInfo Info { get; }
    AdapterCapabilities Capabilities { get; }
}

public interface IFemexImporter : IFemexAdapter
{
    TransferResult<FemexModel> Import(
        ImportRequest request,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken);
}

public interface IFemexExporter : IFemexAdapter
{
    TransferResult<ExportReceipt> Export(
        FemexModel model,
        ExportRequest request,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken);
}
```

`AdapterInfo` carries the plugin's name, the program and version it targets, and — load-bearing, see §4.5 —
**the FEMEX schema version it was built against**.

`ExportReceipt` is deliberately not `void`: an export produces native handles, and §5.3 needs somewhere to
put the uid ↔ handle mapping when the adapter's chosen store is a sidecar rather than the document.

### 3.2 `TransferResult`, and why the messages are not on the side

```csharp
public sealed class TransferResult<T>
{
    public T? Value { get; }
    public IReadOnlyList<TransferMessage> Messages { get; }
    public bool Succeeded => Value is not null;
}
```

**The alternative, rejected:** return a bare `FemexModel` and expose the messages as a property on the
adapter, or via an event, or through an `out` parameter. Each of those makes reading the report optional at
the call site, and anything optional at the call site is omitted by the second caller. A single return value
that cannot be destructured without seeing the messages is the only shape where ignoring the report is a
visible decision rather than an oversight.

**`Succeeded` is defined by `Value`, not by severity**, which sounds like a detail and is the invariant the
whole taxonomy rests on — see §3.4.

### 3.3 `AdapterCapabilities`, and what it actually enumerates

A host needs to know what a plugin supports *before* offering it, so a user is not shown "Export to X" for a
program that cannot receive plates. But a capability declaration with no fixed vocabulary is unfalsifiable —
§7.3 wants a test asserting the declaration matches what the plugin does, and that test cannot be written
against free-form strings.

So the vocabulary is FEMEX's own root entity lists (`FemexModel.cs:79-105`), which is the natural axis
because it is the axis the model is actually made of:

```csharp
public enum FemexEntity
{
    Grid, Level, Node, Section, SurfaceProperty, Bar, Plate,
    Material, LoadCase, Load, LoadCombination, Support, Hinge, Mesh,
}

[Flags]
public enum TransferDirection { None = 0, Import = 1, Export = 2, Both = Import | Export }

public sealed class AdapterCapabilities
{
    public IReadOnlyDictionary<FemexEntity, TransferDirection> Entities { get; }
}
```

Declared **per entity and per direction**, because asymmetry is the common case, not the exotic one — a
program you can read plates out of but not write plates into is ordinary.

`Units` and `Gravity` are deliberately absent from the enum. They are not entities but model-level facts that
every adapter must handle, so making them capability-gated would let an adapter declare its way out of §6.5
and §6.6, which are two of the rules most worth not being able to opt out of.

### 3.4 `TransferMessage` is a distinct type from `ValidationMessage`

The obvious economy is to reuse `ValidationMessage` (`ValidationMessage.cs`) — same two severities, same
human-readable text, already written. Reject it, on meaning rather than on shape.

**A validation message says *this model is wrong*. A transfer message says *this crossing was lossy*.**
Those are different claims about different things, and conflating them means an adapter's honest report that
it approximated a spring support reads, downstream, as a defect in the model. It is not. The model is fine;
the target could not hold it. An adapter that is punished for reporting accurately will stop reporting
accurately, and §2.4 dies quietly.

```csharp
public sealed class TransferMessage
{
    public ValidationSeverity Severity { get; }
    public LossCategory? Category { get; }
    public ObjectRef Subject { get; }
    public string Text { get; }
    public string? NativeHandle { get; }
}
```

`ValidationSeverity` itself is reused rather than duplicated — the *discipline* is right and there is no
argument for a second two-valued severity enum in the same library.

**The invariant, which is where §2.1 becomes machine-checkable:**

> Every one of the five loss categories is a **Warning**. **Error** is reserved for a transfer that did not
> happen, and carries a null `Category`.

A loss never blocks, because by §2.1 losing something is what adapters are for. Failure blocks, because
there is no model. This also gives the exception policy its answer (§3.6): a native API failure is an Error
with a null category, and the loss report is already the right vehicle for it.

### 3.5 Message anchoring — a message names its object

```csharp
public readonly struct ObjectRef
{
    public FemexEntity Entity { get; }
    public int? Id { get; }
    public Guid? Uid { get; }
}
```

Free-text messages are the default outcome and the wrong one. `FemexModel.Validate()` itself yields plain
strings internally, which is defensible for a validator whose audience is a human reading a list — and not
defensible for a transfer report, whose audience includes a UI that must highlight the object and a test
that must assert coverage (§7.2).

The format has already paid for this. `FEMEX_Identity.md` gave `Load` an integer `Id` for exactly this
reason: loads *"are the only authored entity with no key at all today"*, and a message about one
*"would have nothing to name it by"*. Having bought that, the contract should not throw it away by reporting
losses as prose.

**Both `Id` and `Uid`, not one.** The integer id is what the file's own references use — it is what makes a
message actionable against the model in hand. The uid is what survives the crossing, and is what §7.2's
round-trip equivalence matches on. Either alone leaves one of the two consumers unable to use the message.

`NativeHandle` is the third leg and it is what makes a report diagnosable: knowing that FEMEX bar 41 lost
something is useful; knowing it was Robot bar `B41` is what lets somebody go and look.

### 3.6 The call shape — threading, cancellation, failure

Three decisions absent from the obvious design and ruinous to change once five plugins implement it. They
look like plumbing. They are not, which is why they are here and not in §8.

**Synchronous, on the caller's thread.** Revit's API may only be touched from its own main thread, and
ETABS' OAPI is no more permissive. An `async` signature is therefore actively harmful: it reads as an
invitation to `Task.Run`, and `Task.Run` around a Revit API call is the single most common way to kill a
Revit add-in. The contract's methods are synchronous, the host owns the thread, and the signature does not
suggest otherwise. An adapter that internally needs concurrency — a file parser, say — may use it, provided
nothing native leaves the calling thread.

**`CancellationToken` and `IProgress<T>` in the signature from the start.** A whole-model transfer is minutes
long. These two parameters are either present from the first version or they are never added, because adding
them later is a breaking change across every plugin. Both are cheap for an adapter that ignores them —
`progress` is nullable, and the token can be checked once per entity loop.

**A native API failure returns; it does not throw.** An adapter throws only for genuine programmer error
(a null model, a malformed request), on the same terms any library does. Everything it can describe — the
program not running, a licence check failing, a file it cannot parse — comes back as an Error-severity
`TransferMessage` with a null `Category` and `Value = null`. The reasoning is that a plugin that throws gives
the host no uniform behaviour to build on: every host then wraps every call in a `catch (Exception)` and
loses the distinction between "ETABS is not installed" and "this adapter has a bug". The loss report already
exists and is already the thing the caller must read.

### 3.7 Where the types live, and which runtime they target

These read as two questions and are one, so they are answered together.

**The types live in the core library**, in a new `Interop/` folder in `griffel-femex`.

The apparent alternative — a small separate contract assembly, so a plugin can reference something minimal —
does not survive contact with the signatures above. `IFemexExporter.Export` takes a `FemexModel`. Any plugin
implementing it needs the model types, which are in the core library. Splitting the contract out therefore
gives a plugin *two* references instead of one and buys nothing, because the heavy dependency was never the
contract; it was the model. One folder, one assembly, one reference.

**Which makes the runtime the real question, and it is the hardest one in this document.**

`griffel-femex.csproj` targets **`net7.0`**. That framework is out of support, and it is loadable by none of
the .NET Framework hosts the hub is aimed at:

| Host | Runtime | Can load `net7.0`? |
| --- | --- | --- |
| Revit 2023, 2024 | `net48` | No |
| Revit 2025+ | `net8.0` | No |
| Robot add-ins | COM against .NET Framework | No |
| ETABS OAPI clients | typically .NET Framework | No |

So "one dependency per plugin" is not merely inconvenient today — it is impossible for at least three of the
five targets, and no arrangement of folders changes that.

**The answer: multi-target `netstandard2.0;net8.0`.** `netstandard2.0` is the only target that both `net48`
and modern .NET can consume, and `net8.0` gives current hosts a first-class one. This is the **one place
this document implies a build change**, and it is not made here.

**A build change is not an adapter convenience, but it is no longer a prerequisite the schema queue is
waiting on.** `JsonSerializerOptions.UnmappedMemberHandling` is System.Text.Json **8.0** API, and
`griffel-femex.csproj` carries no `PackageReference` at all, so on `net7.0` that particular setting
**cannot be written**. That much stands. What does *not* follow — and what this document asserted until
1.4 — is that the status note's item 1 sits behind the same retarget. It did not: schema **1.4** closed it
with `[JsonExtensionData]`, which needs no package, no SDK and no csproj change. See §4.5. The retarget
question now stands on its own merits — reach into `net48` hosts — and has lost its schema prerequisite.

**What the `netstandard2.0` leg costs**, stated because it is not free and a contract that hides it is
setting up a later surprise. `netstandard2.0` does not carry System.Text.Json, and the library needs a modern
one: `JsonPolymorphic` and `JsonDerivedType` (`Geometry/Sections/Section.cs`) and `IJsonOnDeserialized`
(`FemexModel.SelfWeight.cs:27`) are all STJ 7+. So that leg needs an explicit package reference — and
shipping a System.Text.Json into Revit's `net48` AppDomain, beside whatever version Revit itself has already
loaded, is an assembly-binding hazard of exactly the kind that produces a `MissingMethodException` on one
machine and not another. This, rather than the target framework on its own, is the real reason "one
dependency per plugin" is hard, and it is better said now than discovered by plugin #1.

---

## 4. The lossy-mapping taxonomy

One shared vocabulary, so that five adapters report comparably and a user reading two loss reports is
reading the same kind of document twice.

```csharp
public enum LossCategory { Dropped, Approximated, Invented, Unmapped, Stale }
```

### 4.1 Dropped — FEMEX said something the target cannot express

The target has no concept at all, so the information does not arrive. Plate region priority into a program
with no region model is the canonical case: review §3.2 argues FEMEX's priority-based regions are *better*
than SAF's alternative, which is precisely why they have nowhere to go in a program that models a slab as one
thickness.

### 4.2 Approximated — expressible, but not exactly

The target can hold something *near* what FEMEX said. A finite `Restraint.Stiffness`
(`BoundaryConditions/Restraint.cs:16`, `double?`) into a fixed-or-free-only target. A curved edge, already
polygonised by FEMEX's own chords decision, re-polygonised at a different density.

**Sections are now an example of this, and they were the taxonomy's largest single hole.** They were
*Dropped* rather than approximated because `Geometry/Sections/` was `Rectangle`, `Circle` and `TSection`
and nothing else — no profile designation, no catalogue reference, no numeric escape hatch — so there was
no shape to approximate a steel member *with*.

> **Closed by `Claude/FEMEX_StandardSections.md`** (schema 1.5 and 1.6). 1.5 is status item 2, the numeric
> escape hatch: an optional `SectionProperties` block (`area`, `iy`, `iz`, `j`, the shear areas and SAF's
> design group) on every section, and a `generic` discriminator carrying nothing else, so a shape FEMEX
> does not model crosses by its stiffness. 1.6 is item 3: `ishape`, `channel`, `angle`, `box` and `pipe`,
> plus an optional `catalogue` block naming the profile and the library it came from. A section is
> therefore never *Dropped* — a receiver resolves the catalogue name, else builds the parametric shape,
> else builds a member with the stated stiffness, so the worst case is *Approximated*. What remains
> genuinely approximate is narrower and named: an angle crosses with geometric-axis stiffness only, there
> being no `iu`/`iv`, and tapered, asymmetric and compound sections are reserved and unimplemented.

### 4.3 Invented — the target required something FEMEX does not say

The adapter supplied a default. **This is the important category, and the one naive adapters never report**,
because from inside the adapter an invention does not feel like a loss — it feels like success. Everything
worked. A number was produced. The user got a model.

Three of this document's rules exist mainly to force *Invented* into the open: the placeholder section
(§2.2), the assumed unit system (§6.6), and `Gravity.Acceleration` (§6.5). Each is a case where an adapter
that says nothing produces a model that looks complete and is wrong.

### 4.4 Unmapped — a native concept with no FEMEX home

The import-side mirror of *Dropped*. The native model said something and FEMEX has no noun for it. The
inventory is review §5, which is entirely untouched as of the status note: rigid diaphragms (§5.1), bar end
offsets and insertion points (§5.2), bar behaviour (§5.3), stiffness modifiers (§5.4), material
under-specification (§5.5), support local axes (§5.6), elastic foundations (§5.7), temperature gradients
(§5.8). Add ETABS pier and spandrel labels, which review §6 places deliberately out of scope.

An adapter reports *Unmapped* per concept, not per object — one message saying "142 members carried
stiffness modifiers, which FEMEX cannot express" is a useful report; 142 messages saying it is a denial of
service against the person reading them.

### 4.5 Stale — the only category that is not about the native boundary

*Stale* is about the FEMEX boundary: a loss that happens between two FEMEX builds, with no program involved.

`FemexModel.cs:110-120` configures camelCase, indenting, ignore-nulls and the enum converter — and does not
set `UnmappedMemberHandling`. As §3.7 establishes, on `net7.0` it *cannot*. The failure that left was
concrete: status §2.2's example is a 1.3 file read by a 1.2 build, losing its uids without a word. A build
that predated a schema addition destroyed that addition on read and reported nothing.

**Schema 1.4 closed this without the retarget.** Every serializable type now implements `IExtensible` and
carries `[JsonExtensionData]`, so a member this build has no property for is kept, written back on save,
and reported once per distinct name by `FemexModel.ReportUnknownMembers()`. That is *preserve-and-warn*
rather than the refusal `Disallow` would give, and the trade is recorded rather than claimed as a
dominance: extension data preserves syntax, not referential integrity. `UnmappedMemberHandling` remains
unset and is now an **option** a future retarget could adopt, not a gap.

Two limits worth stating plainly. It does not rescue §2.2's own instance — a 1.2 build is already written
and nothing added in 1.4 reaches it; what 1.4 closes is the loss *class*, forwards. And an adapter is not
a FEMEX build, so the reporting rule below still has to hold on the native boundary:

> **Every adapter declares the schema version it was built against** (`AdapterInfo`), compares it to
> `FemexModel.SchemaVersion` on read, and reports a higher one as a *Stale* loss rather than proceeding
> silently.

`CurrentSchemaVersion` is `"1.6"` and `ReadableSchemaVersions` is
`{ "1.1", "1.2", "1.3", "1.4", "1.5", "1.6" }` (`FemexModel.cs:54,64`), so the machinery to notice is
already there; what is missing is the obligation to say something. This is program-agnostic, costs nothing, and is the only loss in the list that no per-program
mapping document would ever catch — which is a good argument for it being in the shared contract rather than
in five of them.

---

## 5. Identity, re-import and merge

The section with the most leverage, because `IIdentified.cs` already specifies the *intent* with unusual
precision and specifies no mechanism whatsoever:

> *"Assigned by the **exporting** application, which remembers the mapping to its own native handle — Revit's
> `UniqueId`, an ETABS GUID, a Robot label. FEMEX never mints one on save; `AssignMissingUids` is a call a
> caller makes, and it never overwrites one that is already there."*

"Remembers the mapping" is the unspecified half, and it is the half every adapter has to implement.

### 5.1 Who mints uids, and how

`AssignMissingUids()` (`FemexModel.Identity.cs:44`) walks every `IIdentified` entity, mints
`Guid.NewGuid()` for those without one, and never overwrites. That is right for a caller building a model in
memory, and it is the wrong mechanism for an adapter, because a random uid means the mapping to the native
handle lives *only* in a side table. Lose the side table and every object in the model becomes a stranger to
the program that exported it — which defeats the entire purpose of `Uid` existing.

> **Rule.** An adapter with a native handle in hand **derives** the uid deterministically: a version-5 GUID
> over the handle, under a fixed FEMEX namespace GUID published with the contract.

Derivation survives losing the side table, survives reinstalling the plugin, and survives the same model
being exported from two machines. `AssignMissingUids()` remains correct for everything else — hand-authored
models, the editor, entities with no native counterpart.

> **Rule.** An adapter **never re-derives over a uid that is already there** — the same rule
> `AssignMissingUids` already follows, for the same reason: a uid that changes is not an identity.

**What happens when both mechanisms meet in one model**, which the contract has to answer because it is
otherwise a silent trap. A random uid and a derived one are indistinguishable at read time; there is no bit
that says which is which. And `ValidateUidCoverage` (`FemexModel.Validation.cs`) only warns when coverage is
*partial* — it returns nothing when `carrying == 0 || carrying == total`. So a fully-covered, half-derived
model passes validation in silence, and half of it will merge on re-import while half duplicates.

Since the schema carries no marker, the contract puts the obligation on the report:

> **Rule.** An adapter states in its loss report how many uids it derived and how many it found already
> present. A model whose uids it did not derive is one it cannot promise to merge.

### 5.2 A note on what the library does not expose

`EnumerateIdentified()` (`FemexModel.Identity.cs:70`) is **private**. It is the one walk over every
`IIdentified` declaration site in the model, and both §5.4's name synthesis and §7.2's uid-keyed equivalence
need exactly that walk. Today an adapter or a conformance test cannot reuse it and must reimplement it —
which means thirteen declaration sites hand-listed in at least two more places, going stale independently.

Making it public is a small, additive change and is not made here. It goes to §9 as the second-cheapest item
on the list.

### 5.3 Where the uid ↔ native-handle mapping is stored

Three mechanisms exist, programs differ in which they permit, and the contract's job is to say which are
acceptable rather than to pretend everybody uses one:

1. **Nowhere — because it is derivable.** The preferred answer, and available whenever the native handle is
   stable and readable (§5.1). Nothing to store, nothing to lose, nothing to migrate.
2. **In the native document.** Revit extensible storage, or a program's own GUID field where it has one —
   ETABS' `FrameObj.SetGUID`, per review §4.6. Robust, and it travels with the file.
3. **A sidecar file** beside the native document. The fallback for programs offering neither, and the reason
   `Export` returns an `ExportReceipt` rather than `void`.

> **Rule.** An adapter declares which mechanism it uses in `AdapterInfo`. A sidecar-based adapter states in
> its loss report that re-import identity depends on a file the user can delete — because a user who moves a
> `.str` without its sidecar deserves to have been told.

### 5.4 Name synthesis, and what it derives from

Robot's properties and ETABS' sections and stories key by **name**, not by id. Robot's
`Labels.StoreWithName(label, "Fixed")` makes the name the foreign key outright, and — review §1.1 —
**silently overwrites** a label of the same name. A blank name and a duplicated name are therefore both data
loss on export, not cosmetic problems.

`Section.Name`, `SurfaceProperty.Name`, `Material.Name` and `LoadCase.Label` are all `string?`, deliberately:
`FEMEX_Identity.md` chose a warning over a requirement so that no existing file was invalidated, and said
plainly that the warning exists to tell an author *"what an exporter targeting Robot is about to have to
invent"*. This is the contract picking that invitation up.

> **Rule.** An exporter synthesises a name wherever one is null or blank, and reports it as *Invented*.

**Stable from what, though** — the rule is empty until it names a source, and every obvious candidate has a
defect:

| Source | Defect |
| --- | --- |
| `Section.Id` and friends | Renumbers whenever a model is rebuilt. Stable within one authoring session only |
| List position / ordinal | Worse: moves whenever a list is reordered, which nothing forbids |
| A counter (`Section1`, `Section2`) | The failure the rule exists to prevent — a second export yields `Section1_2`, a third `Section1_2_2` |
| `Uid` | Correct, **provided one exists** |

The `Uid` objection is real but narrower than it looks. `AssignMissingUids` mints random GUIDs, so a
uid-derived name on a model that has never met an adapter is not *meaningful* — but it is still **stable**,
because the same `IIdentified.cs` guarantee that makes derivation safe makes this safe too: a uid, once
minted, is never overwritten. The instability the rule forbids is a name that changes between exports, and a
uid-derived one does not.

> **Rule.** An exporter calls `AssignMissingUids()` before synthesising any name, and derives the name from
> the object's `Uid`: `{Kind}-{first 8 hex digits of the uid}` — `Section-3f9a2c14`, `Level-b71e04d9`.

Three properties, each deliberate. It is **stable**, by the argument above. It is **collision-resistant
enough** for a model of any plausible size, and a collision is caught by `ValidateNameKeys`' duplicate check
rather than silently overwriting a Robot label. And it is **obviously synthetic**, which matters more than
prettiness: a synthesised name that looks authored hides the fact that something was invented, and §4.3
exists because that is the failure adapters are worst at.

Where the adapter has a native name worth keeping, it uses that instead. The synthesised form is the floor,
not the preference.

### 5.5 The synthesis rule covers more than the validator does

`ValidateNameKeys` (`FemexModel.Validation.cs`) checks exactly four families — `Section`,
`SurfaceProperty`, `Material`, `LoadCase` — because those are the four review §4.6 named.

But `Level.Name` (`Geometry/Level.cs:26`) and `Plate.Name` (`Geometry/Plate.cs:36`) are `string?` too, and a
**storey is name-keyed in ETABS and Robot every bit as much as a section is**. An exporter that synthesises
section names and leaves storeys nameless has solved the half of the problem the validator happens to
mention.

> **Rule.** §5.4 applies to `Section`, `SurfaceProperty`, `Material`, `LoadCase`, **`Level` and `Plate`**.

That the validator has not caught up is a note for §9, not a reason to write the narrower rule. The contract
follows the target programs, not the current state of one validation method.

---

## 6. Pre-decided hard mappings

Decisions that otherwise get made five times, five ways. Each is stated as a rule plus the helper that
already implements it — and where no helper exists, the rule says so rather than gesturing at one.

### 6.1 Level synthesis

Every importer from a non-storey program must invent a `Level` for geometry that has no storey meaning: a
truss diagonal, a raking column, a ramp, a bridge deck. Review §7.3 names this as the real, recurring cost of
the level-based node and leaves it open. The contract closes the *policy* with one answer rather than five:

> **Rule.** Snap an incoming elevation to an existing `Level` within tolerance; otherwise create one. Always
> emit an *Invented* message for a level the native model did not have.

**This rule is the exception to this section's discipline: it names no existing helper, because none
exists.** The repository has `GetCoincidenceTolerance` (`FemexModel.Nodes.cs:34`), which is a
three-dimensional *node* tolerance, and nothing at all for matching a `Level.AbsoluteElevation`. Level
clustering is therefore a piece of implied code, counted in §7.6 alongside the diff utility and the reference
adapter rather than smuggled in.

**And the tolerance itself is deliberately not fixed here.** Review §7.3 argues the level-based node should
be measured against a real Robot or RFEM model before the decision is settled, and status item 6 is that
measurement. A number chosen now from vendor documentation would be a guess that hardens into a rule the
moment plugin #1 depends on it. What the contract does state is the *shape*: a relative tolerance derived
from the native model's own vertical extent, floored, in the manner of `GetCoincidenceTolerance`'s
`1e-6 × diagonal` with a `1e-9` floor (`FemexModel.Nodes.cs:66`) — not an absolute millimetre value, which
would mean something different in a metre model and a millimetre one. The number goes to §9.

### 6.2 Node sharing, and why `GetOrAddNode` alone is not enough

FEMEX's unit of connectivity is the shared node — `Geometry/Node.cs` states it outright: *"two elements are
joined where they name the same node number, and only there."* An importer that trusts the native node list
therefore either loses connectivity silently or invents it silently.

> **Rule.** Importers create nodes through `GetOrAddNode` (`FemexModel.Nodes.cs:101`) and
> `GetCoincidenceTolerance` (`:34`), never by transcribing a native node list.

**But that rule alone is insufficient, because the tolerance moves while you import.**
`GetCoincidenceTolerance` is `1e-6` of the model's *current* bounding diagonal, computed from the nodes
already added (`FemexModel.Nodes.cs:66`). During an import that starts from an empty model it therefore
begins at the `1e-9` floor and grows as the model fills: the first nodes are matched against a far tighter
test than the last. Two nodes `FindNodeAt` kept apart early can be coincident by the time the model is
finished — `ValidateCoincidentNodes` will say so afterwards, as a warning, which is a diagnosis and not a
fix.

The consequence for the contract is that **the same native model read in a different order yields a different
node table**, and so does the same model read twice if the traversal is not deterministic. That is fatal to
§7.2's round-trip equivalence, which assumes a model round-tripped twice is the same model.

> **Rule.** Node and level synthesis are **two-phase**: collect every candidate coordinate and elevation
> first, cluster once against the finished extent, then create. Never streamed one element at a time.

This is the kind of rule that is obvious in hindsight and gets rediscovered separately by every plugin
author, which is the definition of something belonging in a shared contract.

### 6.3 Axis and sign normalisation

FEMEX is right-handed with Z up, and says so in two places because it is the commonest translator bug there
is: `Gravity.cs:11` and `Geometry/Vector3d.cs:11` both flag that **RFEM's global Z points down by default**.

> **Rule.** Normalising to right-handed Z-up happens at the adapter boundary, in the adapter, once. Never in
> the caller afterwards.

The alternative — letting a model in with a flag saying "this one is Z-down" — spreads a conditional through
every consumer of the model and guarantees somebody misses one. A translator from RFEM writes `(0, 0, -1)`
into `Gravity` and flips its geometry; a translator *to* RFEM does the reverse. The trap is stated rather
than inherited.

### 6.4 Load direction and local axes

Three independent facts a receiving program needs and cannot guess — `LoadCoordinateSystem`, `LoadDirection`
and the projected flag — and all three are already resolved, executably, in the library.

> **Rule.** Exporters resolve load directions through `TryGetLoadDirection`
> (`FemexModel.LocalAxes.cs:113`), and bar and plate local axes through `TryGetBarLocalAxes` (`:38`) and
> `TryGetPlateLocalAxes` (`:86`). Never re-derived per plugin.

The reason is not tidiness. Those methods encode the ETABS/SAP local-axis convention and the
vertical-member substitution — the rule for a column, where the general definition degenerates — and a
plugin author re-deriving it from `Bar.cs`'s prose will get the degenerate case wrong. `LoadDirection.Vector`
and `LoadCoordinateSystem.Local` both route through the same resolvers, so an exporter that uses them gets
the arbitrary-direction and local-frame cases for free.

### 6.5 Self-weight: the double-count, and the larger trap underneath it

`LoadCase.SelfWeightFactor` (`Loads/LoadCase.cs:47`) says how much of the structure's own weight a case
carries. Most target programs have their own self-weight flag. Doing both is the classic error, and the
interop review names it as producing a *confidently wrong* answer rather than an obviously incomplete one —
which is worse, because nothing looks broken.

> **Rule.** Set the native flag **or** materialise loads via `TryGetBarSelfWeightPerLength`
> (`FemexModel.SelfWeight.cs:134`) and `TryGetPlateSelfWeightPerArea` (`:161`), never both, and report in the
> loss report which was done.

**And the larger self-weight trap is `Gravity.Acceleration`, which that rule does not touch.**

It defaults to `9.80665` (`Gravity.cs:38`), and its own comment says the default is metre-specific and that
**"a millimetre model that accepts it is 1000x light"**. Every self-weight number in the library flows
through it: `GetWeightDensity` is `Density * Gravity.Acceleration` (`FemexModel.SelfWeight.cs:119`), and both
`TryGet…SelfWeight…` helpers multiply by that.

So an importer from a program whose native units are millimetres, feet or inches, that simply leaves the
default alone, produces a model wrong by three orders of magnitude — with nothing in `Validate()` to catch
it, because gravity's validation checks the direction and the sign, not whether the magnitude matches the
declared length unit.

> **Rule.** An adapter sets `Gravity.Acceleration` consistently with the unit system it declares (§6.6), and
> reports it as *Invented* whenever the native model did not state one — which is nearly always, since most
> programs carry gravity as a preference rather than as model data.

### 6.6 Units

`Units` (`Units.cs`) is two nullable strings — `Length` and `Force` — with no temperature, angle or mass
unit, and no validation of any kind: the word does not appear in `FemexModel.Validation.cs`, so
`"length": "banana"` round-trips clean. Review §5.9 has this as a P1 gap and status item 5 is the fix.

The adapter boundary is where units actually bite, because it is the only place a number leaves the system
that produced it.

**The tempting rule — *refuse a model whose units you cannot read* — has to be rejected**, on two grounds
that are both this document's own. It would be an adapter inventing a notion of "ready" outside `Validate()`,
which §2.3 forbids. And it would block the half-drawn handoff §2.1 exists to protect, since `Units` is
nullable and a model that has never been told its units is exactly the model a user wants to send.

> **Rule.** An adapter **proceeds on a declared assumption** and reports the assumed system as *Invented*.

**And the contract names the assumption rather than leaving it per-adapter**, because "each adapter assumes
something and says what" is the five-ways failure this section exists to prevent, only now with a paper
trail. The choice is not open anyway — FEMEX made it implicitly, twice: in `Gravity.Acceleration`'s
metre-specific default (`Gravity.cs:38`) and in `Examples/Example1.femex`'s `"length": "m"`, `"force": "kN"`
header.

> **Rule.** The assumed system is **metres and kilonewtons**. An adapter assuming otherwise says so in the
> message. The *Invented* report stands either way.

Review §7.2 item 8 — units as enums, plus temperature, angle and mass — supersedes this rule the day it
lands, and this document says so rather than pretending to be permanent.

### 6.7 A note on cost, not correctness

Every helper this section mandates resolves its references with a linear `List.Find`:
`TryGetBarLocalAxes` over `Bars` (`FemexModel.LocalAxes.cs:42`), `TryGetBarSelfWeightPerLength` over `Bars`
then `Sections` (`FemexModel.SelfWeight.cs:138,142`), `GetWeightDensity` over `Materials` (`:121`), and so
on. Calling them once per element across an imported model is quadratic, and an adapter over a large native
model will feel it.

Reusing them is still right, and re-deriving the rules per plugin is still wrong. The answer is an index
built once at the boundary — a dictionary from id to entity, held for the duration of the transfer — not a
second implementation of the conventions. A note for whoever writes plugin #1, not a reason to weaken any
rule above.

---

## 7. Testing the contract

The part that makes the rest enforceable — and it only works if it is split in two, because half of it
cannot run without the program installed.

### 7.1 The loss report is the test specification

> Round-trip a model FEMEX → native → FEMEX, and assert that **every difference between the two models is
> covered by a reported message**.

An undeclared difference is a bug. A declared one is the adapter working as designed. That single assertion
turns §2.4 from a matter of plugin-author diligence into something a test suite can fail on, which is the
only reason to believe adapters will actually report their losses.

### 7.2 Which means "difference" has to be defined here

As stated, the assertion is unfalsifiable, because a *clean* round-trip through any real program differs in
ways no adapter should have to report. Ids and node numbers are renumbered by the native program and
renumbered again on the way back. List order is preserved by nothing. Coordinates come back through the
native program's own precision.

Deferring the diff *utility* to a later pass is right. Deferring the *equivalence definition* would mean that
pass decides it while writing a comparison loop — which is precisely the by-accident decision this whole
document exists to prevent. So:

> **Equivalence.** Objects are matched by **`Uid`**, never by `Id`. Lists compare as **sets** under that key,
> not as sequences. Geometry compares within **`GetCoincidenceTolerance`**. Anything left over — an object
> on one side and not the other, or a matched pair differing in any field — is a difference, and must be
> named by a `TransferMessage` whose `Subject` anchors to it.

That definition is also what makes §5's identity rules load-bearing rather than decorative, and it has a
consequence worth stating plainly: **a model whose uid coverage is partial cannot be round-trip-tested at
all**, because half its objects have no matching key. Uid coverage is a precondition of the test suite, not
a nicety.

### 7.3 Tier 1 — offline, runs everywhere

Nothing in this tier touches a native API, so it runs on any machine and in CI:

- **Null tolerance** across every nullable reference in §2.1's table — feed each null and assert the adapter
  does not throw.
- **Message anchoring** — every message carries an `ObjectRef` that resolves in the model, and no message
  reports its subject only in prose.
- **Name stability** — export twice, assert the synthesised names are identical, and assert they match
  §5.4's `{Kind}-{8 hex}` form where the source was null.
- **Capability honesty** — the `AdapterCapabilities` declaration matches what the adapter actually produces
  and consumes, entity by entity. Checkable only because §3.3 fixed the vocabulary.
- **No second gate** — the adapter accepts every model that passes `Validate(ValidationSeverity.Error)`,
  including deliberately incomplete ones.
- **Two-phase synthesis** — import the same native fixture with its elements in two different orders and
  assert the resulting node and level tables are identical (§6.2).

### 7.4 Tier 2 — live, gated on the program

The FEMEX → native → FEMEX round-trip of §7.1 needs Robot's COM server, the ETABS OAPI or an RFEM endpoint,
installed and licensed. That is a real constraint and pretending otherwise has a predictable outcome:
**a single undifferentiated suite gets skipped wholesale on every machine that lacks any one program**, which
means tier 1 stops running too. Naming the tiers separately is what prevents that.

### 7.5 A conformance base class, and something to run it against

A **conformance test base class** each adapter inherits, so adapter #5 gets both tiers for free and cannot
quietly skip a rule by not writing the test for it.

**And a reference adapter to run it against**, because a base class with no implementation in this repository
is a suite that has never been shown to fail. Tier 1 needs one in-memory fake — FEMEX in, FEMEX out, no
native API — written deliberately **lossy**: dropping something it declares, inventing a section and a unit
system, leaving one concept unmapped, so that each category in §4 is exercised by something. Its job is not
to be a useful adapter but to prove the conformance tests can tell a compliant plugin from a non-compliant
one, before any real plugin depends on that distinction.

### 7.6 What this implies in code — three things, all later

1. A **model-diff utility** implementing §7.2's equivalence: uid-keyed matching, set comparison,
   tolerance on geometry.
2. The **lossy reference adapter** of §7.5.
3. The **level-clustering helper** of §6.1.

All three are a **follow-up pass, not this one**. `griffel-femex.Tests/RoundTripTests.cs` and
`RoundTripIdentityTests.cs` already contain round-trip assertions and are the style to follow.

### 7.7 The golden model is a file, not a call

`SampleModel.Build()` already exercises grids, plates, regions, combinations, self-weight and identity, and
is the obvious fixture. It cannot be used directly. It lives in `griffel-femex-models`, which consumes this
library as a **prebuilt binary** — `<Reference Include="griffel-femex">` with `HintPath` `lib\griffel-femex.dll`,
refreshed by `UpdateFemexDll.ps1` — so a conformance base class shipping beside the contract types cannot
reference it without inverting that dependency.

> **Rule.** Serialise the sample once to a `.femex` file in this repository, following
> `Examples/Example1.femex`, and treat the file as the baseline.

A fixed file is the better baseline on its own merits anyway: `Build()` changes under the suite every time
the sample grows, and a conformance baseline that moves is not a baseline.

---

## 8. Deliberately out of scope

So this is not read as a plugin to-do list. Each of the following is its own later document:

- **Packaging, installers and update mechanisms.** Per-host and unrelated to the contract.
- **Plugin UI.** What a user sees when they pick a file. The contract's `IProgress<TransferProgress>` is the
  seam; what is drawn against it is not settled here.
- **Licensing and obfuscation.**
- **Per-program API version pinning.** Which Revit years one binary covers, and how.
- **The actual per-program mappings.** The ETABS mapping, the Robot mapping, the RFEM mapping — each a
  document, each substantially longer than this one, and each properly written *after* a real file from that
  program has been read (status item 6).

---

## 9. Still open

- **The level-clustering tolerance (§6.1).** The policy is settled; the number is not. Review §7.3 wants the
  level-based node measured against a real Robot or RFEM model first, and status item 6 is that measurement.
  A relative tolerance in the manner of `GetCoincidenceTolerance` is the shape; the coefficient is a guess
  until a real file exists.
- **Whether `EnumerateIdentified()` should be public (§5.2).** Private today
  (`FemexModel.Identity.cs:70`), and both name synthesis and the round-trip equivalence need exactly that
  walk. The cheapest additive change on this list.
- **Whether `Bar.SectionId` and `Bar.MaterialId` should become `int?`** like their `Plate` counterparts.
  That would let a half-drawn bar be honest, and would retire §2.2's invented placeholder — which exists only
  because the schema currently forbids the truthful answer. A schema question, not an adapter one.
- **Whether a contract may imply a build change (§3.7).** The multi-targeting is the first time a
  document-only pass has reached the csproj. Sharpened by the finding that `UnmappedMemberHandling` is
  unreachable without it: the retarget is a schema prerequisite, not only an adapter convenience.
- **Whether a plugin built against 1.3 may read a 1.4 file at all**, or must decline until
  `UnmappedMemberHandling` is set. §4.5 requires it to *report*; whether reporting is sufficient, or whether
  silent field loss should be refused outright, is unresolved — and refusing sits uncomfortably beside §2.3.
- **Whether a host may offer a plugin that declares only one direction.** §3.1 already separates the
  interfaces, so the question is not whether a read-only adapter can be *written* but whether the hub should
  present one — given that a program you can import from but never export to breaks the round-trip that §7
  tests everything against.
- **Whether the mapping store may be required at all (§5.3)** for programs offering neither derivable handles
  nor document-attached storage. A sidecar is a real answer and a fragile one.
- **Whether `ValidateNameKeys` should widen** to `Level` and `Plate`, matching §5.5's synthesis rule. The
  validator currently covers four of the six families the contract names.
- **Nothing here has been tested against a real exported file.** Every FEMEX claim above is verified against
  this repository; every claim about an external program comes from the interop review, which is itself
  explicit that the ETABS `.e2k` grammar and essentially all of INDUCTA RCB are reconstruction. **The
  contract is a hypothesis until plugin #1 either confirms it or breaks it**, and it was written to be cheap
  to revise for exactly that reason.

---

## Sources

**FEMEX** — verified directly in this repository: `FemexModel.cs` (`:50,60,79-105,110-120,139,147`),
`FemexModel.Validation.cs` (`:21,59`, `ValidateBars`, `ValidateNameKeys`, `ValidateUidCoverage`,
`ValidateCoincidentNodes`), `FemexModel.Identity.cs` (`:44,70`), `FemexModel.Nodes.cs` (`:34,66,101`),
`FemexModel.LocalAxes.cs` (`:38,86,113`), `FemexModel.SelfWeight.cs` (`:27,107,119,134,161`),
`IIdentified.cs`, `ValidationMessage.cs`, `ValidationSeverity.cs`, `Gravity.cs` (`:11,26,38`),
`Units.cs`, `Geometry/Node.cs`, `Geometry/Level.cs` (`:26`), `Geometry/Bar.cs` (`:32,35`),
`Geometry/Plate.cs` (`:36,47,50`), `Geometry/Sections/Section.cs`, `Geometry/Vector3d.cs` (`:11`),
`BoundaryConditions/Restraint.cs` (`:16`), `Loads/LoadCase.cs` (`:47`), `Loads/LoadDirection.cs`,
`Loads/LoadCoordinateSystem.cs`, `griffel-femex.csproj`, `Examples/Example1.femex`,
`griffel-femex.Tests/RoundTripTests.cs`, `griffel-femex.Tests/RoundTripIdentityTests.cs`,
and `../griffel-femex-models/griffel-femex-models.csproj`.

**The five programs** — every external claim above is carried by `FEMEX_Interop_Review.md`, which holds the
vendor URLs for Robot, the Revit analytical model, ETABS, INDUCTA RCB, RFEM 6 and SAF 2.2, and which is
explicit about which of them are documented and which are reconstruction. Nothing new is asserted here about
any program that is not already sourced there.

**Prior FEMEX documents** — `FEMEX_Interop_Review.md` §3.2, §4.4, §4.5, §4.6, §5, §6, §7.2, §7.3 ·
`FEMEX_Interop_Status_16082026.md` §2.1, §2.2, §4, §5 · `FEMEX_Identity.md` §5, §6 ·
`FEMEX_Adapters_Plan.md`, the brief this document executes.

**Runtime claims** — `JsonSerializerOptions.UnmappedMemberHandling` and `JsonUnmappedMemberHandling` are
System.Text.Json 8.0 / .NET 8 API; `JsonPolymorphic`, `JsonDerivedType` and `IJsonOnDeserialized` are
System.Text.Json 7.0. Host runtimes (Revit `net48` through 2024 and `net8.0` from 2025) are as recorded in
the interop review's Revit sources.
