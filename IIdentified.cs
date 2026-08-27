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

        /// <summary>
        /// What this object was derived from, where it was derived from something —
        /// SAF's <c>Parent ID</c>, which appears on seventeen of the reference
        /// workbook's forty-three sheets
        /// (<c>Claude/FEMEX_SAF_Corpus_Notes.md</c> §7). FEMEX declining it while
        /// reading a format that carries it is what would make an approximation
        /// <i>irreversible</i> rather than merely lossy, which is the failure this
        /// field exists to prevent.
        ///
        /// Added in schema 1.9, and it has four consumers, none of them speculative:
        /// <list type="number">
        /// <item>a chorded arc's straight pieces point at the arc, so a write back
        /// out can re-emit the arc instead of eight lines — the one loss
        /// <c>FEMEX_SAF_Fit.md</c> §0 names as non-reversible on every round
        /// trip;</item>
        /// <item>a model diff can tell that eight bars are one member, rather than
        /// reporting eight differences against one;</item>
        /// <item>loads expanded from one repeating native object carry a pointer back
        /// to the object they were expanded from, so the expansion can be
        /// collapsed;</item>
        /// <item>a producer that fills SAF's own <c>Parent ID</c> has somewhere for
        /// the value to land instead of it being dropped.</item>
        /// </list>
        ///
        /// <b>It is a provenance pointer and nothing more.</b> Not containment, not
        /// ownership, not a derivation-tracking design. Nothing in this library
        /// traverses it, nothing derives from it, and no behaviour changes when it is
        /// null — which is every object in every file written before 1.9. The one
        /// rule <see cref="FemexModel.Validate()"/> applies is that it must not be the
        /// nil guid and must not be the object's own <see cref="Uid"/>; a value
        /// naming no object <i>in this model</i> is legal and reported as a warning,
        /// because a chord's parent is an arc that was never a FEMEX object in the
        /// first place.
        ///
        /// If derivation tracking is ever wanted it will want a typed relation and a
        /// reason, and this field will be one input to it rather than a half-built
        /// version of it. Nothing needs that today.
        /// </summary>
        Guid? ParentUid { get; set; }
    }
}
