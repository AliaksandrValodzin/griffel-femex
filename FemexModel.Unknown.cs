using griffel_femex.BoundaryConditions;

namespace griffel_femex
{
    /// <summary>
    /// The members a file carries that this build has no property for: where they
    /// can appear, and how <c>Validate()</c> says so.
    ///
    /// <see cref="EnumerateExtensible"/> is the single statement of what can carry
    /// them, in the same discipline <see cref="EnumerateIdentified"/> follows for
    /// uids — one walk that the report reads, so the two cannot disagree about the
    /// set. It is deliberately <b>broader</b> than that one: uids are for the 13
    /// authored families a receiving program merges by, whereas anything that is a
    /// JSON object at all can gain a member in a later schema. So this also reaches
    /// <see cref="Units"/>, <see cref="Gravity"/>, <see cref="Metadata"/>, the
    /// gridlines and grid extents, the restraints and releases, the sections' own
    /// value blocks, the combination terms, the mesh and the root itself.
    /// </summary>
    public partial class FemexModel
    {
        /// <summary>
        /// Every object in the model that can carry unknown members, each with the
        /// wording the validation messages already use for it — "Material 3",
        /// "Plate 4 region 2" — and the plural noun for its kind, which is what a
        /// message names when the same member turns up on many of them.
        ///
        /// The three singleton blocks give their own wording as the kind too. That
        /// is never printed: a kind only appears when a member was seen more than
        /// once under it, and there is only ever one root, one units block and one
        /// mesh.
        /// </summary>
        private IEnumerable<(IExtensible Entity, string Owner, string Kind)> EnumerateExtensible()
        {
            yield return (this, "the model root", "the model root");

            if (Metadata is not null)
                yield return (Metadata, "the metadata block", "the metadata block");

            if (Units is not null)
                yield return (Units, "the units block", "the units block");

            yield return (Gravity, "the gravity block", "the gravity block");

            foreach (var grid in Grids)
            {
                yield return (grid, $"Grid {grid.Id}", "grids");

                if (grid.Extent is not null)
                    yield return (grid.Extent, $"Grid {grid.Id} extent", "grid extents");

                foreach (var line in grid.Lines)
                    yield return (line, $"Grid {grid.Id} line \"{line.Label}\"", "gridlines");
            }

            foreach (var level in Levels)
                yield return (level, $"Level {level.LevelNumber}", "levels");

            foreach (var node in Nodes)
                yield return (node, $"Node {node.NodeNumber}", "nodes");

            foreach (var section in Sections)
            {
                yield return (section, $"Section {section.Id}", "sections");

                if (section.Properties is not null)
                    yield return (section.Properties, $"Section {section.Id} properties", "section properties");
            }

            foreach (var surface in SurfaceProperties)
                yield return (surface, $"Surface property {surface.Id}", "surface properties");

            foreach (var bar in Bars)
                yield return (bar, $"Bar {bar.Id}", "bars");

            foreach (var plate in Plates)
            {
                yield return (plate, $"Plate {plate.Id}", "plates");

                foreach (var region in plate.Regions)
                    yield return (region, $"Plate {plate.Id} region {region.Id}", "plate regions");
            }

            foreach (var material in Materials)
                yield return (material, $"Material {material.Id}", "materials");

            foreach (var loadCase in LoadCases)
                yield return (loadCase, $"Load case {loadCase.Number}", "load cases");

            foreach (var load in Loads)
                yield return (load, $"Load {load.Id}", "loads");

            foreach (var combination in LoadCombinations)
            {
                yield return (combination, $"Load combination {combination.Number}", "load combinations");

                // A term has no id of its own — it is a value addressed by its
                // position — so its position is what names it.
                for (int i = 0; i < combination.Terms.Count; i++)
                {
                    yield return (combination.Terms[i],
                                  $"Load combination {combination.Number} term {i + 1}",
                                  "load combination terms");
                }
            }

            foreach (var support in Supports)
            {
                yield return (support, $"Support {support.Id}", "supports");

                foreach (var (restraint, dof) in EnumerateDofs(support))
                    yield return (restraint, $"Support {support.Id} {dof}", "support restraints");
            }

            foreach (var hinge in Hinges)
            {
                yield return (hinge, $"Hinge {hinge.Id}", "hinges");

                foreach (var (release, dof) in EnumerateDofs(hinge))
                    yield return (release, $"Hinge {hinge.Id} {dof}", "hinge releases");
            }

            if (Mesh is null)
                yield break;

            yield return (Mesh, "the mesh", "the mesh");

            foreach (var meshNode in Mesh.Nodes)
                yield return (meshNode, $"Mesh node {meshNode.Id}", "mesh nodes");

            foreach (var face in Mesh.Faces)
                yield return (face, $"Mesh face {face.Id}", "mesh faces");
        }

        /// <summary>The six degrees of freedom of a support, named as the file names them.</summary>
        private static IEnumerable<(Restraint Restraint, string Dof)> EnumerateDofs(Support support)
        {
            yield return (support.Ux, "ux");
            yield return (support.Uy, "uy");
            yield return (support.Uz, "uz");
            yield return (support.Rx, "rx");
            yield return (support.Ry, "ry");
            yield return (support.Rz, "rz");
        }

        /// <summary>The same six for a hinge.</summary>
        private static IEnumerable<(Release Release, string Dof)> EnumerateDofs(Hinge hinge)
        {
            yield return (hinge.Ux, "ux");
            yield return (hinge.Uy, "uy");
            yield return (hinge.Uz, "uz");
            yield return (hinge.Rx, "rx");
            yield return (hinge.Ry, "ry");
            yield return (hinge.Rz, "rz");
        }

        /// <summary>
        /// What the file says that this build cannot read. A warning and not an
        /// error: nothing about the model is inconsistent, the payload survives the
        /// round trip, and only the author can say whether the member mattered.
        ///
        /// <b>One message per distinct member name</b>, not per object, for the
        /// reason <see cref="ValidateUidCoverage"/> gives: the fact is about the
        /// file, and a file from a schema two versions ahead would otherwise bury
        /// every other message under hundreds of copies of one.
        ///
        /// Keyed on the pair (member name, kind) rather than on the name alone,
        /// because the message names a kind and a <c>"stiffnessModifier"</c>
        /// appearing on both bars and plates has no single kind to name. Reported in
        /// first-encounter order, which is document order.
        /// </summary>
        private IEnumerable<string> ReportUnknownMembers()
        {
            var seen = new Dictionary<(string Name, string Kind), (int Count, string FirstOwner)>();
            var order = new List<(string Name, string Kind)>();

            foreach (var (entity, owner, kind) in EnumerateExtensible())
            {
                if (entity.UnknownMembers is not { Count: > 0 } members)
                    continue;

                foreach (string name in members.Keys)
                {
                    var key = (name, kind);

                    if (seen.TryGetValue(key, out var already))
                    {
                        seen[key] = (already.Count + 1, already.FirstOwner);
                        continue;
                    }

                    seen[key] = (1, owner);
                    order.Add(key);
                }
            }

            foreach (var key in order)
            {
                var (count, firstOwner) = seen[key];

                // One occurrence is named, not counted: "on Material 3" says more
                // than "on 1 material", and it is the case a hand-editable format
                // produces most.
                string where = count == 1 ? firstOwner : $"{count} {key.Kind}";

                yield return $"The file carries a member this build does not know: \"{key.Name}\", " +
                             $"on {where}. It is preserved when the model is re-saved, but nothing " +
                             "here reads it.";
            }
        }
    }
}
