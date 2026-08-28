using System.IO;
using SAF.DataAccess.Models;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// The one page of this assembly that knows the SAF SDK exists as a service
    /// container rather than as an object model.
    /// </summary>
    /// <remarks>
    /// B2 of <c>SAF_Adapter.md</c>, and it earns its keep three ways. The SDK
    /// requires a SimpleInjector bootstrapper — <c>new SimpleInjectorBootstrapper()</c>
    /// → <c>CreateScope()</c> → <c>GetService&lt;IExcelImportService&gt;()</c> —
    /// because most of its implementation is internal; that should not leak into
    /// mapping code. It is the same seam shape record/replay wants, so the pattern
    /// is established on the free target first. And it is where a permissively
    /// licensed Excel reader would go if the EPPlus question ever reopened.
    ///
    /// The mapping code above this interface never names an SDK service, a
    /// container, or a stream position. It names <see cref="ExcelModel"/>, which is
    /// the SDK's data, not the SDK's plumbing.
    /// </remarks>
    public interface ISafGateway
    {
        /// <summary>Reads a SAF workbook. Never throws for a bad file; says so in the result.</summary>
        SafReadResult Read(Stream source);

        /// <summary>Writes a SAF workbook. Never throws for a rejected model; says so in the result.</summary>
        SafWriteResult Write(Stream destination, ExcelModel model);
    }
}
