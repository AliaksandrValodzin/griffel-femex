# Redefining `Plate` — complex slab geometry with voids and thickness zones

> **Step 0 (repo convention):** copy this document to `Claude/FEMEX_Plates.md` alongside the
> existing `Claude/FEMEX.md` / `Claude/FEMEX_Summary.md`, and write a
> `Claude/FEMEX_Plates_Summary.md` when implementation completes.

## Context

`Geometry/Plate.cs` today is a flat N-gon of node ids plus a single `double Thickness`. It
cannot express a real floor: no voids, no thickness zones, no drop panels, no wall openings.
It is also doing two incompatible jobs at once — it is both the **authored design panel** and
the **finite element**. `Examples/Example1.femex` shows the cost: 60 hand-pre-meshed quads,
88 area loads that each duplicate one quad's node list, and a core shaft expressed by simply
*omitting* a quad (id 3105 is skipped).

Research into how the commercial packages describe slabs validates the proposed direction:

- **RAM Concept** — *"slabs are defined by a collection of slab areas and openings with
  arbitrary overlapping polygonal boundaries. Each slab area defines material, thickness and
  surface elevation properties"*, plus *"an integer **priority** determines which slab area or
  opening takes precedence where two or more slab areas overlap."* This is the model adopted here.
- **ETABS / SAFE** — same overlay idea with priority implicit by type (`Opening > Stiff > Drop
  > Slab`, then innermost-wins, then smallest-wins). Openings are a distinct object, never a
  zero thickness. A `None` area contributes no stiffness but still carries load.
- **Dlubal RFEM** — openings are child objects of a surface; *"finite elements are not generated
  and surface loads are not applied to the openings."* Thickness is a **typed object**
  (Constant / Variable / Layers), not a bare number.
