using System;
using UnityEngine;
using Untai.Core;
using UnityEngine.InputSystem;

namespace Untai.Player
{
    /// <summary>
    /// Pasangan dua karakter yang terikat satu untai, dimainkan satu orang.
    /// Tab/Q mengganti karakter aktif; karakter lain diam dan menjadi tumpuan untai.
    /// Bila salah satu mati, keduanya respawn bersama di checkpoint terakhir.
    /// </summary>
    public class Tandem : MonoBehaviour
    {
        public static Tandem Instance { get; private set; }

        public PlayerMotor characterA;
        public PlayerMotor characterB;
        [Tooltip("Jarak horizontal karakter kedua dari karakter pertama saat respawn.")]
        public float respawnSpacing = 1.2f;

        public PlayerMotor Active { get; private set; }
        public PlayerMotor Partner => Active == characterA ? characterB : characterA;
        public PlayerHealth ActiveHealth => Active != null ? Active.GetComponent<PlayerHealth>() : null;
        public int SwitchCount { get; private set; }

        /// <summary>Dipanggil setelah karakter aktif berganti (karakter aktif baru).</summary>
        public event Action<PlayerMotor> Switched;
        /// <summary>Dipanggil setelah keduanya respawn.</summary>
        public event Action Respawned;

        Vector3 _fallbackRespawn = new Vector3(2.5f, 1.2f, 0f);

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (PlayerHealth.RespawnHandler == (Action<PlayerHealth>)HandleDeath) PlayerHealth.RespawnHandler = null;
        }

        public void Bind(PlayerMotor a, PlayerMotor b)
        {
            characterA = a;
            characterB = b;
            PlayerHealth.RespawnHandler = HandleDeath;
            SetActive(a, false);
        }

        void Update()
        {
            if (characterA == null || characterB == null) return;
            if (!_locked && (InputReader.KeyDown(Key.Tab) || InputReader.KeyDown(Key.Q))) SwitchCharacter();
        }

        public void SwitchCharacter()
        {
            SetActive(Partner, true);
            SwitchCount++;
        }

        void SetActive(PlayerMotor next, bool notify)
        {
            Active = next;
            ApplyInput();
            if (notify) Switched?.Invoke(Active);
        }

        bool _locked;
        /// <summary>Kunci input kedua karakter (misal saat level selesai).</summary>
        public bool Locked
        {
            get => _locked;
            set { _locked = value; ApplyInput(); }
        }

        void ApplyInput()
        {
            if (characterA == null || characterB == null) return;
            characterA.InputEnabled = !_locked && Active == characterA;
            characterB.InputEnabled = !_locked && Active == characterB;
        }

        /// <summary>Posisi x terdepan dari kedua karakter (dipakai LevelBuilder untuk melacak chunk).</summary>
        public float LeadX => Mathf.Max(characterA.transform.position.x, characterB.transform.position.x);

        /// <summary>Karakter terdekat dari suatu posisi (dipakai enemy memilih target).</summary>
        public PlayerHealth ClosestTo(Vector3 p)
        {
            float da = (characterA.transform.position - p).sqrMagnitude;
            float db = (characterB.transform.position - p).sqrMagnitude;
            return (da <= db ? characterA : characterB).GetComponent<PlayerHealth>();
        }

        public void SetFallbackRespawn(Vector3 pos) => _fallbackRespawn = pos;

        /// <summary>Teleport keduanya ke titik respawn dan pulihkan nyawa.</summary>
        public void RespawnAll(Vector3 point)
        {
            point.z = 0f;
            Active.TeleportTo(point);
            Partner.TeleportTo(point - new Vector3(respawnSpacing, 0f, 0f));
            characterA.GetComponent<PlayerHealth>().ResetAfterRespawn();
            characterB.GetComponent<PlayerHealth>().ResetAfterRespawn();
            Respawned?.Invoke();
        }

        void HandleDeath(PlayerHealth who)
        {
            RespawnAll(Level.Checkpoint.Active != null ? Level.Checkpoint.Active.RespawnPoint : _fallbackRespawn);
        }
    }
}
