namespace Escape.Core
{
    /// <summary>
    /// v1 → v2: the player pose was stored as the root's eulerAngles, which
    /// conflated yaw with (missing) camera pitch. Split into explicit Yaw
    /// and Pitch fields.
    /// </summary>
    public sealed class SaveMigrationV1ToV2 : ISaveMigration
    {
        public int FromVersion => 1;

        public SaveData Migrate(SaveData data)
        {
            if (data.player != null)
            {
                data.player.Yaw = data.player.EulerRotation.y;
                data.player.Pitch = data.player.EulerRotation.x;
            }
            data.version = 2;
            return data;
        }
    }

    /// <summary>
    /// v2 → v3: pose validity was inferred from "position is not the world
    /// origin", which made a legitimate save at the origin indistinguishable
    /// from a save that never captured a pose. v2 had no explicit flag, so the
    /// old inference is applied once here and recorded; from v3 on, HasPose is
    /// authoritative.
    /// </summary>
    public sealed class SaveMigrationV2ToV3 : ISaveMigration
    {
        public int FromVersion => 2;

        public SaveData Migrate(SaveData data)
        {
            if (data.player != null)
                data.player.HasPose = data.player.Position != UnityEngine.Vector3.zero;
            data.version = 3;
            return data;
        }
    }
}
