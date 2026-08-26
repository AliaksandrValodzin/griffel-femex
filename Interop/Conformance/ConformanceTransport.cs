namespace griffel_femex.Interop.Conformance
{
    /// <summary>
    /// How the harness gets a model out of an adapter and back in again, without
    /// knowing what the far side is.
    ///
    /// A file-based adapter's transport is a stream held in memory. A live-session
    /// adapter's is the program itself, and its request types are its own — which is
    /// exactly why the harness asks for this rather than assuming a stream:
    /// <see cref="ImportRequest"/> is abstract so that a Revit adapter can subclass
    /// it in its own assembly, and a harness hard-coded to
    /// <see cref="StreamImportRequest"/> would be a Tier-1 suite only a file adapter
    /// could ever run.
    /// </summary>
    public abstract class ConformanceTransport
    {
        /// <summary>
        /// A request to export into, discarding anything a previous export left.
        /// </summary>
        public abstract ExportRequest BeginExport();

        /// <summary>
        /// A request to read back what the last <see cref="BeginExport"/> received.
        /// </summary>
        public abstract ImportRequest BeginImport();

        /// <summary>
        /// The same content with its elements in a different order, for §6.2's
        /// two-phase test: the same native model read in two traversal orders must
        /// yield identical node and level tables, or an import's answer depends on
        /// the order it happened to walk the source in.
        ///
        /// Returns false where the transport genuinely cannot reorder — a live
        /// session whose traversal order is the program's, not the test's. The check
        /// is then recorded as skipped rather than passed, because §6.2 is a rule
        /// about the adapter and an unrunnable test is not evidence about it.
        /// </summary>
        public virtual bool TryBeginReorderedImport(out ImportRequest? request)
        {
            request = null;
            return false;
        }
    }
}
