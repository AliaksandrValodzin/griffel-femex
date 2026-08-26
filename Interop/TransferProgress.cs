namespace griffel_femex.Interop
{
    /// <summary>
    /// How far a transfer has got. A readonly struct, because a whole-model transfer
    /// reports thousands of these and none of them outlives the call.
    ///
    /// In the signature from the first version rather than added later: adding
    /// <c>IProgress&lt;T&gt;</c> once five plugins implement the contract is a
    /// breaking change across every one of them, and it is free for an adapter that
    /// ignores it — the parameter is nullable.
    /// </summary>
    public readonly struct TransferProgress
    {
        public TransferProgress(FemexEntity? entity, int completed, int total, string? text = null)
        {
            Entity = entity;
            Completed = completed;
            Total = total;
            Text = text;
        }

        /// <summary>
        /// Which entity list is being walked, or null for work that is not per
        /// entity — opening the file, starting the program.
        /// </summary>
        public FemexEntity? Entity { get; }

        public int Completed { get; }

        /// <summary>
        /// How many there are, or 0 where the adapter does not know yet — a reader
        /// streaming a file often does not.
        /// </summary>
        public int Total { get; }

        /// <summary>One line for a status bar, where the counts are not enough.</summary>
        public string? Text { get; }

        public override string ToString()
        {
            string what = Entity.HasValue ? Entity.Value.ToString() : "model";
            string counts = Total > 0 ? $"{Completed}/{Total}" : Completed.ToString();
            return Text is null ? $"{what} {counts}" : $"{what} {counts} — {Text}";
        }
    }
}
