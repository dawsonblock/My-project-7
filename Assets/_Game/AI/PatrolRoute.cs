using UnityEngine;

namespace Escape.AI
{
    /// <summary>
    /// Ordered waypoints for a guard. Each waypoint can carry a wait time
    /// and a look direction.
    /// </summary>
    public sealed class PatrolRoute : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Waypoint
        {
            public Transform Point;
            public float WaitSeconds = 1.5f;
            public bool LookAround = true;
        }

        public Waypoint[] Waypoints = new Waypoint[0];
        public bool Loop = true;

        public int Count => Waypoints.Length;

        public Waypoint Get(int index) => Waypoints[index];

        private void OnDrawGizmos()
        {
            if (Waypoints == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            for (int i = 0; i < Waypoints.Length; i++)
            {
                var w = Waypoints[i];
                if (w?.Point == null) continue;
                Gizmos.DrawWireSphere(w.Point.position, 0.25f);
                int next = i + 1;
                if (next >= Waypoints.Length) next = Loop ? 0 : -1;
                if (next >= 0 && Waypoints[next]?.Point != null)
                    Gizmos.DrawLine(w.Point.position, Waypoints[next].Point.position);
            }
        }
    }
}
