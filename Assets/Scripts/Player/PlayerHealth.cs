using UnityEngine;
using Untai.Core;
using Untai.Thread;

namespace Untai.Player
{
    /// <summary>
    /// Nyawa pemain, damage dengan knockback dan kebal sesaat, serta respawn
    /// ke checkpoint terakhir (titik tambat benang).
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(ThreadController))]
    public class PlayerHealth : MonoBehaviour
    {
        public int maxHealth = 3;
        public float invulnerableTime = 1.0f;

        public int Health { get; private set; }
        public bool IsInvulnerable => Time.time < _invulnerableUntil;

        PlayerMotor _motor;
        ThreadController _thread;
        float _invulnerableUntil;
        float _lastDeathTime = -10f;
        Renderer[] _renderers;
        bool _visible = true;

        /// <summary>Dipanggil sebelum respawn (misal untuk melepas ayunan).</summary>
        public event System.Action BeforeRespawn;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _thread = GetComponent<ThreadController>();
            _renderers = GetComponentsInChildren<Renderer>(); // tubuh + mata ikut berkedip
            Health = maxHealth;
        }

        void Update()
        {
            // Kedip saat kebal.
            bool show = !IsInvulnerable || Mathf.Repeat(Time.time * 12f, 1f) > 0.35f;
            if (show == _visible) return;
            _visible = show;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = show;
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
            _motor.Body.linearVelocity = new Vector3(side * 7f, 7f, 0f);
        }

        public void Die(DeathCause cause)
        {
            if (Time.time - _lastDeathTime < 0.25f) return; // cegah kematian ganda dalam satu momen
            _lastDeathTime = Time.time;
            GameEvents.RaisePlayerDied(cause);
            Respawn();
        }

        public void Respawn()
        {
            BeforeRespawn?.Invoke();
            Health = maxHealth;
            _invulnerableUntil = Time.time + invulnerableTime;
            _motor.TeleportTo(_thread.TetherPoint + Vector3.up * 1.2f);
            _thread.ResetTrail(); // jejak benang dimulai lagi dari titik tambat
        }

        public void ResetFull()
        {
            Health = maxHealth;
            _invulnerableUntil = 0f;
        }
    }
}
