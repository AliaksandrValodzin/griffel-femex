using System;
using System.Collections.Generic;
using SAF.DataAccess.Models;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>What one <see cref="ISafGateway.Read"/> produced, including what the SDK said while producing it.</summary>
    public sealed class SafReadResult
    {
        private static readonly SafLogEntry[] NoLog = new SafLogEntry[0];
        private static readonly HashSet<string> NoMintedRows = new HashSet<string>();

        public SafReadResult(ExcelModel? model, IReadOnlyList<SafLogEntry>? log, string? failure,
                             ISet<string>? mintedRows = null)
        {
            Model = model;
            Log = log ?? NoLog;
            Failure = failure;
            MintedRows = mintedRows ?? NoMintedRows;
        }

        /// <summary>
        /// The rows whose <c>Id</c> the SDK invented rather than read, addressed by
        /// sheet and row number.
        /// </summary>
        /// <remarks>
        /// <b>The SDK does not leave a blank Id blank.</b> It mints a fresh GUID for
        /// it on <i>read</i>, not only on write, so from the object model an invented
        /// uid is indistinguishable from an authored one — and the reference workbook
        /// leaves 42 of them blank. Adopting those would make FEMEX claim provenance
        /// the file never gave, and would make the same workbook read twice produce
        /// two models nothing could match. This set is how the importer tells the
        /// difference.
        /// </remarks>
        public ISet<string> MintedRows { get; }

        /// <summary>Null when, and only when, <see cref="Failure"/> says why.</summary>
        public ExcelModel? Model { get; }

        public IReadOnlyList<SafLogEntry> Log { get; }

        /// <summary>
        /// The read failed. A gateway returns rather than throwing, per
        /// <c>FEMEX_Adapters.md</c> §3.6 — a corrupt workbook is an ordinary answer.
        /// </summary>
        public string? Failure { get; }

        public static SafReadResult Ok(ExcelModel model, IReadOnlyList<SafLogEntry> log,
                                       ISet<string>? mintedRows = null)
        {
            if (model is null)
                throw new ArgumentNullException(nameof(model));

            return new SafReadResult(model, log, null, mintedRows);
        }

        public static SafReadResult Failed(string failure, IReadOnlyList<SafLogEntry>? log = null)
        {
            return new SafReadResult(null, log, failure);
        }
    }

    /// <summary>What one <see cref="ISafGateway.Write"/> produced.</summary>
    public sealed class SafWriteResult
    {
        private static readonly SafLogEntry[] NoLog = new SafLogEntry[0];
        private static readonly string[] NoErrors = new string[0];

        public SafWriteResult(bool succeeded, IReadOnlyList<SafLogEntry>? log,
                              IReadOnlyList<string>? validationErrors, string? failure)
        {
            Succeeded = succeeded;
            Log = log ?? NoLog;
            ValidationErrors = validationErrors ?? NoErrors;
            Failure = failure;
        }

        public bool Succeeded { get; }

        public IReadOnlyList<SafLogEntry> Log { get; }

        /// <summary>
        /// The SDK's own validation verdict on the workbook it was asked to write,
        /// flattened to text. This is the closest thing to the independent oracle
        /// that runs without a browser, and it is what catches a missing mandatory
        /// column before a receiving program does.
        /// </summary>
        public IReadOnlyList<string> ValidationErrors { get; }

        public string? Failure { get; }
    }
}