- **SAF** (the open Nemetschek/SCIA interchange schema, FEMEX's closest peer) — a
  `StructuralSurfaceMember` carries `Thickness type`, `System plane at {Bottom|Centre|Top}`,
  analysis Z eccentricity, behaviour, and LCS; openings live in a separate
  `StructuralSurfaceMemberOpening` keyed to a parent 2D member.

Four decisions confirmed with the user:
1. **Two tiers** — `Plate` becomes a purely authored design panel; an optional `Mesh` block
   holds generated elements with backlinks. No mesher is written now; the block is data-only.
2. **Contours are node-id lists, straight segments** (curves as chords), matching SAF and the
   existing id-graph architecture.
3. **Integer priority with free overlap** (RAM Concept), not strict nesting.
4. **Explicit `PlateRegionKind { Structural, Opening, LoadOnly }`**, *not* thickness = 0.

Intended outcome: a slab with any number of voids, drop panels and thickness zones is one
`Plate`; walls with door/window openings fall out for free; `Example1.femex` shrinks from 60
plates + 88 loads to 20 plates + 8 loads with identical semantics.

---

## Data model

### New types

**`Geometry/Surfaces/SurfaceProperty.cs`** — abstract, polymorphic, the plate counterpart of
`Geometry/Sections/Section.cs`. New folder/namespace because `Section`'s own doc scopes it to
bar cross-sections.

| Member | Type | Notes |
| --- | --- | --- |
| `Id` | `int` | referenced by `Plate` / `PlateRegion` / `MeshFace` |
| `Name` | `string?` | e.g. `"SLAB-220"` |
| `GetNominalThickness()` | `abstract double` | parallels `Section.CalculateArea()` |

`[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` + `[JsonDerivedType(typeof(ConstantThickness), "constant")]`.
Reserve `"variable"` (3 nodes, linear interpolation, per RFEM) and `"layered"` **in the XML doc
only** — do not implement.

**`Geometry/Surfaces/ConstantThickness.cs`** — `double Thickness`.

Material stays on the plate/region, *not* on the property — exactly as `Bar` references
`SectionId` and `MaterialId` independently. Lets 0.22 be reused with C30 and C40.

**`Geometry/Plate.cs`** (rewritten)

| Member | Type | Notes |
| --- | --- | --- |
| `Id` | `int` (inherited) | element-id space, shared with `Bar` and `MeshFace` |
| `Name` | `string?` | |
| `NodeIds` | `List<int>` | FK → `Node.NodeNumber`; outer contour, ordered, closes implicitly |
| `Kind` | `PlateRegionKind` | default `Structural` |
| `SurfacePropertyId` | `int?` | FK → `SurfaceProperty.Id`; null only for `Opening` |
| `MaterialId` | `int?` | FK → `Material.Id`; **moved down from `Element`** |
| `Behaviour` | `PlateBehaviour` | default `Shell` |
| `Alignment` | `SurfaceAlignment` | default `Centre` |
| `SurfaceOffset` | `double` | along the plate **normal**, never global Z |
| `LocalAxisAngle` | `double` | degrees about the normal; unrotated local X = node[0]→node[1] |
| `Regions` | `List<PlateRegion>` | owned children, ids plate-scoped |

`GetNodeIds()` yields the outer contour then every region contour.

**`Geometry/PlateRegion.cs`** — `Id` (unique within the plate), `Name`, `NodeIds`, `Kind`,
`SurfacePropertyId?` (null = inherit), `MaterialId?` (null = inherit), `Priority`,
`Alignment?`, `SurfaceOffset?`.

**`Geometry/PlateRegionKind.cs`** — `Structural | Opening | LoadOnly`.
`Opening` = no elements, no surface load (RFEM/ETABS). `LoadOnly` = no stiffness, still carries
load (ETABS `None` / INDUCTA independent UDL areas).

**`Geometry/PlateBehaviour.cs`** — `Shell | Plate | Membrane | CompressionOnly`.
SAF's `Orthotropic` is **rejected here**: directionality belongs on a future orthotropic
`SurfaceProperty`, and having it in both places creates a contradiction state.

**`Geometry/SurfaceAlignment.cs`** — `Bottom | Centre | Top`. For a non-horizontal plate these
mean the −normal / +normal face; say so in the doc comment.

**`Mesh/FemexMesh.cs`** — `Generator` (`string?`), `GeneratedAt` (`string?`, ISO-8601 free text,
not `DateTime` — keeps string-contains tests stable and matches the free-text `Units` convention),
`List<MeshNode> Nodes`, `List<MeshFace> Faces`.

**`Mesh/MeshNode.cs`** — `Id` (own id space), `X`, `Y`, `Z` (**absolute**, same datum as
`Level.AbsoluteElevation`), `SourceNodeId?` (FK → `Node.NodeNumber`, null for generated nodes).
Deliberate deviation from `LevelNumber + VerticalOffset`: a generated interior node on a warped
or vertical panel has no natural level.

**`Mesh/MeshFace.cs`** — `Id` (**shared element-id space**), `NodeIds` (FK → `MeshNode.Id`, 3 or 4),
`PlateId`, `RegionId?` (null = base panel), plus the resolved cache `SurfacePropertyId?`,
`MaterialId?`, `Thickness?`, `SurfaceOffset`.

> **Rule to document:** backlinks are authoritative; resolved fields are a mesher-written cache.
> `Validate()` checks the resolved ids *exist*; it does not re-run priority resolution.

### The resolution rule — put this in `PlateRegion`'s XML doc before writing other code

Regions may overlap each other and may hang over the outer contour (the overhang is clipped).
At every point inside the outer contour the governing region is:

1. highest `Priority` wins — the base panel behaves as `int.MinValue`, so any region beats it;
2. on a tie, `Opening` > `LoadOnly` > `Structural`;
3. on a further tie, the region later in `Plate.Regions` wins.

### Modified existing types

- **`Geometry/Element.cs`** — remove `MaterialId` (now nullable on `Plate`, non-nullable on `Bar`).
- **`Geometry/Bar.cs`** — gains `int MaterialId`.
- **`Loads/AreaLoad.cs`** — add `int? PlateId`, `int? RegionId`; `NodeSequence` becomes
  `List<int>?`. Exactly one targeting form per load. Fixes the retarget-on-edit bug while
  keeping free-polygon patch loads.
- **`BoundaryConditions/Support.cs`** — add `int? PlateId`, `int? RegionId`; keep `NodeIds`
  (Example1's `Area` support sits on nodes with no plate — the node form must survive).
- **`BoundaryConditions/Hinge.cs`** — keep `EndOrEdgeIndex` but restrict it to bars in its doc;
  add `int? RegionId`, `int? EdgeStartNodeId`, `int? EdgeEndNodeId`. Naming an edge by its two
  nodes survives contour editing; an index does not.
- **`FemexModel.cs`** — becomes `partial`; add `List<SurfaceProperty> SurfaceProperties` and
  `FemexMesh? Mesh` (declared last so `"mesh"` sorts last in JSON, and omitted entirely when null).

---

## Resulting JSON

```json
{
  "surfaceProperties": [
    { "type": "constant", "thickness": 0.25, "id": 1, "name": "SLAB-250" },
    { "type": "constant", "thickness": 0.45, "id": 2, "name": "DROP-450" }
  ],
  "plates": [
    {
      "name": "L1 slab",
      "nodeIds": [101, 105, 120, 116],
      "kind": "Structural",
      "surfacePropertyId": 1,
      "materialId": 1,
      "behaviour": "Shell",
      "alignment": "Top",
      "surfaceOffset": 0,
      "localAxisAngle": 0,
      "regions": [
        { "id": 1, "name": "Drop panel at C2", "nodeIds": [501, 502, 503, 504],
          "kind": "Structural", "surfacePropertyId": 2, "priority": 10, "surfaceOffset": -0.2 },
        { "id": 2, "name": "Stair void", "nodeIds": [107, 108, 113, 112],
          "kind": "Opening", "priority": 20 }
      ],
      "id": 3001
    }
  ],
  "mesh": {
    "generator": "griffel-mesher 0.1",
    "nodes": [ { "id": 1, "x": 0, "y": 0, "z": 103.5, "sourceNodeId": 101 } ],
    "faces": [ { "id": 3100, "nodeIds": [1, 2, 3, 4], "plateId": 3001,
                 "surfacePropertyId": 1, "materialId": 1, "thickness": 0.25, "surfaceOffset": 0 } ]
  }
}
```

The opening region omits `surfacePropertyId` / `materialId` / `alignment` / `surfaceOffset` —
all null, all suppressed by the existing `DefaultIgnoreCondition = WhenWritingNull`. Derived-class
properties precede base-class ones, so `"id"` lands after `"regions"`, exactly as today's plates
emit `"nodeIds", "thickness", "id", "materialId"`.

## Walls fall out for free

Contours carry no level constraint, and Example1 already has 16 vertical panels (e.g. plate 4100
= nodes 7, 8, 108, 107 spanning levels 0→1). A door or window in a shear wall is just a
`PlateRegion` with `Kind = Opening`. Two things need saying in doc comments: `SurfaceOffset` moves
a wall *horizontally* (it is normal-relative), and `Alignment = Bottom|Top` means −normal/+normal.
The one genuinely new geometric concern is that four nodes on two levels are easily non-coplanar —
hence the coplanarity check below.

---

## `Validate()` additions

Move the method to a new `FemexModel.Validation.cs` (`public partial class FemexModel`) and split
it into private `IEnumerable<string>` helpers behind an unchanged public entry point — the
alternative is a ~250-line single iterator. Contract is unchanged: deferred, non-throwing, one
human-readable message per problem.

Reference/id checks (land these first, they need no geometry):

1. **Duplicate ids** — currently unchecked *everywhere*: `LevelNumber`, `NodeNumber`, `Section.Id`,
   `SurfaceProperty.Id`, `Material.Id`, `LoadCase.Number`, `Support.Id`, `Hinge.Id`, `MeshNode.Id`.
2. **Element-id collisions** across `Bars` ∪ `Plates` ∪ `Mesh.Faces`.
3. **Contours** (identical rules for the plate contour and every region contour): all node ids
   exist; `Count >= 3`; no repeated node within one contour.
4. **Property/material references** resolve when non-null, on `Plate`, `PlateRegion`, `MeshFace`,
   plus `Bar.MaterialId` moved down from `Element`.
5. **Kind rules** — `Structural` with no effective surface property or material is an error;
   `Opening` carrying a surface property or material is an error. `LoadOnly` is deliberately
   unchecked (it may legitimately carry thickness for self-weight).
6. **Region ids** unique within the owning plate.
7. **Mesh integrity** — face node ids resolve and number 3 or 4; `PlateId` resolves; `RegionId`
   resolves *on that plate*; `SourceNodeId` resolves; no face on an `Opening`.
8. **`AreaLoad`** — not both `PlateId` and `NodeSequence`, not neither; `RegionId` requires
   `PlateId` and must belong to it; `NodeSequence.Count >= 3`.
9. **`Support`** — `PlateId`/`RegionId` resolve; `Target == Area` needs a plate or nodes;
   `Target != Area` must not set `PlateId`.
10. **`Hinge`** — bar target ⇒ `EndOrEdgeIndex ∈ {0,1}` and no edge node ids; plate target ⇒
    `RegionId` resolves, edge node ids both set or both null, both exist, **and adjacent in the
    referenced contour**.
11. **`TemperatureLoad.ElementIds`** now resolves against `Bars ∪ Plates ∪ Mesh.Faces`.

Geometric checks (land last, behind their own tests, so they can be dropped independently):

12. **Coplanarity** of every contour. Compute the normal with Newell's method (robust for
    non-convex contours; the z-term is `(x[i] − x[j]) · (y[i] + y[j])`), bail out as *degenerate
    and skip* if `|n| < 1e-12`, then measure `max |(pᵢ − centroid) · n̂|` against
    `1e-6 × contour AABB diagonal` with an absolute floor of `1e-9`. Skip if any node's level is
    unknown. Message quotes both the deviation and the tolerance.
13. **Priority collisions** — two regions of one plate with the same `Priority`, same `Kind`, and
    overlapping 3D AABBs (3D so the same code serves walls; small tolerance so edge-touching
    regions stay quiet). Knowingly a heuristic: false-positives on L-shapes whose boxes overlap
    but whose areas don't, never false-negatives. Word it as a warning — rule 3 does make the
    outcome deterministic, it just probably isn't what the author meant. If it proves noisy,
    delete it; nothing depends on it.

---

## Migrating `Examples/Example1.femex`

No format version, no external consumers, 3 commits of history — hand-rewrite with a throwaway
script rather than shipping a migrator (a mechanical migrator would emit 44 degenerate one-quad
plates, strictly worse than the semantic upgrade).

| Today | After |
| --- | --- |
| 44 slab quads, `t=0.22`, ids 3100–3411, 11 per level on levels 1–4 (a 24×18 rectangle on a 6×6 grid with the core cell omitted — id 3105 is deliberately skipped) | **4 `Plate`s**, ids 3001–3004, contour = the level's 4 corner nodes (L1: `101,105,120,116`), one region `{id:1, "Core shaft", nodeIds:[107,108,113,112], kind:"Opening", priority:10}` |
| 16 wall quads, `t=0.30`, ids 4100–4403 | **16 `Plate`s, ids and contours unchanged**, `surfacePropertyId: 2` — walls stay 1:1 design panels |
| — | 2 `SurfaceProperty`: `{constant, 1, "SLAB-220", 0.22}`, `{constant, 2, "WALL-300", 0.30}` |
| — | `mesh` block: 44 `MeshFace`s **keeping ids 3100–3411**, 80 `MeshNode`s reusing the slab design nodes. The existing quads already form a conforming mesh, so no new nodes. |
| 88 area loads, each `nodeSequence` matching one quad (verified 88/88) | **8** loads: `plateId` 3001–3004 × load cases 1 and 2 |
| temperature load `elementIds: [3400…3410]` | **unchanged** — those are `MeshFace` ids now |
| support 2 (`Area`, nodes 7,8,13,12 at level 0, no plate) | **unchanged** |
| 20 hinges, 136 bars | unchanged (bar JSON key order shifts only) |

Two facts worth putting in the commit message: the old 88 loads covered only the 11 quads per
level and so already **excluded the core** — one whole-panel load on a plate with an `Opening`
region reproduces that exactly, independently confirming "openings carry no surface load"; and
putting mesh faces in the element-id space keeps the temperature load byte-identical.

---

## Tests

Add to `griffel-femex.Tests.csproj` so the regression test can find the example:

```xml
<ItemGroup>
  <None Include="..\Examples\Example1.femex" Link="Examples\Example1.femex"
        CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Extend `BuildSampleModel()` in `RoundTripTests.cs`: two `ConstantThickness` properties; extra
nodes for a drop panel, a void and a wall; plate 1 = contour with a `Structural` region
(priority 10, offset −0.2) and an `Opening` region (priority 20); plate 2 = a wall spanning
levels 0→1 with `Behaviour = Membrane`; a `Mesh` with 4 nodes and 1 face; the `AreaLoad`
retargeted to `PlateId = 1`.

Three existing assertions break and must be updated — `plate.Thickness` (resolve through
`SurfaceProperties` instead), the `new Plate(...)` ctor signature, and the
`NodeSequence = { 1,2,3,4 }` collection-initializer (stops compiling once the list is nullable).

New facts: `SurfaceProperty_IsPolymorphic`, `Plate_Regions_RoundTrip`,
`Opening_HasNoThicknessOrMaterial`, `PlateEnums_SerializeAsStrings`, `Mesh_IsOmitted_WhenNull`,
`Mesh_RoundTrips_WithBacklinks`, `WallPlate_SpansTwoLevels_AndValidates`, and one
`Validate_Reports…` fact per new check (opening-with-material, priority collision, non-coplanar
contour, element-id collision, area load with both targets, hinge edge not adjacent, two-node
contour). Finally `Example1_Loads_AndValidates`: load from `AppContext.BaseDirectory`, assert
`Validate()` is empty, 20 plates, 44 mesh faces, 8 area loads.

That is ~15 new facts on top of the existing 3. **Recommendation:** split into
`RoundTripTests.cs` / `PlateTests.cs` / `ValidationTests.cs` sharing an
`internal static class SampleModels`. This is a convention change from the current single-file
setup — flagged rather than assumed.

---

## Ordered tasks

| # | Task | Risk |
| --- | --- | --- |
| 1 | `Geometry/Surfaces/{SurfaceProperty,ConstantThickness}.cs`; add `SurfaceProperties` to the root | low |
| 2 | `Geometry/{PlateRegionKind,PlateBehaviour,SurfaceAlignment}.cs` | low |
| 3 | Move `MaterialId` from `Element` onto `Bar`; build stays green with the old `Plate` | low |
| 4 | Rewrite `Plate.cs`, add `PlateRegion.cs` — **write the resolution rule into the XML doc first** | **high — semantics.** No mesher exists to falsify the rule; cheap to fix now, very expensive once models are saved against it |
| 5 | `Mesh/{FemexMesh,MeshNode,MeshFace}.cs`; add `Mesh?` to the root | **medium — one-way door.** Faces sharing the element-id space, and mesh nodes using absolute Z, are both defensible and both painful to reverse |
| 6 | `AreaLoad`: `PlateId`, `RegionId`, nullable `NodeSequence` | low |
| 7 | `Support`: `PlateId`, `RegionId` | low |
| 8 | `Hinge`: `RegionId`, edge node ids, tighten `EndOrEdgeIndex` doc | low |
| 9 | `FemexModel` → `partial`; `FemexModel.Validation.cs` with checks 1–11 (no geometry) | medium — volume |
| 10 | Geometric checks 12–13 | **high — geometry.** Newell sign conventions, degenerate contours, tolerance scaling. Land last so they can be dropped without unwinding step 9 |
| 11 | Update `RoundTripTests.cs` + csproj; get the existing 3 facts green | low |
| 12 | Add the new facts — **write `Example1_Loads_AndValidates` before step 13** | low |
| 13 | Hand-migrate `Example1.femex` via a throwaway (uncommitted) script | **medium.** Node bookkeeping (5×4 grid per level, ids 101–120 / 201–220 / …, x-fastest) is mechanical but easy to get subtly wrong |
| 14 | Refresh `Claude/FEMEX.md` + `FEMEX_Summary.md` — in particular the now-false *"plates derive axes from node order"* and *"thickness is a plate property — no shared section needed"* | low |

## Deliberately out of scope

- **No mesher.** The `Mesh` block is data-only; nothing in this change populates it except the
  Example1 migration.
- **The `bars`/`plates` discriminator wart stays.** Switching to `List<Element>` would delete the
  `bars`/`plates` JSON keys for a cosmetic gain — and `Plate` is *ceasing* to be an element, so
  betting on `List<Element>` bets on the shrinking abstraction. Its own commit, if ever.
- **`Orthotropic` behaviour, variable and layered thickness** — reserved in docs, not implemented.
- **`AreaLoad` direction/projection semantics** are still implicit. Worth a separate ticket, since
  a `LoadOnly` region's whole purpose is carrying a pressure whose direction the format never states.

## Behaviour changes worth naming

- The old file's `plates[].thickness` is **silently dropped** on deserialize (no
  `UnmappedMemberHandling` is set), so an un-migrated model loads clean but semantically empty.
  Validation check 5 is what catches it. The format has no version field — worth noting.
- `Plate`'s constructor no longer throws on `< 3` nodes. That throw is the last violation of the
  codebase's centralised, deferred, non-throwing validation convention; it becomes check 3.
- `Plate.GetNodeIds()` now also yields region contour nodes. No current caller assumes otherwise.
- `Element` loses `MaterialId`, so `Element`-typed code can no longer read a material generically.
  Nothing does today — `Validate()` already switches on concrete types.
- `FemexModel` becomes `partial`. New pattern here, and the one item that is a style call rather
  than a necessity.

## Verification

1. `dotnet build` from `C:\Griffel Studio\griffel-femex` — expect 0 warnings, 0 errors (the
   existing bar).
2. `dotnet test` — the 3 existing facts (updated) plus the ~15 new ones green.
3. `Example1_Loads_AndValidates` is the end-to-end gate: the migrated example round-trips,
   `Validate()` returns empty, and the counts (20 plates, 44 mesh faces, 8 area loads) hold.
4. Manual eyeball of the emitted JSON for the sample model against the shape in this document —
   camelCase, `"type": "constant"`, enums as strings, opening regions with no thickness/material
   keys, `"mesh"` absent when null.
5. Manual eyeball against the draft schemas in `FEMEX description.docx`, per the convention
   established in `Claude/FEMEX.md`.

## Sources

- [RAM Concept — Slabs and Openings](https://docs.bentley.com/LiveContent/web/RAM%20Concept%20Help-v16/en/GUID-199BC94C-C664-451D-8779-F6B2740BF6C8.html)
- [RFEM 6 — Openings](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6/000041) · [RFEM 6 — Thicknesses](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6/000036) · [RFEM 5 — Variable Thicknesses](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-5/004211)
- [Overlapping Slab Objects in ETABS and SAFE](https://structuralacademy.com/article/en/sobreposicao-de-objetos-de-laje-nos-programas-etabs-e-safe) · [SAFE — Modeling slabs with openings](https://wiki.csiamerica.com/display/safe/Modeling+slabs+with+openings)
- [SAF — StructuralSurfaceMember](https://gitbook.saf.guide/structural-analysis-elements/structuralsurfacemember) · [SAF — StructuralSurfaceMemberOpening](https://gitbook.saf.guide/structural-analysis-elements/structuralsurfacememberopening)
- [INDUCTA SLB](https://www.inducta.com.au/SLB_main.html) · [INDUCTA RCB area loads](https://inducta.com.au/RCB_arealoads.html)
