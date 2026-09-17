namespace Escape.Core
{
    /// <summary>
    /// Owns the live GameState. All mutations go through commands handled by
    /// the gameplay services; this is the single source of truth they mutate.
    /// </summary>
    public interface IGameStateService
    {
        GameState State { get; }
        void NewGame();
        void ReplaceState(GameState state);
    }

    public sealed class GameStateService : IGameStateService
    {
        public GameState State { get; private set; } = new GameState();

        public void NewGame() => State = new GameState();

        public void ReplaceState(GameState state) => State = state ?? new GameState();
    }
}
