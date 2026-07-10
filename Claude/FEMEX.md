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
Add static helpers using one shared `JsonSerializerOptions`:
- `string ToJson()` / `static FemexModel FromJson(string)`
- `void Save(string path)` / `static FemexModel Load(string path)`
- Options: `PropertyNamingPolicy = CamelCase`, `JsonStringEnumConverter`
  (enums as readable strings), `WriteIndented = true`,
  `DefaultIgnoreCondition = WhenWritingNull`.
- Optional `IEnumerable<string> Validate()` checking referential integrity
  (every referenced id exists) — surfaced but not required for serialization.

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
- **`Node.cs`**: replace `Level AssociatedLevel` with `int LevelNumber`; `X`, `Y`,
  `VerticalOffset` as `double`. Drop the ctor null-throw; keep
  `GetTotalAbsoluteElevation(FemexModel)` as a lookup helper (not serialized).
- **`Element.cs`**: keep abstract base but only shared serializable props:
  `int Id`, `int MaterialId`. Move `RotationAngle` and `SectionId` off the base
  onto `Bar` (rotation of local X is a bar concept; plates derive axes from node
  order). Keep an abstract `IEnumerable<int> GetNodeIds()`.
- **`Bar.cs`**: `int StartNodeId`, `int EndNodeId`, `int SectionId`,
  `double RotationAngle`.
- **`Plate.cs`**: `List<int> NodeIds` (order matters), `double Thickness`
  (per spec, thickness is a plate property — no shared section needed).
- **`Sections/`**: keep `Rectangle`, `Circle`; add `int Id` + polymorphism to
  `Section`; add `TSection` (spec says "T-shape, etc."). **Remove `FlatPlate.cs`**
  (plate thickness now lives on `Plate`). Drop the `CalculateArea` hack or keep
  as a real per-section computation.

### Loads (`Loads/`)
- **`Load.cs`**: add `type` discriminator + derived types; replace
  `LoadCase Case` with `int LoadCaseNumber`.
- Keep `PointLoad`, `LinearLoad`, `AreaLoad` (already id-based).
- **Add `TemperatureLoad.cs`**: target element/node id(s) + uniform temperature
  change `DeltaT` (and optional gradient across depth).
- `LoadCase.cs`, `LoadNature.cs`: keep; ensure parameterless ctor (already present).

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
