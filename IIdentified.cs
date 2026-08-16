namespace griffel_femex
{
    /// <summary>
    /// Something a receiving program can recognise on re-import as the object it
    /// exported, rather than duplicating it. Null is a real answer, not a gap: it
    /// says this object has no round-trip identity, which is the honest state of a
    /// hand-authored file.
    ///
    /// The integer id stays the in-file reference key — <c>Bar.StartNodeId</c>,
    /// <c>Plate.NodeIds</c> and every other reference are untouched. This is SAF's
    /// three-layer answer, not a replacement for the id: a name is what programs
    /// keying by name read, an id is what this file's own references read, and a
    /// <see cref="Uid"/> is what says "this is the object you gave me".
    ///
    /// Assigned by the <b>exporting</b> application, which remembers the mapping to
    /// its own native handle — Revit's <c>UniqueId</c>, an ETABS GUID, a Robot
    /// label. FEMEX never mints one on save; <see cref="FemexModel.AssignMissingUids"/>
    /// is a call a caller makes, and it never overwrites one that is already there.
    /// </summary>
    public interface IIdentified
    {
        Guid? Uid { get; set; }
    }
}
