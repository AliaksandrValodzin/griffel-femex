# Node sharing — one node per location, coincident nodes as a warning

> **Step 0 (repo convention):** this document sits alongside `Claude/FEMEX.md` and
> `Claude/FEMEX_Plates.md`; `Claude/FEMEX_Node_Sharing_Summary.md` records what was
> actually built.

## Context

A node is not a drawing point. It is the model's **unit of connectivity**: two elements are
joined where they name the same `nodeNumber`, and nowhere else. Two nodes that happen to sit
at the same coordinates are two independent sets of degrees of freedom, and the elements
attached to each are free to move apart.

Nothing in the format said so, and nothing checked it. The sample model showed the cost — the
column top, the slab's near corner and a wall corner were three separate nodes at
`(0, 0, 148.5)`. Serialized, validated and rendered, that model is indistinguishable from a
connected one; solved, the column does not hold the slab up and the wall hangs off nothing.

The failure mode is one-directional and quiet. Authoring code that adds a node per element
contour is the natural thing to write, so duplicates accumulate; and every downstream view of
the model — node count, element list, plot, JSON diff — looks exactly the same whether the
duplicate was meant or not.

## The three decisions

1. **Model code shares nodes.** An element that meets another at a point reuses the node that
   is already there rather than adding a second one. The sample model is rewritten this way,
   and the library grows the lookup that makes it a one-liner.
2. **The format still permits duplicates.** Coincident nodes are the *only* way to express a
   joint that is deliberately disconnected — a movement joint, a slip plane, a bearing, two
   structures that merely touch. Forbidding them would remove expressiveness and break
   existing files. Nothing about the serializer, the schema or `Validate()`'s error set
   changes.
3. **Validation warns.** Since the intended and the accidental case are written identically,
   the model cannot decide between them — but it can say "you have written the unusual one,
   confirm that you meant it". That is a warning, not an error.

This forces a distinction `Validate()` did not have: it returned a flat sequence of strings,
all of which meant "this model is broken". A warning is a message about a model that is *not*
broken, so severity becomes part of the message.

## Data model

### New: `ValidationSeverity.cs`, `ValidationMessage.cs` (namespace `griffel_femex`)

```
enum ValidationSeverity { Error, Warning }

sealed class ValidationMessage
    ValidationSeverity Severity
    string             Text
    static Error(string) / Warning(string)
    ToString() => "{Severity}: {Text}"
```

The rule that keeps the two apart: **anything the format forbids is an error; a warning is
always about legal FEMEX.** A consumer that ignores warnings still reads every model it could
read before.

### Changed: `FemexModel.Validate()`

```
IEnumerable<ValidationMessage> Validate()
IEnumerable<ValidationMessage> Validate(ValidationSeverity severity)   // filtered
```

Neither is a behaviour change for the existing checks — every one of them keeps its wording
and is wrapped as an `Error`. `Validate(Error)` is the "is this model usable" question;
`Validate()` with nothing filtered out is the "is anything odd here" question.

### New: `FemexModel.Nodes.cs`

The lookup that makes sharing the easy path, so model code does not have to keep its own
map of coordinates to node numbers:

| Member | Notes |
| --- | --- |
| `double GetCoincidenceTolerance()` | the distance below which two nodes are one location |
| `Node? FindNodeAt(x, y, levelNumber, verticalOffset = 0)` | matches on the **absolute** point, so a node reached from another level and offset still counts as being there; throws for an unknown level |
| `Node GetOrAddNode(x, y, levelNumber, verticalOffset = 0)` | `FindNodeAt`, else append one numbered `NextNodeNumber()` |
| `int NextNodeNumber()` | one past the highest in use, 1 for an empty model |

`GetOrAddNode` is what geometry-building code should call instead of `Nodes.Add`.

> **Extended by `FEMEX_Gridlines.md`:** `GetOrAddNodeAtGrid(gridId, labelA, labelB, level)` sits
> on top of it, so geometry can be set out from architectural grid labels and still share the
> nodes it meets.

### Tolerance

Scaled to the model's own bounding-box diagonal — `max(1e-6 × diagonal, 1e-9)` — which is the
rule the coplanarity check already used, lifted into shared constants so there is one
definition. Being relative, it means the same thing whether the model is authored in metres or
millimetres. It grows as the model grows, but only by a millionth of the diagonal, so a node
matched by `FindNodeAt` midway through authoring is still coincident once the model is
finished, and the authoring helper and the validator cannot disagree.

## The check

`ValidateCoincidentNodes` — last in the pipeline, with the other geometric checks, and the
only one that yields warnings.

- Resolves every node to an absolute point, skipping ones whose level is unknown and ones
  repeating a node number already seen (both already errors in their own right — a repeated
  number must not be reported a second time as a coincidence with itself).
- Buckets them into a grid of tolerance-sized cells and compares each against the 26
  neighbouring cells as well, so a pair straddling a cell boundary is still found. Union-find
  merges the matches, so a location is reported **once** however many nodes are stacked there.
- Groups are transitive: a chain of nodes each within tolerance of the next is one group even
  if its ends are further apart. That only arises for nodes that are all but coincident anyway.

Message:

```
Nodes 13, 98 and 99 are at the same location (10, 10, 148.5). Elements only connect
where they reference the same node number, so unless the joint is meant to be
disconnected they should share one node.
```

Node numbers ascending, groups ordered by their lowest number, so output is deterministic.

## The sample models

Both copies — `griffel-femex.Tests/SampleModels.cs` and the `griffel-femex-models` project's
`SampleModel.cs` — become one-node-per-location:

| Was | Now |
| --- | --- |
| 2 (column top, offset 0.2) and 11 (slab corner) | node 2, offset dropped: the column now holds the slab up |
| 41 (wall base) | node 1, the column base |
| 43 (wall top right) | node 12, a slab corner |
| 44 (wall top left) | node 2 |

The removed nodes stay in the source as commented-out lines naming their replacement, because
the interesting thing about the file is *which* nodes are shared.

Node `VerticalOffset` then has no non-zero use in the sample, so its round-trip gets its own
test rather than riding on the column top.

## Tests

- `NodeTests.cs` (new) — the sample model warns about nothing; `GetOrAddNode` returns the
  existing node and adds none; adding where nothing is returns `NextNodeNumber()`; building a
  contour entirely through `GetOrAddNode` shares the corners it has in common with the slab
  and leaves the model warning-free; `FindNodeAt` returns null when absent, matches across
  levels via the absolute point, and throws for an unknown level.
- `ValidationTests.cs` — two nodes at one location warn; the same model has **zero errors**
  (the format allows it); three stacked nodes give one warning naming all three; a node
  reached from another level at the same absolute point warns; a millimetre apart does not
  warn; 1e-9 apart does.

## Verification

`dotnet test` for the library, then `UpdateFemexDll.ps1` and `dotnet run` for the models
project, whose console output separates errors from warnings. Confirm the regenerated
`ModelOutput/SampleModel.femex` has as many distinct locations as nodes.

## Deliberately out of scope

- **Merging duplicates.** No `MergeCoincidentNodes()`. Which of two stacked nodes each element
  should keep is a modelling decision, and the format's whole point here is that the duplicate
  may be intended.
- **Mesh nodes.** `Mesh.Nodes` have their own id space and are generated, not authored;
  coincidence there is the mesher's business, and FEMEX has no mesher.
- **Warnings for anything else.** The severity axis is new; other candidates (unreferenced
  nodes, zero-length bars, unused load cases) are left for when they are actually wanted.
