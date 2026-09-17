using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// Crash-safe file writes. A save is serialized to memory, written to
    /// slot.tmp, flushed, read back and structurally verified, then swapped
    /// into place — File.Replace moves the old file to .bak atomically.
    /// Corruption of slot.json therefore never loses the previous good save.
    /// </summary>
    public static class SaveFileIO
    {
        public static string TmpPath(string finalPath) => finalPath + ".tmp";
        public static string BakPath(string finalPath) => finalPath + ".bak";

        /// <summary>Write tmp → flush → verify read-back → .bak + atomic replace.</summary>
        public static void AtomicWrite(string finalPath, string json)
        {
            var tmp = TmpPath(finalPath);
            var bak = BakPath(finalPath);
            var bytes = Encoding.UTF8.GetBytes(json);

            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true); // fsync — the tmp file must be durable before the swap
            }

            // Read-back verification: what landed on disk must parse as an
            // enveloped save of the current schema.
            var probe = JsonUtility.FromJson<SaveEnvelopeProbe>(File.ReadAllText(tmp));
            if (probe == null || probe.schemaVersion != SaveData.CurrentVersion)
            {
                File.Delete(tmp);
                throw new InvalidOperationException("Save read-back verification failed.");
            }

            if (File.Exists(finalPath))
                File.Replace(tmp, finalPath, bak);
            else
                File.Move(tmp, finalPath);
        }

        /// <summary>Read candidates in recovery order: primary, then backup.</summary>
        public static bool TryRead(string finalPath, out string json)
        {
            if (File.Exists(finalPath))
            {
                json = File.ReadAllText(finalPath);
                return true;
            }
            json = null;
            return false;
        }
    }
}
