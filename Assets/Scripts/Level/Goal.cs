using UnityEngine;
using Untai.Core;
using Untai.Player;

namespace Untai.Level
{
    /// <summary>Gerbang akhir level. Menyentuhnya memicu GameEvents.GoalReached sekali.</summary>
    public class Goal : MonoBehaviour
    {
        bool _reached;

        public static Goal Create(Vector3 groundPos, Transform parent)
        {
            var root = new GameObject("Goal");
            root.transform.SetParent(parent, false);
            root.transform.position = groundPos;
            var gold = new Color(1f, 0.8f, 0.2f);
            Prims.Box("PostL", groundPos + new Vector3(-0.8f, 1.5f, 0f), new Vector3(0.3f, 3f, 0.3f), gold, root.transform, false);
            Prims.Box("PostR", groundPos + new Vector3(0.8f, 1.5f, 0f), new Vector3(0.3f, 3f, 0.3f), gold, root.transform, false);
            Prims.Box("Top", groundPos + new Vector3(0f, 3.1f, 0f), new Vector3(1.9f, 0.3f, 0.3f), gold, root.transform, false);
            Prims.Label("GOAL", groundPos + new Vector3(0f, 3.8f, 0f), root.transform, gold, 0.15f);
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * 1.5f;
            trigger.size = new Vector3(1.6f, 3f, 3f);
            return root.AddComponent<Goal>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (_reached) return;
            if (other.attachedRigidbody == null || other.attachedRigidbody.GetComponent<PlayerMotor>() == null) return;
            _reached = true;
            GameEvents.RaiseGoalReached();
        }
    }
}
