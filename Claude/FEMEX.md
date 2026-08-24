# FEMEX — JSON-Serializable Model Refactor

## Context
FEMEX is a minimal file format describing a finite-element model of a building
structure (see `FEMEX description.docx`). The goal is **one JSON-serializable
root C# class** holding the four data blocks: Geometry, Materials, Loads,
Boundary Conditions.

The current code is a good skeleton but does not meet the goal:
- No root container class tying the blocks together.
- Geometry uses **object references** (`Node.AssociatedLevel`, `Bar.StartNode`,
  `Element.Material/Section`, `Load.Case`) that don't round-trip cleanly in JSON,
  while `Loads` already use integer ids — inconsistent.
- Abstract types (`Element`, `Section`, `Load`) lack polymorphic discriminators,
  so `System.Text.Json` can't deserialize them.
- Classes have only parameterized constructors (some throw), blocking
  deserialization.
- **Boundary Conditions block is empty**; **Temperature load is missing**.
- `decimal`/`double` mixing; naming/type drift vs. the draft schemas.

Confirmed direction: full refactor to a serializable model, **camelCase** JSON,
**ID-based integer references** into shared lists on the root.

## Target design

### Root: `FemexModel.cs` (new, namespace `griffel_femex`)
Single serializable root with shared lists (each entity has an integer id):
```
Units? Units                      // optional metadata (length/force convention)
List<Level>    Levels
List<Node>     Nodes
List<Section>  Sections           // bar sections, "stored separately"
List<Material> Materials
List<Bar>      Bars
List<Plate>    Plates
List<LoadCase> LoadCases
List<Load>     Loads
List<Support>  Supports
List<Hinge>    Hinges
```
> **Extended by `FEMEX_Gridlines.md`:** the root also carries `List<Grid> Grids` and
> `List<int> DefaultGridIds`, both ahead of `Levels`. Grids are architectural
> setting-out annotation — non-structural, referenced by a level rather than
> referencing one.

> **Extended by `FEMEX_BarLocalAxes_LoadDirection.md`:** the root's first member is
> `string? SchemaVersion`, so it is the first key in the file. It is deliberately
> uninitialized — `null` is how a file written before load directions existed is
> told apart from one written after, and that distinction changes what a magnitude
> means. `ToJson()` stamps `CurrentSchemaVersion` when it is null. The global frame
> is stated too: right-handed, **Z up**, `Level.AbsoluteElevation` being global Z.

