using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Geometry.Sections
{
    /// <summary>
    /// What library this section came out of and what it is called there — the
    /// identity layer, which is how a steel frame crosses FEMEX by name where the
    /// receiver knows the library.
    ///
    /// FEMEX ships <b>no catalogue rows</b>. It ships the vocabulary to name any of
    /// them and, in <see cref="SectionProperties"/>, the numbers to survive not
    /// recognising one. A file carries its own sections' names and their resolved
    /// numbers together, so it stays self-contained: the receiver resolves by name
    /// if it has the library, and falls back to stiffness if it does not. There is
    /// no curation, no licensing and no duplication of tables Robot, ETABS and RFEM
    /// already ship as a core feature.
    ///
    /// <b>No CIS/2 form code.</b> SAF carries one to disambiguate a profile name
    /// across vendor libraries; FEMEX's <c>type</c> discriminator already is one —
    /// <c>"ishape"</c> says what a form code says, in the same object, one key
    /// earlier. The one distinction it cannot make is
    /// <see cref="SectionManufacture"/>, and that is a field here.
    ///
    /// <b>No normalisation.</b> <c>"IPE 300"</c> and <c>"IPE300"</c> are stored
    /// exactly as written. Matching designations across libraries is an adapter's
    /// job against its own database, and a format that normalises silently makes the
    /// round trip lossy in the one place it was trying to be lossless.
    /// </summary>
    public class SectionCatalogue : IExtensible
    {
        /// <summary>
        /// The library or standard <b>as the producing program names it</b> —
        /// "Euronorm", "AISC15.xml", "BS 5950". Free text, and provenance rather
        /// than a controlled vocabulary: the set of national standards is open,
        /// unbounded and still growing, so a closed enum could never be complete and
        /// would need a schema bump per country.
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// The designation as that program spells it — "IPE300", "W12X26",
        /// "HEB180". A profile named with no source draws a warning: the same
        /// designation names different profiles in different libraries.
        /// </summary>
        public string? Profile { get; set; }

        /// <summary>How the profile was made; see <see cref="SectionManufacture"/>.</summary>
        public SectionManufacture? Manufacture { get; set; }

        // Parameterless constructor for serialization
        public SectionCatalogue() { }

        // Convenience constructor
        public SectionCatalogue(string? source, string? profile, SectionManufacture? manufacture = null)
        {
            Source = source;
            Profile = profile;
            Manufacture = manufacture;
        }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
