using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// Ring buffer of every dispatched command. For debugging, QA,
    /// save/progression bugs and player bug reports.
    /// </summary>
    public sealed class CommandJournal
    {
        private const int Capacity = 512;
        private readonly Queue<string> _lines = new Queue<string>(Capacity);
        private readonly bool _echoToConsole;

        public IReadOnlyCollection<string> Lines => _lines;

        public CommandJournal(bool echoToConsole = true) => _echoToConsole = echoToConsole;

        public void Record(string command)
        {
            var line = $"{System.DateTime.Now:HH:mm:ss} {command}";
            _lines.Enqueue(line);
            while (_lines.Count > Capacity) _lines.Dequeue();
            if (_echoToConsole) Debug.Log($"[CMD] {line}");
        }

        public string Dump()
        {
            var sb = new StringBuilder();
            foreach (var l in _lines) sb.AppendLine(l);
            return sb.ToString();
        }

        public void Clear() => _lines.Clear();
    }
}