> **Extended by `FEMEX_SelfWeight.md`:** the root's third member is a non-nullable
> `Gravity Gravity` — `dx`, `dy`, `dz` (default `0, 0, −1`) and an `acceleration`
> (default `9.80665`, in the model's own length units) — sitting immediately after
> `Units`, because "which way gravity acts" is the same class of statement as "Z is
> up". Direction and strength are stated **once**, on the root; how much of the
> weight a given load case takes is a dimensionless factor on that case. `Gravity`
> is initialized where `Units` is nullable, because it is consumed rather than
> merely annotated. `CurrentSchemaVersion` is now `"1.2"`, and `ToJson()` restamps
> any version this build has migrated while leaving an unrecognised one alone.

> **Extended by `FEMEX_Identity.md`:** every authored entity implements
> `IIdentified` — one optional `Guid? Uid`, the canonical 36-character string,
> **omitted entirely when null**, because null is a truthful value: this object has
> no round-trip identity, which is the honest state of a hand-authored file. It is
> what lets a receiving program recognise on re-import the object it exported and
> *merge* rather than duplicate it. The integer id stays the in-file reference key
> — `Bar.StartNodeId`, `Plate.NodeIds`, `AreaLoad.PlateId` are untouched — so this
> is SAF's three-layer answer (name / id / uid), not a replacement for either of
> the other two. Uniqueness is **model-wide**: a uid naming two objects, and the
> nil uid written out, are both errors. The mesh carries none, being regenerated
> wholesale, and neither does `Gridline`, whose identity is already its required
> `Label`. FEMEX never mints a uid on save; `FemexModel.AssignMissingUids()` is a
> call the caller makes, and it never overwrites one that is already there.
> `CurrentSchemaVersion` is now `"1.3"`.

> **Extended by `FEMEX_Metadata.md`:** the root's second member is a nullable
> `FileMetadata? Metadata` — `producer`, `producerVersion`, `projectName`,
> `createdAt` — so it is the second key in the file, immediately after
> `schemaVersion` and ahead of `Units`. `ToJson()` does **not** stamp it: a version
> is a statement about the format, which the library knows, and provenance is a
> statement about the caller, which it does not. Every serializable type also
> implements `IExtensible` — one `[JsonExtensionData]` dictionary, so a member from
> a schema this build has never heard of is preserved on read, re-emitted on save
> and named by `Validate()` rather than dropped in silence.
> `UnmappedMemberHandling`, which the interop review asked for, is System.Text.Json
> **8.0** and this project targets `net7.0`; extension data needs no package, no SDK
> and no csproj change, and preserves the payload instead of refusing the file.
> `CurrentSchemaVersion` is now `"1.4"`.

> **Extended by `FEMEX_StandardSections.md`:** a section is **three orthogonal
> layers**, any subset and at least one — an optional `catalogue` naming it in some
> program's library, the `type` discriminator and its dimensions, and an optional
> `properties` stating its resolved stiffness. A receiver takes the richest it can
> act on: *resolve the catalogue name; else build the parametric shape; else build a
> member with the stated stiffness*, so a section is never lost, only degraded — and
> where a property is stated it is authoritative over the parametric one, a
> tabulated area carrying root fillets no idealisation does. FEMEX ships **no
> catalogue rows**: the vocabulary to name any profile and the numbers to survive
> not recognising one, both travelling in the same file. `CurrentSchemaVersion` is
> now `"1.6"` — `"1.5"` added `properties`, the geometry-less `generic` shape and
> `Section.GetArea()`, and `"1.6"` added `ishape`, `channel`, `angle`, `box`,
> `pipe` and `catalogue`, in that order so that a profile 1.6 cannot resolve is
> survivable.

> **Extended by `FEMEX_SAF_Fit_Update_Plan.md` (1.7):** a `Material` also says what
> it **is** and what it can be **designed against**. `MaterialType? Type` — SAF's
> closed six, `Concrete, Steel, Timber, Aluminium, Masonry, Other` — and
> `string? Quality`, the grade as its code writes it (`S235`, `C25/30`), are the two
> columns SAF marks mandatory and FEMEX had no home for at all; the enum/free-text
> split is the same line `SectionManufacture` draws against `SectionCatalogue.Source`.
> `double? ThermalExpansion` is what turns a `TemperatureLoad` into a strain, without
> which the load was a number the receiver could not use — an internal inconsistency,
> not just an omission. `double? ShearModulus` may be stated and is authoritative over
> `E/(2(1+ν))`, the identical stated-wins-over-derived rule `Section.GetArea()`
> already states for area, and the one place a FEMEX material is allowed to
> contradict the isotropic relation. `MaterialProperties? Properties` is SAF's 22
> `Design properties` in three groups — the escape hatch 1.5 opened for sections,
> applied to the other half of the pair, so a value crosses even when the receiver has
> never heard of the grade. `Strength` stays, and from 1.7 on `Properties` is where a
> design value belongs: it says *which* strength, and `Strength` never did. All five
> are nullable with no initializer, so a 1.6 file re-saved as 1.7 gains not one byte.
> `CurrentSchemaVersion` is now `"1.7"`.

Add static helpers using one shared `JsonSerializerOptions`:
- `string ToJson()` / `static FemexModel FromJson(string)`
- `void Save(string path)` / `static FemexModel Load(string path)`
- Options: `PropertyNamingPolicy = CamelCase`, `JsonStringEnumConverter`
  (enums as readable strings), `WriteIndented = true`,
  `DefaultIgnoreCondition = WhenWritingNull`.
- Optional `IEnumerable<string> Validate()` checking referential integrity
  (every referenced id exists) — surfaced but not required for serialization.
  > **Superseded by `FEMEX_Node_Sharing.md`:** `Validate()` yields
  > `ValidationMessage` (severity + text), not bare strings, so it can also report
  > things that are legal FEMEX but suspect.

### Serialization rules applied to every class
- Add a **public parameterless constructor**; convert required fields to
  `{ get; set; }` (or `init`). Keep existing convenience constructors as
  secondary overloads.
- Use `double` everywhere for numeric quantities (drop `decimal`).
- Polymorphic bases get discriminators, e.g.:
  ```
  [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
  [JsonDerivedType(typeof(Rectangle),  "rectangle")]
  [JsonDerivedType(typeof(Circle),     "circle")]
  [JsonDerivedType(typeof(TSection),   "tshape")]
  public abstract class Section { ... }
  ```
  Same pattern for `Load` (`point`/`linear`/`area`/`temperature`).

### Geometry changes (`Geometry/`)
- **`Level.cs`**: `LevelNumber`, `Name`, `AbsoluteElevation`, `RelativeElevation`,
  `IsGround` — all `double`/`{get;set;}`; parameterless ctor.
  > **Extended by `FEMEX_Gridlines.md`:** `Level` also carries `List<int>? GridIds`,
  > left un-initialized because null (inherit the model default) and an empty list
  > (deliberately no grid) mean different things.
- **`Node.cs`**: replace `Level AssociatedLevel` with `int LevelNumber`; `X`, `Y`,
  `VerticalOffset` as `double`. Drop the ctor null-throw; keep
  `GetTotalAbsoluteElevation(FemexModel)` as a lookup helper (not serialized).
  > **Extended by `FEMEX_Node_Sharing.md`:** a node is the model's unit of
  > connectivity — elements are joined where they name the same node number, and
  > only there — so model code shares nodes via `FemexModel.GetOrAddNode`. The
  > format still allows several nodes at one location, which is how a deliberately
  > disconnected joint is written; `Validate()` reports it as a warning.
- **`Element.cs`**: keep abstract base but only shared serializable props:
  `int Id`, `int MaterialId`. Move `RotationAngle` and `SectionId` off the base
  onto `Bar` (rotation of local X is a bar concept; plates derive axes from node
  order). Keep an abstract `IEnumerable<int> GetNodeIds()`.
  > **Superseded by `FEMEX_Plates.md`:** `MaterialId` has moved down onto `Bar`
  > and `Plate`, and plates no longer derive their axes from node order alone —
  > `Plate.LocalAxisAngle` rotates local X about the plate normal.
- **`Bar.cs`**: `int StartNodeId`, `int EndNodeId`, `int SectionId`,
  `double RotationAngle`.
  > **Clarified by `FEMEX_BarLocalAxes_LoadDirection.md`:** `RotationAngle` is a
  > roll of local y and z *about local x*, not a rotation relative to global X —
  > local x is fixed by the two nodes and no angle can change it. The default
  > orientation of y and z, and the substitution for a vertical member, follow the
  > ETABS/SAP convention and are executable as `FemexModel.TryGetBarLocalAxes`.
- **`Plate.cs`**: `List<int> NodeIds` (order matters), `double Thickness`
  (per spec, thickness is a plate property — no shared section needed).
  > **Superseded by `FEMEX_Plates.md`:** `Plate` is a design panel — an outer
  > contour plus subregions — and thickness lives in a shared, reusable
  > `SurfaceProperty` referenced by id, exactly as `Section` works for bars.
  > **Clarified by `FEMEX_BarLocalAxes_LoadDirection.md`:** local z is the outer
  > contour's Newell normal (counter-clockwise seen from above gives +Z), local x
  > is the first chord in the plane turned by `LocalAxisAngle` counter-clockwise
  > about local z, and local y is `ẑ × x̂`. `FemexModel.TryGetPlateLocalAxes` is
  > that rule, and the planarity check now shares its normal.
- **`Sections/`**: keep `Rectangle`, `Circle`; add `int Id` + polymorphism to
  `Section`; add `TSection` (spec says "T-shape, etc."). **Remove `FlatPlate.cs`**
  (plate thickness now lives on `Plate`). Drop the `CalculateArea` hack or keep
  as a real per-section computation.
  > **Superseded by `FEMEX_Plates.md`:** the shared plate-property object that
  > `FlatPlate.cs` was reaching for came back as
  > `Geometry/Surfaces/SurfaceProperty.cs`.
  > **Extended by `FEMEX_StandardSections.md`:** the union is nine types, not three
  > — `rectangle`, `circle`, `tshape`, `ishape`, `channel`, `angle`, `box`, `pipe`
  > and `generic` — with `tapered`, `asymmetric` and `compound` reserved in a doc
  > comment on the base, following the precedent `SurfaceProperty` sets with its
  > unimplemented `variable` and `layered`. `CalculateArea()` was kept, not dropped:
  > it stays geometry-only and abstract, and the new **non-abstract**
  > `GetArea()` beside it — `Properties?.Area ?? CalculateArea()` — is what a
  > consumer wants, and what `FemexModel.SelfWeight.cs` weighs a bar by.
  > `GenericSection` has no geometry, so its `CalculateArea()` is `0.0` and only
  > `GetArea()` is meaningful on it.

### Loads (`Loads/`)
- **`Load.cs`**: add `type` discriminator + derived types; replace
  `LoadCase Case` with `int LoadCaseNumber`.
- Keep `PointLoad`, `LinearLoad`, `AreaLoad` (already id-based).
- **Add `TemperatureLoad.cs`**: target element/node id(s) + uniform temperature
  change `DeltaT` (and optional gradient across depth).
- `LoadCase.cs`, `LoadNature.cs`: keep; ensure parameterless ctor (already present).
  > **Extended by `FEMEX_LoadCombinations.md`:** the root also carries
  > `List<LoadCombination> LoadCombinations`, after `Loads`. A combination is a
  > factored sum of load cases — `(loadCaseNumber, factor)` terms, a limit state
  > and a combination type — in its own number space. Terms name load cases only,
  > never other combinations, so the structure is flat.
  > **Extended by `FEMEX_BarLocalAxes_LoadDirection.md`:** `AreaLoad` and
  > `LinearLoad` share an abstract `DistributedLoad` carrying the three facts a
  > magnitude alone cannot state — `CoordinateSystem`, `Direction` (with
  > `Dx`/`Dy`/`Dz` for `Vector`) and `Projected`, SAF's factoring rather than
  > RFEM's fused enum. The sign lives in the magnitude and global +Z is up, so a
  > gravity load is negative. `LinearLoad` also gains an optional `BarId`: the host
  > whose local axes a local direction resolves against.
  > `FemexModel.TryGetLoadDirection` resolves the lot to one global unit vector.
  > **Extended by `FEMEX_SelfWeight.md`:** `LoadCase` gains a non-nullable
  > `double SelfWeightFactor`, written on every case in every file — `0` is the
  > positive statement "no self-weight here", which is precisely what the format
  > could not say before. `1.0` is normal gravity along the root's `Gravity` vector.
  > No load case is reserved and `LoadNature.Dead` keeps no special meaning: any
  > case may carry the factor, and more than one doing so is a warning rather than
  > an error. A case's own loads are *additional to* its self-weight, never a
  > substitute for it — so a case with no entries in the `loads` array is not an
  > empty case. `Material.UnitWeight` (γ) became `Material.Density` (ρ, mass per
  > unit volume); `FemexModel.SelfWeight.cs` states γ = ρ·g, a bar's γ·A and a
  > plate's γ·t as code, each as a global force vector because a wall's weight is
  > not along its normal.
  > **Extended by `FEMEX_Identity.md`:** `Load` gains an `int Id`, in its own id
  > space beside `Support.Id` and `Hinge.Id` rather than in the shared element
  > space — a load is not an element. Loads were the only authored entity with no
  > key at all, having only list position and an optional label, and one carrying a
  > uid but no id would have been the format's sole exception. Nothing references a
  > load, so this is not a foreign key: it exists so a load can be named in a
  > message. Reading a file written before 1.3 numbers its loads **1..N in list
  > order** — the only identity a load ever had — gated on the declared version, so
  > a duplicated load id in a current file stays an error. `Section.Name`,
  > `SurfaceProperty.Name`, `Material.Name` and `LoadCase.Label` stay `string?`,
  > and a blank or duplicated one is a **warning** worded for the programs that key
  > by name; the interop review wanted them required, and this is the half-step.

### Boundary Conditions (`BoundaryConditions/` — new files)
Per the description:
- **`Support.cs`**: `int Id`, `SupportTarget Target` (enum Point/Linear/Area),
  `List<int> NodeIds`, and six DOF entries (`Ux,Uy,Uz,Rx,Ry,Rz`) each a
  `Restraint`.
- **`Restraint.cs`**: `bool Fixed` (infinite stiffness) + `double? Stiffness`
  (finite value; null = free) — captures "infinite or finite stiffness".
- **`Hinge.cs`**: `int Id`, `HingeTarget Target` (Point/Linear), `List<int>
  NodeIds`, `int ElementId` + which end/edge, and per-DOF `Release`
  (full/partial via a bool + optional residual stiffness) — captures
  "full or partial", "belong to element ends or edges".
- Enums `SupportTarget`, `HingeTarget` in this namespace.

### Cleanup
- **Delete `Class1.cs`** (template placeholder).
- Register `BoundaryConditions/` files (folder currently empty in the `.csproj`
  `<Folder Include>` — remove that stub once real files exist).

## Critical files
- New: `FemexModel.cs`, `BoundaryConditions/{Support,Restraint,Hinge}.cs`,
  `Geometry/Sections/TSection.cs`, `Loads/TemperatureLoad.cs`.
- Modified: `Geometry/{Level,Node,Element,Bar,Plate}.cs`,
  `Geometry/Sections/{Section,Rectangle,Circle}.cs`, `Materials/Material.cs`,
  `Loads/{Load,PointLoad,LinearLoad,AreaLoad,LoadCase}.cs`.
- Removed: `Class1.cs`, `Geometry/Sections/FlatPlate.cs`.

## Verification
The project is a library (no entry point), so add a lightweight round-trip check:
1. Add an xUnit test project `griffel-femex.Tests` (or a throwaway console under
   `#if DEBUG`) that:
   - Builds a small `FemexModel` (2 levels, a few nodes, one bar + one plate,
     a material, a section, a load case with each load type, one support and one
     hinge).
   - `ToJson()` → assert JSON is camelCase and contains type discriminators.
   - `FromJson(json)` → assert key fields survive the round-trip and
     `Validate()` returns no errors.
2. Run `dotnet build` then `dotnet test` (or run the console harness).
3. Manually eyeball the emitted JSON against the draft schemas in
   `FEMEX description.docx` for shape/naming parity.
