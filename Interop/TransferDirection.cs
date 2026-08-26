namespace griffel_femex.Interop
{
    /// <summary>
    /// Which way an adapter can carry one <see cref="FemexEntity"/>. Declared per
    /// entity and per direction, because asymmetry is the common case rather than
    /// the exotic one — a program you can read plates out of but not write plates
    /// into is ordinary.
    /// </summary>
    [Flags]
    public enum TransferDirection
    {
        None = 0,
        Import = 1,
        Export = 2,
        Both = Import | Export,
    }
}
