using System.Text.Json;

namespace griffel_femex
{
    /// <summary>
    /// Something that keeps the JSON members this build does not recognise, rather
    /// than dropping them. Every serializable type in FEMEX implements it.
    ///
    /// The failure this removes is the one the interop review calls disqualifying:
    /// a build reading a file from a later schema silently loses every field that
    /// schema added, and reports nothing — no error, no warning, a model that
    /// validates and is wrong. With extension data the payload survives the read,
    /// is written back on save, and <see cref="FemexModel.Validate()"/> names it.
    ///
    /// This is preserve-and-warn rather than refuse. <c>UnmappedMemberHandling.Disallow</c>
    /// — System.Text.Json 8.0, which this build's <c>net7.0</c> target cannot
    /// reach — would turn the same loss into a hard read failure, so a 1.4 build
    /// could not open a 1.5 file at all. The trade is deliberate and is not a
    /// dominance: extension data preserves <i>syntax</i>, not referential
    /// integrity, so a build that deletes an object a member it does not
    /// understand refers to writes a file that is internally inconsistent and
    /// looks authoritative. Both are losses; only one of them is quiet.
    ///
    /// What this closes is the loss <i>class</i>, forwards. It does not rescue a
    /// 1.3 file read by a 1.2 build — a 1.2 build is already written and nothing
    /// added here reaches it. <b>1.4 is the last build that can lose a future
    /// field in silence.</b>
    ///
    /// The interface is the enumeration handle only —
    /// <c>FemexModel.EnumerateExtensible</c> walks it. <c>[JsonExtensionData]</c>
    /// must sit on the concrete property, so every implementing type declares it
    /// for itself; the attribute is not inherited through this interface.
    /// </summary>
    public interface IExtensible
    {
        /// <summary>
        /// The members of this object's JSON that map to no declared property.
        /// Null when there were none, so a type that understood everything it read
        /// writes not one extra byte.
        /// </summary>
        Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
