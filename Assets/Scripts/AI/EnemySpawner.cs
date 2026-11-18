using UnityEngine;
using Untai.Level;
using Untai.Player;

namespace Untai.AI
{
    /// <summary>
    /// Membuat enemy saat LevelBuilder meminta (titik spawn dari chunk hasil PCG)
    /// dan membersihkannya saat level dibangun ulang.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        public LevelBuilder level;
        public PlayerHealth player;

        Transform _root;

        public void Bind(LevelBuilder builder, PlayerHealth target)
        {
            level = builder;
            player = target;
            level.EnemySpawnRequested += Spawn;
            level.LevelCleared += Clear;
        }

        void OnDestroy()
        {
            if (level != null)
            {
                level.EnemySpawnRequested -= Spawn;
                level.LevelCleared -= Clear;
            }
        }

        void Spawn(EnemySpawnInfo info)
        {
            if (_root == null) _root = new GameObject("Enemies").transform;
            OnEnemyCreated(EnemyBrain.Create(info.position, info.minX, info.maxX, player, _root));
        }

        protected virtual void OnEnemyCreated(EnemyBrain e) { }

        void Clear()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
        }
    }
}
