using System.Threading;

namespace griffel_femex.Interop
{
    /// <summary>
    /// What every adapter says about itself before anything is asked of it.
    /// </summary>
    public interface IFemexAdapter
    {
        AdapterInfo Info { get; }

        AdapterCapabilities Capabilities { get; }
    }

    /// <summary>
    /// Native model in, FEMEX out.
    ///
    /// <b>Synchronous, on the caller's thread.</b> Revit's API may only be touched
    /// from its own main thread, and ETABS' OAPI is no more permissive. An
    /// <c>async</c> signature is therefore actively harmful: it reads as an
    /// invitation to <c>Task.Run</c>, and <c>Task.Run</c> around a Revit API call is
    /// the single most common way to kill a Revit add-in. The host owns the thread
    /// and the signature does not suggest otherwise. An adapter that internally
    /// needs concurrency — a file parser, say — may use it, provided nothing native
    /// leaves the calling thread.
    ///
    /// <b>A native API failure returns; it does not throw.</b> An adapter throws only
    /// for genuine programmer error — a null request, a request type it was never
    /// given. Everything it can describe comes back as an Error-severity
    /// <see cref="TransferMessage"/> with a null category and a null
    /// <see cref="TransferResult{T}.Value"/>. A plugin that throws gives the host no
    /// uniform behaviour to build on: every host then wraps every call in
    /// <c>catch (Exception)</c> and loses the distinction between "ETABS is not
    /// installed" and "this adapter has a bug".
    /// </summary>
    public interface IFemexImporter : IFemexAdapter
    {
        TransferResult<FemexModel> Import(
            ImportRequest request,
            IProgress<TransferProgress>? progress,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// FEMEX in, native model out. The same call shape as
    /// <see cref="IFemexImporter"/>, and for the same reasons.
    ///
    /// <b>No second gate.</b> §2.3: an exporter accepts every model that passes
    /// <c>Validate(ValidationSeverity.Error)</c>, including deliberately incomplete
    /// ones. An adapter inventing its own notion of "ready" is the failure §2.1
    /// exists to prevent — a half-drawn model is exportable, and what the target
    /// cannot hold is reported, not refused.
    /// </summary>
    public interface IFemexExporter : IFemexAdapter
    {
        TransferResult<ExportReceipt> Export(
            FemexModel model,
            ExportRequest request,
            IProgress<TransferProgress>? progress,
            CancellationToken cancellationToken);
    }
}
