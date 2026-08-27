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

> **Extended by `FEMEX_SAF_Fit_Update_Plan.md` (1.8):** the model's **units are typed**
> and a restraint has a **direction**. `Units` is five enums — `LengthUnit`,
> `ForceUnit`, `TemperatureUnit`, `AngleUnit`, `MassUnit` — where 1.7 had two free-text
> strings in which `"length": "banana"` round-tripped clean, which is the only defect
> an annotation can have. The typed spellings take **new JSON keys**, `lengthUnit` and
> `forceUnit`: `"m"` and `"Metre"` cannot share a key without a custom converter and
> there is not one in this repository, so this is the **first bump to rename a key** and
> the only non-additive change in the format's history. The old spellings bind to
> getter-less properties, migrate once through `OnDeserialized()`, and are reported by
> `Validate()`; text naming no unit is **dropped and named**, because carrying it
> forward is the defect the bump exists to end. The block is still pure annotation —
> nothing in the library converts by it — and it deliberately does **not** supply SAF's
> mandatory `Model.System of units`, which is one `Metric | Imperial` flag about a whole
> model where five independent enums permit `Metre` with `Kip`.
> `Restraint.Sense` — a `RestraintSense?`, `Both | CompressionOnly | TensionOnly`,
> null meaning both — crossed with `Fixed` and `Stiffness` reaches **seven of SAF's
> eight** translation states, where 1.7 reached three: an uplift-free bearing and a
> tension-only tie were rigid supports, and a model carrying one opened, validated,
> solved and was wrong. The eighth, `Non linear`, is a stiffness curve rather than a
> state and is recorded as unmapped. And `Restraint.Stiffness` finally says **what it is
> measured against**: a total spring at a `Point`, per unit length along a `Linear`, a
> **bedding modulus per unit area** — SAF's Winkler `C1`, force/length³ — over an `Area`,
> with Pasternak `C2` unmapped. That is documentation plus one warning and no schema
> change, and it closes the ambiguity in which two adapters could read one file and
> differ by a factor of the slab area. `CurrentSchemaVersion` is now `"1.8"`.

> **Extended by `FEMEX_SAF_Fit_Update_Plan.md` (1.9):** the **load side** gains the three
> things one real SAF workbook showed it was missing, plus provenance. The root carries
> `List<LoadGroup> LoadGroups` immediately before `LoadCases`, and a `LoadCase` names one
> through `int? LoadGroupId` — SAF's mandatory `Load group` column, and an entity rather
> than a string because a group carries `LoadGroupRelation`
> (`Standard | Exclusive | Together`), a statement about a *set* of cases that
> `LoadNature` cannot make. `LoadGroupType` and `LoadNature` then say the same thing
> twice and can disagree, so `Validate()` checks them against a stated compatibility map
> — a second source of truth designed against rather than discovered.
> `Plate` and `PlateRegion` gain `LoadDistribution? Distribution` —
> `SurfaceLoadSpanning`, a frame rotation, and an optional list of receiving members —
> **on the panel and never on the load**, because a slab spans one way for every load it
> carries and two loads must not be able to disagree about it; a 1.8 file's one-way slab
> read as two-way put half its load on the wrong beams. `TemperatureLoad` gains signed
> `GradientY`/`GradientZ` along the element's own local axes, and 1.6's unsigned
> `gradientPerDepth` becomes a getter-less shim migrated once and reported as a
> **reinterpretation, not a rename** — the number keeps its value and changes its
> meaning, which is worse in kind than 1.8's dropped free text.
> A load may also sit **at a station along a member** rather than on a node —
> `PointLoad.BarId`/`Position`, `LinearLoad.StartPosition`/`EndPosition`, relative from
> the start node — which preserves topology, so a round trip stays equivalent where
> minting a node or snapping to an end would not. And `IIdentified` gains
> `Guid? ParentUid`: **provenance and nothing more**, no traversal and no behaviour, but
> it is what lets a chorded arc's pieces point at the arc they came from, which turns
> the format's one non-reversible loss into a recoverable one. SAF carries the same
> column on seventeen of its forty-three sheets, so this is a pass-through rather than an
> invention. `CurrentSchemaVersion` is now `"1.9"`.

> **Extended by `FEMEX_SAF_Fit_Update_Plan.md` (1.10):** a **member** can finally say how
> it behaves and where it sits. `Bar` gains `BarBehaviour? Behaviour` — SAF's
> `Standard | AxialOnly | CompressionOnly | TensionOnly`, populated on every row of the
> SAF reference workbook and axial-only on four fifths of them, so this is the opposite
> of a corner case; `BarAlignment? Alignment`, the nine system lines, which SAF marks
> mandatory and which is *not* always the centroid; `BarEccentricity? Eccentricity`,
> eight nullable offsets keeping SAF's split between the **structural** offset that moves
> the picture and the **analysis** offset that moves the answer, because a receiver that
> fuses them produces geometry that looks right and stiffness that is wrong; and
> `int? EndSectionId`, a single linear taper. That last one **downgrades** SAF's varying
> member to *Approximated* rather than closing it — SAF states spans, and the corpus's one
> example has three — so a rafter haunched at both ends still arrives wrong, now with a
> message attached. `SectionId` stays the prismatic fallback, and the `tapered`
> discriminator reserved on `Section` stays reserved: a taper is a property of the member,
> not a kind of section. `Support` gains `BarId`/`Position`/`EndPosition` and `Hinge` a
> `Position`, completing 1.9's positions for the boundary conditions — a hinge needs no
> bar reference because its `ElementId` already is one. Every one of the six is nullable
> with no initializer, so a 1.9 file re-saved as 1.10 gains not one byte.
> `CurrentSchemaVersion` is now `"1.10"`.

> **Extended by `FEMEX_HingeAxes_Summary.md` (1.10, in documentation and two helpers):** a hinge
> finally says **which axes** its six releases are in. They are local, never global, and which local
> frame is a function of what the hinge sits on: a hinge on a **bar** is in that bar's own axes, roll
> included, so `ux` is the axial release and `rz` the one that pins a beam end; a hinge on a **plate
> edge** is in the *edge's* frame — x along the edge from `EdgeStartNodeId` to `EdgeEndNodeId`, z the
> **panel's** normal, y = ẑ × x̂, which for an edge in contour order points into the panel; a hinge on
> a **mesh face** is the same edge rule over the face's own nodes, indexed by `EndOrEdgeIndex`.
> `FemexModel.TryGetHingeLocalAxes` is that whole rule executable, `TryGetEdgeLocalAxes` its edge
> half, beside `TryGetBarLocalAxes` and `TryGetPlateLocalAxes`. **No schema change and no
> coordinate-system flag** — the frame is not a choice a hinge gets to make, which is what tells this
> apart from `FEMEX_Interop_Review.md` §5.6, where a `Support` genuinely needs one. Before this the
> only executable statement that a release was local at all was the tension-only bar rule in
> `ValidateBarCompleteness`, which reads `Ux` as the axial DOF; one rule inferring a convention is not
> a convention. `CurrentSchemaVersion` stays `"1.10"`.

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
