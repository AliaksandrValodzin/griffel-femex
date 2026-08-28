using System;
using SAF.DataAccess.Models;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// B4. SAF keys objects by <b>Name</b>, a string, and carries an optional
    /// <c>Id</c> GUID beside it. Both matter, and they matter differently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name is the reference key — every cross-sheet reference in a SAF workbook
    /// is a name — so it is the natural <c>TransferMessage.NativeHandle</c> and the
    /// natural entry in an <c>ExportReceipt</c>. The Id is provenance, and it is
    /// optional in practice as well as in theory: 42 of the reference workbook's
    /// rows leave it blank, and the SDK mints a fresh GUID for every one of them on
    /// write whatever FEMEX does.
    /// </para>
    /// <para>
    /// That last fact is why <see cref="UidOf"/> returns null rather than
    /// <see cref="Guid.Empty"/> for a blank Id. A uid FEMEX minted is not provenance,
    /// and an importer that presented one as though it had been read would be making
    /// a claim the layer beneath it had already disproved.
    /// </para>
    /// </remarks>
    internal static class SafIdentity
    {
        public static Guid? UidOf(ExcelObjectBase source)
        {
            return source.Id == Guid.Empty ? (Guid?)null : source.Id;
        }

        /// <summary>
        /// A stable uid for a FEMEX object that is one part of a single SAF row.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Three SAF rows become more than one FEMEX object each: a curve member
        /// chorded into a chain of bars, a <c>Position = Both</c> hinge split into
        /// one hinge per end, and a repeated point load expanded into its series.
        /// The first piece keeps the row's own uid. The rest have no identity of
        /// their own in the source at all, so minting one would make the same
        /// workbook produce a different model on every read — which is the
        /// instability §7.3's name-stability rule exists to forbid, applied to the
        /// other half of identity.
        /// </para>
        /// <para>
        /// Deriving it instead makes the read a function of the file: the same row
        /// yields the same pieces every time, so a round trip through SAF and back
        /// matches on uid rather than merely looking similar. The derivation is the
        /// row's uid with its last four bytes mixed with the piece's index, which is
        /// reversible enough to be recognisable in a diff and distinct enough not to
        /// collide with a real one.
        /// </para>
        /// </remarks>
        public static Guid Derived(Guid parent, int index)
        {
            byte[] bytes = parent.ToByteArray();
            unchecked
            {
                uint salt = (uint)(index + 1) * 2654435761u;
                bytes[12] ^= (byte)(salt & 0xFF);
                bytes[13] ^= (byte)((salt >> 8) & 0xFF);
                bytes[14] ^= (byte)((salt >> 16) & 0xFF);
                bytes[15] ^= (byte)((salt >> 24) & 0xFF);
            }

            return new Guid(bytes);
        }

        /// <summary>
        /// A stable uid for a FEMEX object that SAF has no sheet for at all — a
        /// surface property, which SAF states as a thickness on each surface.
        /// </summary>
        /// <remarks>
        /// The object is derived entirely from a number, so its identity is derived
        /// entirely from that number too. Minting one instead would mean the same
        /// workbook read twice produced two models that no diff could match, and
        /// every plate in them pointing at a different thickness object.
        /// </remarks>
        public static Guid DerivedFrom(string kind, double value)
        {
            return DerivedFromKey(
                kind + "|" + value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// A stable uid for a row whose <c>Id</c> cell was blank, derived from the
        /// row's address in the workbook.
        /// </summary>
        /// <remarks>
        /// The address is all such a row has. Deriving from it means the same file
        /// read twice gives the same model, which is what everything downstream — the
        /// diff, the round-trip assertion, the report — assumes and none of them can
        /// establish for themselves. It is still an invention, and it is declared as
        /// one.
        /// </remarks>
        public static Guid DerivedFromKey(string key)
        {
            byte[] hash;
            using (var md5 = System.Security.Cryptography.MD5.Create())
                hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));

            return new Guid(hash);
        }

        /// <summary>
        /// A stable, obviously-synthetic SAF name for a FEMEX object that has none.
        /// </summary>
        /// <remarks>
        /// The four sheets this is needed for — bars, nodes, supports and hinges —
        /// are among the largest in a workbook, so this runs on most of the model.
        /// It is <c>NameSynthesis</c>'s form, <c>{Kind}-{8 hex of the uid}</c>,
        /// rather than a counter, because §7.3's name-stability rule is that the
        /// same model exported twice produces the same names — which a counter
        /// satisfies only until the model is edited.
        /// </remarks>
        public static string NameFor(griffel_femex.Interop.FemexEntity kind, Guid uid)
        {
            return griffel_femex.Interop.NameSynthesis.For(kind, uid);
        }
    }
}
