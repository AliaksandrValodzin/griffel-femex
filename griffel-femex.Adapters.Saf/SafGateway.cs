using System;
using System.Collections.Generic;
using System.IO;
using SAF.Bootstrappers.SimpleInjector5;
using SAF.DataAccess.Contracts;
using SAF.DataAccess.Models;
using SAF.DataAccess.Models.Interfaces;
using SAF.Infrastructure.Events;
using SAF.Infrastructure.Extensions;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// The default <see cref="ISafGateway"/>: the official SAF SDK, bootstrapped
    /// through SimpleInjector, with its event stream captured for the duration of
    /// each call.
    /// </summary>
    /// <remarks>
    /// Two things here are not obvious and both cost an afternoon if rediscovered.
    ///
    /// The bootstrapper is expensive and is built once per gateway instance rather
    /// than per call — it wires the SDK's whole object configuration. A scope, by
    /// contrast, is per call and is disposed with the call.
    ///
    /// The SDK writes SAF <b>2.3.0</b>, not 2.2.0, and the caller does not get to
    /// choose: the version stamp is the SDK's. Every user-facing string in this
    /// assembly says 2.3.0 on the write leg for that reason
    /// (<c>FEMEX_SAF_Corpus_Notes.md</c> §5).
    /// </remarks>
    public sealed class SafGateway : ISafGateway, IDisposable
    {
        /// <summary>The SAF specification version the SDK emits. Not ours to pick.</summary>
        public const string WrittenSpecVersion = "2.3.0";

        /// <summary>The oldest specification version the SDK will read.</summary>
        public const string OldestReadableSpecVersion = "1.0.0";

        private readonly object _gate = new object();
        private SimpleInjectorBootstrapper? _bootstrapper;
        private bool _disposed;

        public SafReadResult Read(Stream source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            var log = new List<SafLogEntry>();
            try
            {
                // Buffered because the workbook is read twice — see FindMintedRows —
                // and because a caller's stream need not be seekable.
                byte[] bytes = Buffer(source);

                using (IDisposable scope = CreateScope(out IServiceProvider provider))
                {
                    IEventService events = provider.GetService<IEventService>();
                    using (Capture(events, log))
                    {
                        IExcelImportService import = provider.GetService<IExcelImportService>();
                        ExcelModel model;
                        using (var first = new MemoryStream(bytes, writable: false))
                            model = import.Import(first);

                        if (model is null)
                            return SafReadResult.Failed("The SAF SDK read the workbook and returned no model.", log);

                        return SafReadResult.Ok(model, log, FindMintedRows(import, model, bytes));
                    }
                }
            }
            catch (Exception ex)
            {
                // §3.6: a failure returns, it does not throw. A workbook that is not
                // a workbook, a sheet the SDK cannot parse and an EPPlus stream error
                // are all the same answer to a caller — the file did not read.
                return SafReadResult.Failed(Describe(ex), log);
            }
        }

        public SafWriteResult Write(Stream destination, ExcelModel model)
        {
            if (destination is null)
                throw new ArgumentNullException(nameof(destination));
            if (model is null)
                throw new ArgumentNullException(nameof(model));

            var log = new List<SafLogEntry>();
            try
            {
                using (IDisposable scope = CreateScope(out IServiceProvider provider))
                {
                    IEventService events = provider.GetService<IEventService>();
                    using (Capture(events, log))
                    {
                        IExcelExportService export = provider.GetService<IExcelExportService>();
                        ExcelExportResult result = export.Export(destination, model);
                        return new SafWriteResult(result.IsSuccess, log, Flatten(result.ValidationResults), null);
                    }
                }
            }
            catch (Exception ex)
            {
                return new SafWriteResult(false, log, null, Describe(ex));
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _bootstrapper = null;
            }
        }

        /// <summary>
        /// A stable address for one row of one sheet, which is the only identity a
        /// row with a blank <c>Id</c> column has.
        /// </summary>
        public static string RowKey(IExcelModuleObject item)
        {
            return item.ObjectGrouping + "#" + item.RowNumber.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Which rows the SDK gave a uid the workbook did not contain, found by
        /// reading the same bytes twice and seeing which Ids moved.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The SDK's <c>Id</c> is non-nullable and it fills a blank cell with a fresh
        /// GUID on read. So there is no property to test: an invented uid and an
        /// authored one are the same shape, and the reference workbook leaves 42 of
        /// them blank. What separates them is that an authored uid is a function of
        /// the file and an invented one is not — read the file again and it changes.
        /// </para>
        /// <para>
        /// The cost is one extra parse per import, roughly doubling the read. That is
        /// the price of being able to say which uids are provenance, and of the same
        /// workbook producing the same model twice — without which nothing downstream
        /// can be diffed and §7.2 equivalence cannot be asserted at all.
        /// </para>
        /// </remarks>
        private static HashSet<string> FindMintedRows(IExcelImportService import, ExcelModel first,
                                                      byte[] bytes)
        {
            var minted = new HashSet<string>();

            ExcelModel second;
            using (var stream = new MemoryStream(bytes, writable: false))
                second = import.Import(stream);

            if (second is null)
                return minted;

            var seen = new Dictionary<string, Guid>();
            foreach (IExcelModuleObject item in second.Objects)
            {
                if (item is ExcelObjectBase identified)
                    seen[RowKey(item)] = identified.Id;
            }

            foreach (IExcelModuleObject item in first.Objects)
            {
                if (item is not ExcelObjectBase identified)
                    continue;

                string key = RowKey(item);
                if (!seen.TryGetValue(key, out Guid other) || other != identified.Id)
                    minted.Add(key);
            }

            return minted;
        }

        private static byte[] Buffer(Stream source)
        {
            if (source is MemoryStream memory && memory.TryGetBuffer(out ArraySegment<byte> segment))
            {
                var copy = new byte[segment.Count];
                Array.Copy(segment.Array!, segment.Offset, copy, 0, segment.Count);
                return copy;
            }

            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            return buffer.ToArray();
        }

        private IDisposable CreateScope(out IServiceProvider provider)
        {
            SimpleInjectorBootstrapper bootstrapper;
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(SafGateway));

                if (_bootstrapper is null)
                {
                    _bootstrapper = new SimpleInjectorBootstrapper();
                    _bootstrapper.Verify();
                }

                bootstrapper = _bootstrapper;
            }

            IServiceProvider scope = bootstrapper.CreateScope();
            provider = scope;

            // The SDK's scope is an IServiceProvider that also owns disposables. It
            // is disposable in practice and not by contract, so this is a cast with
            // a fallback rather than an assumption.
            return scope as IDisposable ?? NullScope.Instance;
        }

        private static IDisposable Capture(IEventService events, List<SafLogEntry> log)
        {
            IEventSubscription subscription = events.Subscribe<LogEvent>(e =>
            {
                log.Add(new SafLogEntry(Translate(e.Severity), e.Message ?? string.Empty, e.Source));
            });

            return new Subscription(events, subscription);
        }

        private static SafLogSeverity Translate(LogEvent.Level level)
        {
            switch (level)
            {
                case LogEvent.Level.Debug: return SafLogSeverity.Debug;
                case LogEvent.Level.Trace: return SafLogSeverity.Trace;
                case LogEvent.Level.Warn: return SafLogSeverity.Warn;
                case LogEvent.Level.Error: return SafLogSeverity.Error;
                default: return SafLogSeverity.Info;
            }
        }

        private static IReadOnlyList<string> Flatten(IReadOnlyList<ExcelValidationResult>? results)
        {
            var flattened = new List<string>();
            if (results is null)
                return flattened;

            foreach (ExcelValidationResult result in results)
            {
                if (result.Severity != SAF.DataAccess.Models.Enums.ExcelValidationMessageSeverity.Error)
                    continue;

                string subject = result.Identifier?.ObjectName ?? result.Identifier?.ObjectIdentifier ?? "model";
                foreach (var message in result.ValidationResults)
                    flattened.Add($"{subject}: {message.Property} — {message.Message}");
            }

            return flattened;
        }

        private static string Describe(Exception ex)
        {
            Exception inner = ex;
            while (inner.InnerException is not null)
                inner = inner.InnerException;

            return ReferenceEquals(inner, ex)
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"{ex.GetType().Name}: {ex.Message} ({inner.GetType().Name}: {inner.Message})";
        }

        private sealed class Subscription : IDisposable
        {
            private readonly IEventService _events;
            private readonly IEventSubscription _subscription;

            public Subscription(IEventService events, IEventSubscription subscription)
            {
                _events = events;
                _subscription = subscription;
            }

            public void Dispose()
            {
                // A subscription that outlives the call would attribute the next
                // transfer's SDK commentary to this one.
                try
                {
                    _events.Unsubscribe(_subscription);
                }
                catch (Exception)
                {
                    // Unsubscribing is best-effort cleanup; a failure here must not
                    // turn a successful transfer into a thrown one.
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();

            public void Dispose()
            {
            }
        }
    }
}
