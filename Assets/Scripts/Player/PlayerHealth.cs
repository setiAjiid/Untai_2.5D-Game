using System;
using UnityEngine;
using Untai.Core;

namespace Untai.Player
{
    /// <summary>
    /// Nyawa satu karakter: damage dengan knockback dan kebal sesaat. Saat mati, respawn
    /// diserahkan ke RespawnHandler (Tandem: kedua karakter respawn bersama di checkpoint).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerHealth : MonoBehaviour
    {
        public int maxHealth = 3;
        public float invulnerableTime = 1.5f;

        public int Health { get; private set; }
        public float HealthRatio => maxHealth > 0 ? Health / (float)maxHealth : 0f;
        public bool IsInvulnerable => Time.time < _invulnerableUntil;

        /// <summary>Penanganan respawn global (diisi Tandem). Null = respawn sendiri di checkpoint.</summary>
        public static Action<PlayerHealth> RespawnHandler;

        PlayerMotor _motor;
        float _invulnerableUntil;
        Renderer _renderer;
        // Dibagi antar karakter: dua karakter jatuh bersamaan dihitung satu kematian.
        static float s_LastDeathTime = -10f;

        /// <summary>Dipanggil sebelum respawn.</summary>
        public event Action BeforeRespawn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            RespawnHandler = null;
            s_LastDeathTime = -10f;
        }

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _renderer = GetComponent<Renderer>();
            Health = maxHealth;
        }

        void Update()
        {
            // Kedip saat kebal.
            if (_renderer != null)
                _renderer.enabled = !IsInvulnerable || Mathf.Repeat(Time.time * 12f, 1f) > 0.35f;
        }

        public void TakeDamage(int amount, Vector3 sourcePosition)
        {
            if (IsInvulnerable || amount <= 0) return;
            Health -= amount;
            GameEvents.RaisePlayerDamaged(amount);
            if (Health <= 0)
            {
                Die(DeathCause.Damage);
                return;
            }
            _invulnerableUntil = Time.time + invulnerableTime;
            float side = Mathf.Sign(transform.position.x - sourcePosition.x);
            if (side == 0f) side = 1f;
            // Knockback kecil agar tidak mudah terpental ke jurang.
            _motor.Body.linearVelocity = new Vector3(side * 3.5f, 5f, 0f);
        }

        public void Die(DeathCause cause)
        {
            if (Time.time - s_LastDeathTime < 0.25f) return; // cegah kematian ganda dalam satu momen
            s_LastDeathTime = Time.time;
            GameEvents.RaisePlayerDied(cause);
            Respawn();
        }

        public void Respawn()
        {
            if (RespawnHandler != null)
            {
                RespawnHandler(this);
                return;
            }
            var cp = Level.Checkpoint.Active;
            _motor.TeleportTo(cp != null ? cp.RespawnPoint : transform.position + Vector3.up);
            ResetAfterRespawn();
        }

        /// <summary>Pulihkan nyawa dan beri kebal sesaat setelah respawn.</summary>
        public void ResetAfterRespawn()
        {
            BeforeRespawn?.Invoke();
            Health = maxHealth;
            _invulnerableUntil = Time.time + invulnerableTime;
        }

        public void ResetFull()
        {
            Health = maxHealth;
            _invulnerableUntil = 0f;
        }
    }
}
