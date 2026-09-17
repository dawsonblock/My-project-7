using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Named location SceneService drops the player at after a scene load.
    /// </summary>
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        public string SpawnId = "default";

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        }
    }
}
