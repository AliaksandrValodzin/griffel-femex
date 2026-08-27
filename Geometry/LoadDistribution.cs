using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Geometry
{
    /// <summary>
    /// How a panel distributes the surface load applied to it: which way it spans,
    /// in what frame, and — where the author says so — which members receive the
    /// result.
    ///
    /// This is the fix for <c>FEMEX_SAF_Fit.md</c> §4 item 3, one of the eight
    /// concepts that crossed FEMEX <i>silently wrong</i>: a one-way slab read as a
    /// two-way one opens, validates and solves, and puts half the load on the wrong
    /// beams. Nothing in 1.8 could say otherwise.
    ///
    /// <b>On the panel, never on the load.</b> A slab spans one way for every load it
    /// carries. Putting the spanning direction on the load would let two loads on one
    /// panel disagree about a property of the panel, which is a contradiction the
    /// format would then have to arbitrate; on the panel there is one answer by
    /// construction. It is also where SAF puts it — the distribution is its own
    /// object that a surface load points at, not a column on the load.
    ///
    /// <b>Null is <see cref="SurfaceLoadSpanning.TwoWay"/> with no named members</b>,
    /// which is what every panel in every file written before this meant, and is the
    /// reading a receiver has always given them. On a
    /// <see cref="PlateRegion"/> null means something different again — inherit the
    /// plate's — following the rule the region already applies to
    /// <c>SurfacePropertyId</c>, <c>Alignment</c> and <c>SurfaceOffset</c>.
    ///
    /// Nothing in this library redistributes anything: FEMEX states the panel's
    /// intent and the receiving program's own load-panel machinery carries it out.
    /// Saying so is the point — an unstated intent is what the receiver invents.
    /// </summary>
    public class LoadDistribution : IExtensible
    {
        /// <summary>
        /// Which way the panel carries load. Non-nullable with no initializer, so a
        /// block that states nothing else states <see cref="SurfaceLoadSpanning.TwoWay"/>
        /// deliberately — which is a different fact from the panel carrying no
        /// distribution block at all, in the way an author-written
        /// <c>Restraint.Sense</c> of <c>Both</c> differs from silence.
        /// </summary>
        public SurfaceLoadSpanning Spanning { get; set; }

        /// <summary>
        /// The spanning frame's rotation about the panel normal, in degrees,
        /// counter-clockwise seen from local +z — the same sign convention
        /// <c>Plate.LocalAxisAngle</c> and <c>Grid.RotationAngle</c> use.
        ///
        /// Applied <b>on top of</b> <c>Plate.LocalAxisAngle</c>, not instead of it:
        /// the panel's own local axes are the frame this rotates. Two angles rather
        /// than one because they are two different statements — where the panel's
        /// axes point, and which way its reinforcement or its joists run — and a
        /// file that fused them could not change one without moving the other.
        ///
        /// Meaningless for <see cref="SurfaceLoadSpanning.TwoWay"/>, and
        /// <see cref="FemexModel.Validate()"/> says so rather than the type
        /// forbidding it.
        /// </summary>
        public double RotationAngle { get; set; }

        /// <summary>
        /// The members that receive the distributed load (references
        /// <c>Element.Id</c> of a <see cref="Bar"/>). Null — the normal case — means
        /// whatever bounds the panel, which is the receiving program's own decision
        /// and the only honest thing to say when the author has not named anything.
        ///
        /// An explicit list is SAF's <c>Load applied to</c>, which the reference
        /// workbook populates on the row that most needs it: a panel whose load is
        /// distributed to <c>Beams and edges</c> names <c>B46;B47</c>
        /// (<c>Claude/FEMEX_SAF_Corpus_Notes.md</c> §3.10). An empty list is not the
        /// same as null and is warned about — it says "these members, and there are
        /// none".
        /// </summary>
        public List<int>? BarIds { get; set; }

        // Parameterless constructor for serialization
        public LoadDistribution() { }

        public LoadDistribution(SurfaceLoadSpanning spanning, double rotationAngle = 0.0)
        {
            Spanning = spanning;
            RotationAngle = rotationAngle;
        }

        // Members this build does not know; see IExtensible. Its own property: the
        // attribute is not inherited through the interface.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
