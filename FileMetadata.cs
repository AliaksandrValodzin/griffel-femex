using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex
{
    /// <summary>
    /// Who wrote this file, with what, for which project, and when — the second key
    /// in the document, immediately after <see cref="FemexModel.SchemaVersion"/>.
    ///
    /// Every format FEMEX targets says this on the way in. ETABS stamps
    /// <c>PROGRAM "ETABS" VERSION "21.0.0"</c> on line 1 of every <c>.e2k</c>; SAF
    /// devotes a whole <c>Model</c> worksheet to project and model specifications.
    /// A FEMEX file before 1.4 could not say what wrote it, which makes a wrong
    /// number in one impossible to trace back to the translator that produced it.
    ///
    /// The precedent is one level down: <see cref="Mesh.FemexMesh.Generator"/> and
    /// <see cref="Mesh.FemexMesh.GeneratedAt"/> already record the provenance of
    /// the mesh block. This is the same statement promoted to the root, in the same
    /// spelling.
    ///
    /// Four nullable free-text fields, and nothing in the library computes with any
    /// of them. <see cref="CreatedAt"/> in particular is a string rather than a
    /// <c>DateTimeOffset?</c>: System.Text.Json would reformat the value on write,
    /// and matching the spelling <see cref="Mesh.FemexMesh.GeneratedAt"/> already
    /// uses — "ISO-8601 timestamp as free text, in keeping with Units being free
    /// text" — is worth more than a type nothing reads.
    ///
    /// <see cref="FemexModel.ToJson"/> deliberately does <b>not</b> stamp this
    /// block, for the reason <see cref="FemexModel.AssignMissingUids"/> is a call
    /// rather than a save-time action: auto-stamping a timestamp would mean the
    /// same model built twice from the same source produced different files.
    /// <c>schemaVersion</c> is stamped because it is a statement about the
    /// <i>format</i>, which the library knows; who produced the file and when is a
    /// statement about the <i>caller</i>, which it does not.
    ///
    /// Not validated, matching <see cref="Units"/>: everything
    /// <see cref="FemexModel.Validate()"/> reports is something that makes a
    /// receiver get a model wrong, and a missing producer does not change how one
    /// number in the file is read.
    /// </summary>
    public class FileMetadata : IExtensible
    {
        // The application that wrote this file, e.g. "griffel-etabs".
        public string? Producer { get; set; }

        // That application's own version, e.g. "0.1.0" — not the schema version,
        // which is FemexModel.SchemaVersion and says something about the format.
        public string? ProducerVersion { get; set; }

        // Free-text project name, the closest FEMEX equivalent of SAF's Model
        // worksheet.
        public string? ProjectName { get; set; }

        // ISO-8601 timestamp as free text, in keeping with FemexMesh.GeneratedAt.
        public string? CreatedAt { get; set; }

        // Parameterless constructor for serialization
        public FileMetadata() { }

        // Convenience constructor
        public FileMetadata(string? producer, string? producerVersion = null,
                            string? projectName = null, string? createdAt = null)
        {
            Producer = producer;
            ProducerVersion = producerVersion;
            ProjectName = projectName;
            CreatedAt = createdAt;
        }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
