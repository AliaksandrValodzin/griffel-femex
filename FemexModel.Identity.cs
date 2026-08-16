using griffel_femex.Geometry;

namespace griffel_femex
{
    /// <summary>
    /// Round-trip identity: the optional <see cref="IIdentified.Uid"/> a receiving
    /// program recognises on re-import as the object it exported, and the 1.2 → 1.3
    /// migration that gives a pre-1.3 file's loads the ids they never had.
    ///
    /// <see cref="EnumerateIdentified"/> is the single statement of what carries a
    /// uid, and both <see cref="AssignMissingUids"/> and <c>Validate()</c> read it,
    /// so the two cannot disagree about the set — the same discipline that keeps
    /// <c>GetCoincidenceTolerance</c> shared between <c>FindNodeAt</c> and the
    /// validator, and <c>GetEffectiveProperties</c> shared between validation and
    /// the self-weight helpers.
    /// </summary>
    public partial class FemexModel
    {
        // How many loads the 1.2 → 1.3 migration numbered. A private field, which
        // System.Text.Json never serializes, recording a property of the read
        // rather than of the model: a re-emitted file is 1.3 and carries its ids,
        // so it must not report again, and a model built in memory never migrates.
        private int? _migratedLoadIdCount;

        /// <summary>
        /// Gives every authored object that has no <see cref="IIdentified.Uid"/> a
        /// fresh one, and returns how many it filled. Existing uids are
        /// <b>never</b> overwritten: an existing uid is the anchor the whole
        /// feature rests on, and rewriting it is the one thing that would destroy
        /// it. Calling this twice therefore returns <c>0</c> the second time.
        ///
        /// Deliberately a call the caller makes, rather than something
        /// <see cref="ToJson"/> does on save. Auto-stamping would mean the same
        /// model built twice from the same source carried different uids, and
        /// <c>Examples/Example1.femex</c> could never be regenerated
        /// byte-identically — the property that made the plate migration checkable
        /// at all.
        ///
        /// SAF's guidance is what this implements only half of: the exporting
        /// application assigns the uid and remembers the mapping to its own native
        /// handle. FEMEX can mint the value; only the exporter can remember what it
        /// stands for.
        /// </summary>
        public int AssignMissingUids()
        {
            int assigned = 0;

            foreach (var (entity, _) in EnumerateIdentified())
            {
                if (entity.Uid.HasValue)
                    continue;

                entity.Uid = Guid.NewGuid();
                assigned++;
            }

            return assigned;
        }

        /// <summary>
        /// Every authored object that carries a uid, each with the wording the
        /// validation messages already use for it — "Material 3", "Bar 12",
        /// "Plate 4 region 2", "Load 7".
        ///
        /// The mesh is deliberately absent: it is regenerated wholesale, so a stable
        /// identity for a mesh node or face means nothing. So is
        /// <see cref="Geometry.Grids.Gridline"/>, for the opposite reason — its
        /// identity is already its non-nullable, validated, unique <c>Label</c>.
        /// </summary>
        private IEnumerable<(IIdentified Entity, string Owner)> EnumerateIdentified()
        {
            foreach (var grid in Grids)
                yield return (grid, $"Grid {grid.Id}");

            foreach (var level in Levels)
                yield return (level, $"Level {level.LevelNumber}");

            foreach (var node in Nodes)
                yield return (node, $"Node {node.NodeNumber}");

            foreach (var section in Sections)
                yield return (section, $"Section {section.Id}");

            foreach (var surface in SurfaceProperties)
                yield return (surface, $"Surface property {surface.Id}");

            foreach (var bar in Bars)
                yield return (bar, $"Bar {bar.Id}");

            foreach (var plate in Plates)
            {
                yield return (plate, $"Plate {plate.Id}");

                foreach (PlateRegion region in plate.Regions)
                    yield return (region, $"Plate {plate.Id} region {region.Id}");
            }

            foreach (var material in Materials)
                yield return (material, $"Material {material.Id}");

            foreach (var loadCase in LoadCases)
                yield return (loadCase, $"Load case {loadCase.Number}");

            foreach (var load in Loads)
                yield return (load, $"Load {load.Id}");

            foreach (var combination in LoadCombinations)
                yield return (combination, $"Load combination {combination.Number}");

            foreach (var support in Supports)
                yield return (support, $"Support {support.Id}");

            foreach (var hinge in Hinges)
                yield return (hinge, $"Hinge {hinge.Id}");
        }

        /// <summary>
        /// Numbers the loads of a pre-1.3 file 1..N in list order, which is the only
        /// identity a load ever had before <c>Load.Id</c> existed, and records how
        /// many for <see cref="Validate()"/> to report.
        ///
        /// Gated on the version rather than on "the id is zero", deliberately: every
        /// file this build reads as null, 1.1 or 1.2 has by definition no load ids,
        /// and a 1.3 file is left exactly as it is — so genuinely duplicated load
        /// ids in a current file are still the error they should be. Without the
        /// gate, a 1.2 file read into the new type would give every load id zero and
        /// the duplicate check would report a flood of errors on a file that was
        /// never wrong.
        /// </summary>
        private void MigrateLegacyLoadIds()
        {
            if (SchemaVersion is not null &&
                !string.Equals(SchemaVersion, "1.1", StringComparison.Ordinal) &&
                !string.Equals(SchemaVersion, "1.2", StringComparison.Ordinal))
            {
                return;
            }

            if (Loads.Count == 0)
                return;

            for (int i = 0; i < Loads.Count; i++)
                Loads[i].Id = i + 1;

            _migratedLoadIdCount = Loads.Count;
        }
    }
}
