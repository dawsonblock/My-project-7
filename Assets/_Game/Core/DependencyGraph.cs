using System;
using System.Collections.Generic;

namespace Escape.Core
{
    /// <summary>
    /// Tri-state DFS cycle detection over a string-keyed dependency graph.
    /// A node revisited while still Visiting is a real cycle; a Visited node
    /// is a shared dependency (diamond), which is legal.
    /// </summary>
    public static class DependencyGraph
    {
        private enum VisitState { Visiting, Visited }

        /// <summary>
        /// Returns true when any dependency cycle exists; cycleAt names a
        /// node on the cycle. Unknown ids in the edge map are leaves.
        /// </summary>
        public static bool HasCycle(IEnumerable<string> nodes,
            Func<string, IEnumerable<string>> dependencies, out string cycleAt)
        {
            var state = new Dictionary<string, VisitState>(StringComparer.Ordinal);
            string at = null;
            foreach (var n in nodes)
                if (n != null && Dfs(n, dependencies, state, ref at))
                {
                    cycleAt = at;
                    return true;
                }
            cycleAt = null;
            return false;
        }

        private static bool Dfs(string node,
            Func<string, IEnumerable<string>> deps,
            Dictionary<string, VisitState> state, ref string cycleAt)
        {
            if (state.TryGetValue(node, out var s))
            {
                if (s == VisitState.Visiting) { cycleAt = node; return true; }
                return false; // Visited — already proven acyclic
            }
            state[node] = VisitState.Visiting;
            var edges = deps(node);
            if (edges != null)
                foreach (var dep in edges)
                    if (dep != null && Dfs(dep, deps, state, ref cycleAt))
                        return true;
            state[node] = VisitState.Visited;
            return false;
        }
    }
}
