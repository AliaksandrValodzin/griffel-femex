namespace griffel_femex.Interop
{
    /// <summary>
    /// Who this adapter is: the plugin's name, the program and version it targets,
    /// and — load-bearing rather than decorative — <b>the FEMEX schema version it
    /// was built against</b>.
    ///
    /// That last field is what makes <see cref="LossCategory.Stale"/> reportable.
    /// An adapter is not a FEMEX build, so <c>IExtensible</c>'s preserve-and-warn
    /// does not reach it: an adapter compiled against 1.8 reading a 1.9 file has
    /// properties for none of what 1.9 added, and every one of them leaves the model
    /// on the way to the native program. The machinery to notice is already there —
    /// <see cref="FemexModel.SchemaVersion"/> and
    /// <see cref="FemexModel.CurrentSchemaVersion"/> — and what was missing is the
    /// obligation to say something. <see cref="CompareSchema"/> is that obligation
    /// made callable.
    /// </summary>
    public sealed class AdapterInfo
    {
        public AdapterInfo(string name, string targetProgram, string? targetProgramVersion,
                           string schemaVersion)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An adapter has a name.", nameof(name));
            if (string.IsNullOrWhiteSpace(targetProgram))
                throw new ArgumentException("An adapter names what it targets.", nameof(targetProgram));
            if (string.IsNullOrWhiteSpace(schemaVersion))
                throw new ArgumentException("An adapter states the schema it was built against.",
                                            nameof(schemaVersion));

            Name = name;
            TargetProgram = targetProgram;
            TargetProgramVersion = targetProgramVersion;
            SchemaVersion = schemaVersion;
        }

        public string Name { get; }

        /// <summary>The program or format on the other side — "SAF", "Revit", "ETABS".</summary>
        public string TargetProgram { get; }

        /// <summary>
        /// Which version of it, where the adapter is pinned to one. Null says the
        /// adapter does not claim a version, which is honest for a file format read
        /// through a versioned SDK.
        /// </summary>
        public string? TargetProgramVersion { get; }

        /// <summary>
        /// The FEMEX schema this adapter was compiled against — normally
        /// <see cref="FemexModel.CurrentSchemaVersion"/> at build time, and
        /// deliberately not read from the library at run time, since the whole point
        /// is to notice when the two have parted company.
        /// </summary>
        public string SchemaVersion { get; }

        /// <summary>
        /// The <see cref="LossCategory.Stale"/> message a model deserves, or null
        /// when it does not deserve one.
        ///
        /// Deliberately not an ordering rule over version strings: FEMEX has no
        /// ordering policy over versions, and inventing one here would invent
        /// behaviour for versions that do not exist yet. The test is the one the
        /// library already makes — the model states a version this adapter's build
        /// does not know — and the message says what that costs.
        /// </summary>
        public TransferMessage? CompareSchema(FemexModel model)
        {
            if (model is null)
                throw new ArgumentNullException(nameof(model));

            string? stated = model.SchemaVersion;
            if (stated is null || string.Equals(stated, SchemaVersion, StringComparison.Ordinal))
                return null;

            return TransferMessage.ModelLoss(
                LossCategory.Stale,
                $"This model is written in FEMEX {stated}; the {Name} adapter was built against " +
                $"{SchemaVersion}. Anything {stated} added that {SchemaVersion} has no property for " +
                "leaves the model at this boundary.");
        }
    }
}
