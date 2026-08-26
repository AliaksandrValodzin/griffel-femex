namespace griffel_femex.Interop
{
    /// <summary>
    /// What an export produced. Deliberately not <c>void</c>: an export mints native
    /// handles, and §5.3 needs somewhere to put the uid ↔ handle mapping that the
    /// <i>next</i> import reads to recognise an object as the one it sent rather
    /// than duplicating it.
    ///
    /// <b>The mapping is data, not a sidecar path.</b> The documents' single-desktop
    /// framing shows here: a batch run over forty models has nowhere sensible to
    /// scatter forty sidecar files, and a web driver has no disk to scatter them on.
    /// An adapter that <i>also</i> wants to write a sidecar still can — this is what
    /// it would write — and one whose target stores the uid in the document itself
    /// returns the mapping anyway, because the report and the diff both want it.
    /// </summary>
    public sealed class ExportReceipt
    {
        private static readonly Dictionary<Guid, string> NoHandles = new Dictionary<Guid, string>();

        public ExportReceipt(string? destinationName = null,
                             IEnumerable<KeyValuePair<Guid, string>>? nativeHandles = null)
        {
            DestinationName = destinationName;

            if (nativeHandles is null)
            {
                NativeHandles = NoHandles;
                return;
            }

            var map = new Dictionary<Guid, string>();
            foreach (var pair in nativeHandles)
                map[pair.Key] = pair.Value;

            NativeHandles = map;
        }

        /// <summary>What the model was written to, for the report to name.</summary>
        public string? DestinationName { get; }

        /// <summary>
        /// Uid to native handle, for every object the adapter wrote and could name
        /// on the other side. Empty is honest and common: a format that has no
        /// handle of its own, or an adapter that has not implemented §5.3 yet, says
        /// so by returning nothing rather than by inventing keys.
        /// </summary>
        public IReadOnlyDictionary<Guid, string> NativeHandles { get; }
    }
}
