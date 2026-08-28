using System.Collections.Generic;
using System.IO;

namespace griffel_femex.Adapters.Saf.Tests
{
    /// <summary>
    /// The published SAF example corpus, and what it actually is.
    /// </summary>
    /// <remarks>
    /// Eleven workbooks, and <b>two models</b>: eight of them are the same 133-object
    /// steel frame re-emitted at successive specification versions, and three are the
    /// same house differing by five objects. So a test over all eleven exercises two
    /// distinct structures and nine version-compatibility paths, which is a
    /// version-migration test wearing a corpus test's clothes. It is worth having for
    /// exactly that and it is not coverage — recorded here rather than left for a
    /// reader to infer from the file names.
    /// </remarks>
    public static class SafCorpus
    {
        /// <summary>The one 2.2.0 workbook: SCIA's "model containing all supported objects".</summary>
        public const string Reference = "SAF_example_HOUSE_metric_ZYX_220.xlsx";

        private static readonly string[] Files =
        {
            "SAF_example_HOUSE_metric_ZYX_200.xlsx",
            "SAF_example_HOUSE_metric_ZYX_210.xlsx",
            "SAF_example_HOUSE_metric_ZYX_220.xlsx",
            "SAF_example_STEEL_HALL_metrix_ZYX_105.xlsx",
            "SAF_example_STEEL_HALL_metrix_ZYX_106.xlsx",
            "SAF_example_STEEL_HALL_metrix_ZYX_107.xlsx",
            "SAF_example_STEEL_HALL_metrix_ZYX_108.xlsx",
            "SAF_example_STEEL_HALL_metrix_ZYX_109.xlsx",
            "SAF_example_STEEL_HALL_metrix_ZYX_110.xlsx",
            "SAF_example_STEEL_HALL_metrix_ZYX_200.xlsx",
            "SAF_example_STEEL_HALL_metrix_ZYX_210.xlsx",
        };

        public static IEnumerable<object[]> All()
        {
            foreach (string file in Files)
                yield return new object[] { file };
        }

        public static string PathOf(string file)
        {
            return Path.Combine("Corpus", file);
        }

        public static Stream Open(string file)
        {
            return File.OpenRead(PathOf(file));
        }
    }
}
