# FEMEX Refactor — Implementation Summary

Refactored FEMEX into a single JSON-serializable model per `Claude/FEMEX.md`.
Clean build (0 warnings, 0 errors) and all 3 round-trip tests pass.

> **Superseded in part.** The plate model described below was replaced by the
> design-panel model in `Claude/FEMEX_Plates.md` / `FEMEX_Plates_Summary.md`.
> Statements about `Plate.Thickness`, `Element.MaterialId` and plates deriving
> their axes from node order no longer hold; corrections are marked inline.
>
> `Validate()` has since become `IEnumerable<ValidationMessage>` rather than
> `IEnumerable<string>` — see `Claude/FEMEX_Node_Sharing_Summary.md`.

## New root & metadata
- **`FemexModel.cs`** — root container with flat lists (`Levels, Nodes, Sections,
  Bars, Plates, Materials, LoadCases, Loads, Supports, Hinges`), a shared
  `JsonSerializerOptions` (camelCase, `JsonStringEnumConverter`, `WriteIndented`,
  ignore-nulls), `ToJson`/`FromJson`/`Save`/`Load`, and a `Validate()`
  referential-integrity check.
- **`Units.cs`** — optional length/force metadata.

## Geometry
Converted every class to parameterless-ctor + `{get;set;}`, `double` everywhere,
and replaced object references with integer ids:
- **`Node`** now carries `LevelNumber` (was `Level` ref);
  `GetTotalAbsoluteElevation(FemexModel)` resolves via lookup.
- **`Element`** is a polymorphic base with only `Id`/`MaterialId` and abstract
  `GetNodeIds()`; `RotationAngle`/`SectionId` moved onto `Bar`.
  *(Superseded: `MaterialId` has since moved down onto `Bar` and `Plate`, because
  a plate that is an opening has no material.)*
- **`Bar`** uses `StartNodeId`/`EndNodeId`/`SectionId`; **`Plate`** uses
  `List<int> NodeIds` + its own `Thickness`.
  *(Superseded: `Plate` is now a design panel with an outer contour, subregions and
  a `SurfacePropertyId`; `Thickness` is gone.)*
- **`Section`** made polymorphic (`rectangle`/`circle`/`tshape`) with `Id`;
  added **`TSection`**; removed `FlatPlate.cs`.

## Loads
- **`Load`** is polymorphic (`point`/`linear`/`area`/`temperature`) with
  `LoadCaseNumber`.
- Added **`TemperatureLoad`** (element ids, `DeltaT`, optional gradient).

## Boundary Conditions (new folder)
- **`Support`** — six-DOF `Restraint` (fixed / finite-stiffness / free).
- **`Hinge`** — six-DOF `Release` (rigid / full / partial) + `ElementId` /
  `EndOrEdgeIndex`.
- **`SupportTarget`** / **`HingeTarget`** enums.

## Cleanup
- Deleted `Class1.cs`.
- Dropped the empty `BoundaryConditions` folder stub from the `.csproj`.

## Verification
Added **`griffel-femex.Tests`** (xUnit) with a round-trip test that:
- builds a full sample model,
- asserts camelCase + discriminators in the JSON,
- confirms polymorphic types and key fields survive `FromJson`,
- and that `Validate()` is empty.

The emitted JSON was eyeballed against the docx draft schemas — shape and naming
match.

## Deviations worth flagging
1. **Target framework bumped net6.0 → net7.0.** The `[JsonPolymorphic]` /
   `[JsonDerivedType]` attributes the plan specifies don't exist in .NET 6's
   `System.Text.Json`. Installed SDK is 7.0.302, so this was the clean path
   (vs. hand-writing custom converters).
2. **Bar/Plate discriminators aren't emitted.** Because the model stores them in
   concrete-typed lists (`List<Bar>`, `List<Plate>`), `System.Text.Json` only
   writes the `Element` discriminator when serializing through the base type.
   The polymorphic attributes are kept on `Element` (harmless, enables
   `List<Element>` use later) and this is noted in the test. Sections and Loads
   use base-typed lists, so their discriminators appear as expected.
