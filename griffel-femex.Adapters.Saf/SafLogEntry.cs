namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// One line of the SDK's own commentary on a read or a write.
    /// </summary>
    /// <remarks>
    /// The SDK reports through an event subscription rather than through exceptions
    /// or a return value — <c>IEventService.Subscribe&lt;LogEvent&gt;</c>, five
    /// severities, 205 events on the reference workbook and none above INFO
    /// (<c>FEMEX_SAF_Corpus_Notes.md</c> §2). Folding the loud half of that into the
    /// transfer report is cheaper and more honest than inventing our own diagnostics
    /// for things the layer beneath already noticed.
    ///
    /// It is a plain struct rather than the SDK's <c>LogEvent</c> so that nothing
    /// above <see cref="ISafGateway"/> has to reference the SDK to read a message.
    /// </remarks>
    public readonly struct SafLogEntry
    {
        public SafLogEntry(SafLogSeverity severity, string message, string? source)
        {
            Severity = severity;
            Message = message;
            Source = source;
        }

        public SafLogSeverity Severity { get; }

        public string Message { get; }

        /// <summary>The SDK component that spoke, where it named one.</summary>
        public string? Source { get; }

        public override string ToString()
        {
            return Source is null ? $"{Severity}: {Message}" : $"{Severity} [{Source}]: {Message}";
        }
    }

    /// <summary>The SDK's five log levels, mirrored so callers need no SDK reference.</summary>
    public enum SafLogSeverity
    {
        Debug,
        Trace,
        Info,
        Warn,
        Error,
    }
}
