using System;
using System.Collections.Generic;
using System.Globalization;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// Unique names, per sheet, for a workbook FEMEX is writing.
    /// </summary>
    /// <remarks>
    /// §1.3's asymmetry, handled rather than assumed away: a FEMEX name is optional
    /// and a blank or duplicate is reported as a warning, where SAF treats a
    /// duplicate name within a sheet as fatal. So a model that merely validates with
    /// warnings on this side produces a workbook that will not open on that one
    /// unless something makes the names unique. This is that something.
    ///
    /// Disambiguation appends <c>~2</c>, <c>~3</c> rather than renumbering, so a
    /// name the user chose survives recognisably and only the collision is marked.
    /// </remarks>
    internal sealed class SafNamer
    {
        private readonly Dictionary<string, HashSet<string>> _used =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        public string Unique(string sheet, string candidate)
        {
            if (!_used.TryGetValue(sheet, out HashSet<string>? taken))
            {
                taken = new HashSet<string>(StringComparer.Ordinal);
                _used[sheet] = taken;
            }

            if (string.IsNullOrWhiteSpace(candidate))
                candidate = sheet;

            if (taken.Add(candidate))
                return candidate;

            for (int i = 2; ; i++)
            {
                string next = candidate + "~" + i.ToString(CultureInfo.InvariantCulture);
                if (taken.Add(next))
                    return next;
            }
        }
    }
}
