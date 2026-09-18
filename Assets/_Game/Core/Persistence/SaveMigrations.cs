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
}
