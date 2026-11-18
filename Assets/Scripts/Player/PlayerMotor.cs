using UnityEngine;
using Untai.Core;
using Untai.Data;

namespace Untai.Player
{
    /// <summary>
    /// Gerak samping 2.5D berbasis Rigidbody: Z dikunci, lompat variabel, coyote time,
    /// jump buffer, dan deteksi tanah dengan SphereCast.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class PlayerMotor : MonoBehaviour
    {
        // Kecepatan, lompat, dan gravitasi dibaca dari GameplayParameters.Active.
        float moveSpeed => GameplayParameters.Active.moveSpeed;
        float jumpVelocity => GameplayParameters.Active.jumpVelocity;
        float gravityMultiplier => GameplayParameters.Active.gravityMultiplier;

        [Header("Gerak")]
        public float groundAcceleration = 70f;
        public float airAcceleration = 35f;

        [Header("Lompat")]
        [Tooltip("Gravitasi tambahan saat tombol lompat dilepas ketika naik (lompat variabel).")]
        public float lowJumpMultiplier = 2f;
        public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.12f;
        public float maxFallSpeed = 25f;

        [Header("Tanah")]
        public float groundCheckExtra = 0.12f;

        public bool IsGrounded { get; private set; }
        /// <summary>Saat true (misal sedang mengayun), motor tidak mengatur kecepatan horizontal.</summary>
        public bool ExternalControl { get; set; }
        public bool InputEnabled { get; set; } = true;
        public Rigidbody Body { get; private set; }
        public float FacingSign { get; private set; } = 1f;

        CapsuleCollider _capsule;
        float _coyoteTimer;
        float _jumpBufferTimer;
        bool _jumpHeld;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            Body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.useGravity = true;
            // Tanpa gesekan agar tidak menempel di dinding platform (bila prefab belum punya material).
            if (_capsule.sharedMaterial == null) _capsule.sharedMaterial = CreateNoFrictionMaterial();
        }

        public static PhysicsMaterial CreateNoFrictionMaterial()
        {
            return new PhysicsMaterial("PlayerNoFriction")
            {
                dynamicFriction = 0f, staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f, bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }

        public static readonly Color DefaultColor = new Color(0.95f, 0.55f, 0.25f);

        /// <summary>
        /// Buat pemain dari prefab PrefabLibrary (fallback primitif), lalu pastikan semua komponen
        /// pemain dan benang ada.
        /// </summary>
        public static PlayerMotor Spawn(Vector3 position)
        {
            PlayerMotor m = null;
            var prefab = PrefabLibrary.Get(l => l.player);
            if (prefab != null) m = Prims.Spawn(prefab, position, null).GetComponent<PlayerMotor>();
            if (m == null) m = CreatePlayer(position);
            EnsureComponents(m.gameObject);
            return m;
        }

        /// <summary>Tambahkan komponen yang belum ada (urutan: benang, render benang, nyawa, ayunan).</summary>
        public static void EnsureComponents(GameObject go)
        {
            if (go.GetComponent<Thread.ThreadController>() == null) go.AddComponent<Thread.ThreadController>();
            if (go.GetComponent<Thread.ThreadRenderer>() == null) go.AddComponent<Thread.ThreadRenderer>();
            if (go.GetComponent<PlayerHealth>() == null) go.AddComponent<PlayerHealth>();
            if (go.GetComponent<Thread.ThreadSwing>() == null) go.AddComponent<Thread.ThreadSwing>();
        }

        void Update()
        {
            if (!InputEnabled) return;
            if (InputReader.JumpPressed) _jumpBufferTimer = jumpBufferTime;
            _jumpHeld = InputReader.JumpHeld;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            IsGrounded = CheckGround();
            _coyoteTimer = IsGrounded ? coyoteTime : _coyoteTimer - dt;
            _jumpBufferTimer -= dt;

            Vector3 v = Body.linearVelocity;
            float input = InputEnabled ? InputReader.Horizontal : 0f;
            if (Mathf.Abs(input) > 0.01f) FacingSign = Mathf.Sign(input);

            if (!ExternalControl)
            {
                float accel = IsGrounded ? groundAcceleration : airAcceleration;
                // Di udara, momentum hasil ayunan dipertahankan selama input searah.
                bool keepMomentum = !IsGrounded && Mathf.Abs(v.x) > moveSpeed &&
                                    (Mathf.Abs(input) < 0.01f || Mathf.Sign(input) == Mathf.Sign(v.x));
                if (keepMomentum) accel *= 0.15f;
                v.x = Mathf.MoveTowards(v.x, input * moveSpeed, accel * dt);

                // Lompat memakai coyote time dan jump buffer agar terasa responsif.
                if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
                {
                    v.y = jumpVelocity;
                    _jumpBufferTimer = 0f;
                    _coyoteTimer = 0f;
                }
            }

            // Gravitasi tambahan: jatuh lebih cepat, dan lompat pendek jika tombol dilepas.
            float extra = gravityMultiplier - 1f;
            if (!ExternalControl && v.y > 0f && !_jumpHeld) extra += lowJumpMultiplier;
            v += Physics.gravity * (extra * dt);
            if (v.y < -maxFallSpeed) v.y = -maxFallSpeed;
            v.z = 0f;
            Body.linearVelocity = v;
        }

        bool CheckGround()
        {
            float radius = _capsule.radius * 0.9f;
            Vector3 origin = transform.position + _capsule.center;
            float dist = _capsule.height * 0.5f - radius + groundCheckExtra;
            if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, dist, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.normal.y > 0.5f && Body.linearVelocity.y <= 0.5f;
            }
            return false;
        }

        public void TeleportTo(Vector3 position)
        {
            position.z = 0f;
            Body.position = position;
            transform.position = position;
            Body.linearVelocity = Vector3.zero;
        }

        /// <summary>Versi primitif dari kode: kapsul + Rigidbody + PlayerMotor (fallback, juga dipakai generator prefab).</summary>
        public static PlayerMotor CreatePlayer(Vector3 position)
        {
            var go = Prims.Create(PrimitiveType.Capsule, "Player", position, Vector3.one, DefaultColor, null);
            // "Mata" kecil supaya arah hadap terlihat.
            Prims.Create(PrimitiveType.Sphere, "Eye", position + new Vector3(0.3f, 0.45f, -0.35f),
                Vector3.one * 0.22f, Color.white, go.transform, false);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            return go.AddComponent<PlayerMotor>();
        }
    }
}
