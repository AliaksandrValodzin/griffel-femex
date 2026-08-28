using System;
using System.Collections.Generic;
using griffel_femex.Interop;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// Collects the messages one transfer produces, and enforces the per-concept
    /// rule so the mapping code does not have to remember it.
    /// </summary>
    /// <remarks>
    /// §4.4 of <c>FEMEX_Adapters.md</c> asks for one message per concept where the
    /// loss is about a kind of thing and one per object where it is about a thing.
    /// Which of the two a given loss is, is a property of the loss, so it lives in
    /// the catalogue and is enforced here: <see cref="Concept"/> is idempotent,
    /// and asking for a per-object message about a per-concept loss is a mistake
    /// this class refuses rather than silently permits.
    /// </remarks>
    public sealed class SafMessageLog
    {
        private readonly List<TransferMessage> _messages = new List<TransferMessage>();
        private readonly HashSet<SafLoss> _concepts = new HashSet<SafLoss>();
        private readonly HashSet<(SafLoss, FemexEntity)> _anchoredConcepts =
            new HashSet<(SafLoss, FemexEntity)>();
        private readonly HashSet<SafLoss> _reported = new HashSet<SafLoss>();

        public IReadOnlyList<TransferMessage> Messages => _messages;

        /// <summary>Every declared loss this transfer actually reported, per object or per concept.</summary>
        public IReadOnlyCollection<SafLoss> Reported => _reported;

        /// <summary>
        /// Reports a loss once for the whole transfer, however many objects it
        /// touched. A second call with the same loss is a no-op, which is what lets
        /// the mapping code say it at the site that noticed rather than hoisting a
        /// flag to the top of the file.
        /// </summary>
        public void Concept(SafLoss loss, string? detail = null)
        {
            SafMessages.Entry entry = SafMessages.For(loss);
            if (entry.PerObject)
            {
                throw new InvalidOperationException(
                    $"{loss} is catalogued as a per-object loss; report it against the object it is about.");
            }

            if (!_concepts.Add(loss))
                return;

            _reported.Add(loss);
            string text = detail is null ? entry.Text : entry.Text + " " + detail;
            _messages.Add(entry.Entity is FemexEntity kind
                ? TransferMessage.Loss(entry.Category, new ObjectRef(kind), text)
                : TransferMessage.ModelLoss(entry.Category, text));
        }

        /// <summary>
        /// Reports a per-concept loss once for each kind of object it is about.
        /// </summary>
        /// <remarks>
        /// A few losses are one fact that lands on several entity kinds at once — a
        /// whole model restated in different units is the clear case. Reporting it
        /// only against the model would leave the differences on bars and loads
        /// unanchored, and §4.4's per-concept report is exactly the shape for
        /// "everything of this kind changed this way". So the loss is said once per
        /// kind, and no more than once per kind.
        /// </remarks>
        public void Concept(SafLoss loss, FemexEntity anchor, string? detail = null)
        {
            SafMessages.Entry entry = SafMessages.For(loss);
            if (entry.PerObject)
            {
                throw new InvalidOperationException(
                    $"{loss} is catalogued as a per-object loss; report it against the object it is about.");
            }

            if (!_anchoredConcepts.Add((loss, anchor)))
                return;

            _reported.Add(loss);
            string text = detail is null ? entry.Text : entry.Text + " " + detail;
            _messages.Add(TransferMessage.Loss(entry.Category, new ObjectRef(anchor), text));
        }

        /// <summary>Reports a loss about one object, with the SAF name it came from.</summary>
        public void Object(SafLoss loss, ObjectRef subject, string? nativeHandle = null,
                           string? detail = null)
        {
            SafMessages.Entry entry = SafMessages.For(loss);
            if (!entry.PerObject)
            {
                throw new InvalidOperationException(
                    $"{loss} is catalogued as a per-concept loss; report it once for the transfer.");
            }

            _reported.Add(loss);
            string text = detail is null ? entry.Text : entry.Text + " " + detail;
            _messages.Add(TransferMessage.Loss(entry.Category, subject, text, nativeHandle));
        }

        /// <summary>A message that is not a catalogued loss — a read failure, or the SDK's own commentary.</summary>
        public void Add(TransferMessage message)
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            _messages.Add(message);
        }

        public void AddRange(IEnumerable<TransferMessage> messages)
        {
            if (messages is null)
                throw new ArgumentNullException(nameof(messages));

            foreach (TransferMessage message in messages)
                _messages.Add(message);
        }

        /// <summary>
        /// Folds the SDK's own log into the report. Only <see cref="SafLogSeverity.Error"/>
        /// crosses.
        /// </summary>
        /// <remarks>
        /// Two reasons, and the second is a contract gap worth stating rather than
        /// working around. The reference workbook produces 205 events and none above
        /// Info, so carrying the quiet ones would bury the transfer's own findings
        /// under the SDK's progress commentary. And <see cref="TransferMessage"/> has
        /// exactly two severities: Error, which needs no loss category, and Warning,
        /// which requires one. An SDK warning is not a loss of a known category, so
        /// carrying it as a Warning would mean inventing a category for it and
        /// carrying it as an Error would overstate it. It is left out, deliberately,
        /// and recorded in the summary rather than guessed at here.
        /// </remarks>
        public void AddSdkLog(IReadOnlyList<SafLogEntry> log)
        {
            if (log is null)
                return;

            foreach (SafLogEntry entry in log)
            {
                if (entry.Severity != SafLogSeverity.Error)
                    continue;

                _messages.Add(TransferMessage.Failure(
                    "The SAF SDK reported: " + entry.Message, null, entry.Source));
            }
        }
    }
}
