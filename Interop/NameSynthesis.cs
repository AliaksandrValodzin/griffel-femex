using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Loads;
using griffel_femex.Materials;

namespace griffel_femex.Interop
{
    /// <summary>
    /// The one implementation of §5.4's name rule, so that five adapters do not
    /// arrive at five answers to a question with one right one.
    ///
    /// <b>Why a name matters at all.</b> Robot's properties and ETABS' sections and
    /// storeys key by <i>name</i>, not by id, and Robot's
    /// <c>Labels.StoreWithName</c> <b>silently overwrites</b> a label of the same
    /// name. A blank name and a duplicated name are therefore both data loss on
    /// export, not cosmetic problems.
    ///
    /// <b>Why the uid and nothing else.</b> Every other candidate has a defect that
    /// breaks the one property the rule exists for. An id renumbers whenever a model
    /// is rebuilt. A list position moves whenever a list is reordered, which nothing
    /// forbids. A counter is the failure itself — a second export yields
    /// <c>Section1_2</c> and a third <c>Section1_2_2</c>. A uid, once minted, is
    /// never overwritten, so a uid-derived name does not change between exports.
    /// That it is not <i>meaningful</i> is beside the point and, in fact, the third
    /// property: <c>Section-3f9a2c14</c> is <b>obviously synthetic</b>, and a
    /// synthesised name that looked authored would hide the invention — which §4.3
    /// says is the failure adapters are worst at.
    ///
    /// <b>Six families, not the validator's four.</b> <c>ValidateNameKeys</c> checks
    /// <c>Section</c>, <c>SurfaceProperty</c>, <c>Material</c> and <c>LoadCase</c>.
    /// But a storey is name-keyed in ETABS and Robot every bit as much as a section
    /// is, so <see cref="Level"/> and <see cref="Plate"/> are here too. The contract
    /// follows the target programs, not the current state of one validation method.
    /// </summary>
    public static class NameSynthesis
    {
        /// <summary>
        /// <c>{Kind}-{first 8 hex digits of the uid}</c> — <c>Section-3f9a2c14</c>,
        /// <c>Level-b71e04d9</c>. Collision-resistant enough for a model of any
        /// plausible size, and a collision is caught by <c>ValidateNameKeys</c>'
        /// duplicate check rather than silently overwriting a Robot label.
        /// </summary>
        public static string For(FemexEntity kind, Guid uid)
        {
            return $"{kind}-{uid.ToString("N").Substring(0, 8)}";
        }

        /// <summary>
        /// Fills every blank name in the six name-keyed families and returns one
        /// <see cref="LossCategory.Invented"/> message per name filled.
        ///
        /// <b>This mutates the model</b>, and does so on purpose: §5.4's rule is that
        /// an exporter calls <see cref="FemexModel.AssignMissingUids"/> before
        /// synthesising any name, because the name is derived from the uid and a
        /// model that has never met an adapter has none. An exporter unwilling to
        /// stamp the caller's model should copy it first; an exporter that
        /// synthesises names without stamping is deriving a name from nothing.
        ///
        /// Where an adapter has a native name worth keeping it uses that instead —
        /// the synthesised form is the floor, not the preference.
        /// </summary>
        public static IReadOnlyList<TransferMessage> Apply(FemexModel model)
        {
            if (model is null)
                throw new ArgumentNullException(nameof(model));

            model.AssignMissingUids();

            var messages = new List<TransferMessage>();

            foreach (Section section in model.Sections)
                Fill(messages, FemexEntity.Section, section.Id, section.Uid, section.Name,
                     name => section.Name = name);

            foreach (SurfaceProperty surface in model.SurfaceProperties)
                Fill(messages, FemexEntity.SurfaceProperty, surface.Id, surface.Uid, surface.Name,
                     name => surface.Name = name);

            foreach (Material material in model.Materials)
                Fill(messages, FemexEntity.Material, material.Id, material.Uid, material.Name,
                     name => material.Name = name);

            foreach (LoadCase loadCase in model.LoadCases)
                Fill(messages, FemexEntity.LoadCase, loadCase.Number, loadCase.Uid, loadCase.Label,
                     name => loadCase.Label = name);

            foreach (Level level in model.Levels)
                Fill(messages, FemexEntity.Level, level.LevelNumber, level.Uid, level.Name,
                     name => level.Name = name);

            foreach (Plate plate in model.Plates)
                Fill(messages, FemexEntity.Plate, plate.Id, plate.Uid, plate.Name,
                     name => plate.Name = name);

            return messages;
        }

        private static void Fill(List<TransferMessage> messages, FemexEntity kind, int id, Guid? uid,
                                 string? current, Action<string> assign)
        {
            if (!string.IsNullOrWhiteSpace(current))
                return;

            // AssignMissingUids ran first, so this is unreachable for a model the
            // caller handed over intact. It is here because a caller can null a uid
            // between the two calls, and a name derived from nothing would be the
            // instability the rule forbids.
            if (uid is not Guid value)
                return;

            string name = For(kind, value);
            assign(name);

            messages.Add(TransferMessage.Loss(
                LossCategory.Invented,
                new ObjectRef(kind, id, value),
                $"{kind} {id} had no name, and the target keys by name. It was given the synthesised " +
                $"\"{name}\", which is stable across exports and obviously not authored."));
        }
    }
}
