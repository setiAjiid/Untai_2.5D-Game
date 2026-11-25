using System.Collections.Generic;
using UnityEngine;
using Untai.Core;
using Untai.Data;

namespace Untai.Level
{
    /// <summary>
    /// Peti yang bisa ditarik dengan benang dan dijadikan pijakan.
    /// Peti kinematik: digerakkan halus lewat kode (bukan gaya fisika) supaya stabil sebagai pijakan,
    /// tidak bisa didorong pemain, tidak menembus platform (BoxCast ke arah gerak), dan tidak terbang
    /// (hanya gerak horizontal dari tarikan, plus jatuh manual bila tidak ada pijakan di bawahnya).
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class PullCrate : MonoBehaviour
    {
        public static readonly List<PullCrate> All = new List<PullCrate>();

        public const float DefaultSize = 1.2f;
        public static readonly Color DefaultColor = new Color(0.62f, 0.43f, 0.24f);
        public static readonly Color DefaultHighlightColor = new Color(1f, 0.9f, 0.35f);

        [Tooltip("Renderer yang berganti material saat peti bisa dikait.")]
        public Renderer targetRenderer;
        public Material idleMaterial;
        public Material highlightMaterial;
        public float gravity = 25f;
        public float maxFallSpeed = 20f;
        [Tooltip("Di bawah tinggi ini peti dikembalikan ke posisi awal (jatuh ke jurang).")]
        public float resetHeight = -6f;

        const float Skin = 0.02f;

        Rigidbody _body;
        BoxCollider _box;
        Vector3 _spawn;
        float _fallSpeed;
        float _pullVx;
        bool _highlighted;

        /// <summary>Buat peti dari prefab PrefabLibrary, atau primitif bila prefab kosong. groundPos = permukaan.</summary>
        public static PullCrate Create(Vector3 groundPos, Transform parent)
        {
            Vector3 pos = groundPos + Vector3.up * (DefaultSize * 0.5f + Skin);
            var prefab = PrefabLibrary.Get(l => l.pullCrate);
            if (prefab != null)
            {
                var c = Prims.Spawn(prefab, pos, parent).GetComponent<PullCrate>();
                if (c != null) return c;
            }
            return CreatePrimitive(pos, parent);
        }

        /// <summary>Versi primitif dari kode (fallback, juga dipakai generator prefab).</summary>
        public static PullCrate CreatePrimitive(Vector3 center, Transform parent)
        {
            var go = Prims.Box("PullCrate", center, Vector3.one * DefaultSize, DefaultColor, parent);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var crate = go.AddComponent<PullCrate>();
            crate.targetRenderer = go.GetComponent<Renderer>();
            return crate;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _box = GetComponent<BoxCollider>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        }

        void Start() => _spawn = transform.position;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Setengah ukuran dunia peti (collider x skala).</summary>
        public Vector3 HalfExtents => Vector3.Scale(_box.size, transform.lossyScale) * 0.5f;

        /// <summary>Kecepatan tarik horizontal untuk langkah fisika berikutnya (diatur ThreadPull tiap FixedUpdate).</summary>
        public void SetPull(float vx) => _pullVx = vx;

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 pos = _body.position;
            Vector3 half = HalfExtents;

            // Tarikan horizontal: berhenti tepat sebelum dinding/pemain. Kotak cast sedikit diangkat dan
            // dipendekkan agar tidak mengenai lantai yang sedang dipijak.
            if (Mathf.Abs(_pullVx) > 0.001f)
            {
                float dir = Mathf.Sign(_pullVx);
                float dx = Mathf.Abs(_pullVx) * dt;
                var castHalf = new Vector3(half.x - Skin, half.y - 0.08f, half.z - Skin);
                if (Physics.BoxCast(pos + Vector3.up * 0.06f, castHalf, Vector3.right * dir, out RaycastHit hit,
                        Quaternion.identity, dx + Skin, ~0, QueryTriggerInteraction.Ignore))
                    dx = Mathf.Max(0f, hit.distance - Skin);
                pos.x += dir * dx;
            }
            _pullVx = 0f;

            // Jatuh manual: turun sampai menyentuh permukaan di bawahnya.
            float fall = Mathf.Min(_fallSpeed + gravity * dt, maxFallSpeed) * dt;
            var downHalf = new Vector3(half.x - Skin, half.y - Skin, half.z - Skin);
            bool grounded = false;
            if (Physics.BoxCast(pos, downHalf, Vector3.down, out RaycastHit ground, Quaternion.identity,
                    fall + Skin, ~0, QueryTriggerInteraction.Ignore))
            {
                float allowed = Mathf.Max(0f, ground.distance - Skin);
                if (allowed < fall) { fall = allowed; grounded = true; }
            }
            pos.y -= fall;
            _fallSpeed = grounded ? 0f : Mathf.Min(_fallSpeed + gravity * dt, maxFallSpeed);

            if (pos.y < resetHeight)
            {
                pos = _spawn;
                _fallSpeed = 0f;
            }
            pos.z = 0f;
            _body.MovePosition(pos);
        }

        public void SetHighlight(bool on)
        {
            if (_highlighted == on || targetRenderer == null) return;
            _highlighted = on;
            targetRenderer.sharedMaterial = on
                ? Prims.MatOr(highlightMaterial, DefaultHighlightColor)
                : Prims.MatOr(idleMaterial, DefaultColor);
        }
    }
}
