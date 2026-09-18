using System;
using System.Collections.Generic;

namespace Escape.Core
{
    /// <summary>
    /// On-disk wrapper around SaveData. schemaVersion lives on the envelope
    /// so a file's format can be inspected before the payload is trusted.
    /// Files written before the envelope existed deserialize with
    /// payload == null and are treated as legacy v1 SaveData.
    /// </summary>
    [Serializable]
    public sealed class SaveEnvelope
    {
        public int schemaVersion = SaveData.CurrentVersion;
        public string gameVersion = "";
        public string timestampUtc = "";
        public string slotId = "";
        public SaveData payload;
    }

    /// <summary>
    /// Minimal parse probe. Unity serialization always materializes
    /// [Serializable] reference fields, so payload == null can never
    /// distinguish formats — but schemaVersion is absent in legacy v1
    /// files (bare SaveData), so it stays 0 there.
    /// </summary>
    [Serializable]
    public sealed class SaveEnvelopeProbe
    {
        public int schemaVersion;
    }

    /// <summary>
    /// Validation outcome. Fatal problems land in Errors; repairs the
    /// validator applied (dropped unknown ids, removed duplicates) land in
    /// Warnings and set WasRepaired — never silently thrown away.
    /// </summary>
    public sealed class SaveValidationResult
    {
        public bool IsValid;
        public bool WasRepaired;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
    }
}
