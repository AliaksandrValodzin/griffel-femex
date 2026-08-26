namespace griffel_femex.Interop
{
    /// <summary>
    /// What every transfer returns: the thing produced, and what it cost.
    ///
    /// The rejected alternative is to return a bare model and expose the messages as
    /// a property on the adapter, or via an event, or through an <c>out</c>
    /// parameter. Each of those makes reading the report optional at the call site,
    /// and anything optional at the call site is omitted by the second caller. A
    /// single return value that cannot be destructured without seeing the messages
    /// is the only shape where ignoring the report is a visible decision rather than
    /// an oversight.
    ///
    /// <b><see cref="Succeeded"/> is defined by <see cref="Value"/>, not by
    /// severity</b>, which sounds like a detail and is the invariant the whole
    /// taxonomy rests on: a transfer that reports fifty losses and produces a model
    /// succeeded, and a transfer that reports none and produces nothing did not.
    /// </summary>
    public sealed class TransferResult<T> where T : class
    {
        private static readonly TransferMessage[] NoMessages = new TransferMessage[0];

        private TransferResult(T? value, IReadOnlyList<TransferMessage> messages)
        {
            Value = value;
            Messages = messages;
        }

        public T? Value { get; }

        public IReadOnlyList<TransferMessage> Messages { get; }

        public bool Succeeded => Value is not null;

        /// <summary>The transfer happened. It may still have lost a great deal.</summary>
        public static TransferResult<T> Ok(T value, IEnumerable<TransferMessage>? messages = null)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            return new TransferResult<T>(value, Freeze(messages));
        }

        /// <summary>
        /// The transfer did not happen. At least one message says why, and by the
        /// invariant in <see cref="TransferMessage"/> it is an Error carrying no
        /// category.
        /// </summary>
        public static TransferResult<T> Failed(IEnumerable<TransferMessage> messages)
        {
            IReadOnlyList<TransferMessage> frozen = Freeze(messages);
            if (frozen.Count == 0)
                throw new ArgumentException("A failed transfer must say why.", nameof(messages));

            return new TransferResult<T>(null, frozen);
        }

        /// <summary>A failure from one sentence, which is the common case.</summary>
        public static TransferResult<T> Failed(string text)
        {
            return Failed(new[] { TransferMessage.Failure(text) });
        }

        private static IReadOnlyList<TransferMessage> Freeze(IEnumerable<TransferMessage>? messages)
        {
            if (messages is null)
                return NoMessages;

            var list = new List<TransferMessage>(messages);
            return list.Count == 0 ? (IReadOnlyList<TransferMessage>)NoMessages : list;
        }
    }
}
