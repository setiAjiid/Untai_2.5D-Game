using UnityEngine;
using Untai.Core;
using Untai.Player;

namespace Untai.Level
{
    /// <summary>Jebakan duri: menyentuhnya memberi 1 damage dan knockback.</summary>
    public class Trap : MonoBehaviour
    {
        public int damage = 1;

        public static Trap Create(Vector3 groundPos, Transform parent)
        {
            var root = new GameObject("Trap");
            root.transform.SetParent(parent, false);
            root.transform.position = groundPos;
            var red = new Color(0.9f, 0.2f, 0.2f);
            for (int i = -1; i <= 1; i++)
            {
                var spike = Prims.Box("Spike", groundPos + new Vector3(i * 0.35f, 0.25f, 0f),
                    new Vector3(0.3f, 0.3f, 1.2f), red, root.transform, false);
                spike.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            }
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.3f, 0f);
            col.size = new Vector3(1.1f, 0.6f, 2f);
            return root.AddComponent<Trap>();
        }

        void OnTriggerStay(Collider other)
        {
            var hp = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<PlayerHealth>() : null;
            if (hp != null) hp.TakeDamage(damage, transform.position);
        }
    }
}
