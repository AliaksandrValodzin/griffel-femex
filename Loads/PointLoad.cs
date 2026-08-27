namespace griffel_femex.Loads
{
    /// <summary>
    /// A point load and moment applied at a node, or — from 1.9 — at a stated
    /// position along a bar.
    ///
    /// <b>The position is data about the load, not a fact about the topology.</b>
    /// SAF addresses a point action either <c>In node</c> or <c>On beam</c> with a
    /// position along it, and the reference workbook uses both on the very first
    /// file read (<c>Claude/FEMEX_SAF_Corpus_Notes.md</c> §3.8). The three answers
    /// FEMEX could have given instead are each wrong in their own way: minting a node
    /// and splitting the member changes topology, element count and member identity,
    /// which breaks the round-trip equivalence definition outright; snapping to the
    /// nearer end changes the answer silently, which is the failure class the product
    /// exists to catch; and refusing loses a load that is on nearly every real file.
    /// Carrying the number costs two nullable fields, changes nothing for a file that
    /// does not use them, and is exactly reversible.
    /// </summary>
    public class PointLoad : Load
    {
        /// <summary>
        /// The node the load acts at (references <c>Node.NodeNumber</c>). Read only
        /// when <see cref="BarId"/> is null, which is every load written before 1.9.
        /// </summary>
        public int NodeNumber { get; set; }

        /// <summary>
        /// The bar the load acts on (references <c>Element.Id</c> of a
        /// <see cref="griffel_femex.Geometry.Bar"/>), when it acts along a member
        /// rather than at a node. Null — the normal case — means
        /// <see cref="NodeNumber"/> is the target and behaves exactly as it always
        /// has.
        /// </summary>
        public int? BarId { get; set; }

        /// <summary>
        /// Where along <see cref="BarId"/> the load acts: <b>relative, 0 at the bar's
        /// start node and 1 at its end node</b>. Null with a bar named means the
        /// start.
        ///
        /// Relative and not absolute, deliberately, though SAF states both: a
        /// relative station survives the member being re-measured, and converting an
        /// absolute one needs only the bar's length. That conversion is exact on a
        /// straight member and an approximation on a chorded arc, where the chord
        /// length is not the arc length — which an adapter reports rather than
        /// hides.
        /// </summary>
        public double? Position { get; set; }

        // Forces in X, Y, Z directions
        public double Fx { get; set; }
        public double Fy { get; set; }
        public double Fz { get; set; }

        // Moments about X, Y, Z axes
        public double Mx { get; set; }
        public double My { get; set; }
        public double Mz { get; set; }
    }
}
