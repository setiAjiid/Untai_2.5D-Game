using System;
using UnityEngine;

namespace Untai.Core
{
    public enum DeathCause { Fall, Damage, Simulated }

    /// <summary>
    /// Event global gameplay. Modul lain (DDA, HUD) cukup berlangganan di sini
    /// sehingga Player, Level, dan Thread tidak perlu tahu siapa pendengarnya.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<DeathCause> PlayerDied;
        public static event Action<int> PlayerDamaged;
        /// <summary>(posisi checkpoint, rasio sisa benang sebelum dipulihkan)</summary>
        public static event Action<Vector3, float> CheckpointReached;
        /// <summary>Indeks chunk yang baru dimasuki pemain.</summary>
        public static event Action<int> ChunkEntered;
        public static event Action GoalReached;

        public static void RaisePlayerDied(DeathCause cause) => PlayerDied?.Invoke(cause);
        public static void RaisePlayerDamaged(int amount) => PlayerDamaged?.Invoke(amount);
        public static void RaiseCheckpointReached(Vector3 pos, float ratio) => CheckpointReached?.Invoke(pos, ratio);
        public static void RaiseChunkEntered(int index) => ChunkEntered?.Invoke(index);
        public static void RaiseGoalReached() => GoalReached?.Invoke();
    }
}
