using System.Collections.Generic;
using UnityEngine;
using Untai.Core;
using Untai.Player;

namespace Untai.AI
{
    public enum EnemyState { Patrol, Chase, Attack, Return }

    /// <summary>
    /// Enemy dengan finite state machine: Patrol -> Chase -> Attack -> (Chase | Return) -> Patrol.
    /// Gerakan dibatasi pada platform tempat enemy muncul (minX..maxX).
    /// Warna tubuh dan label di atas kepala menunjukkan state aktif.
    /// </summary>
    public class EnemyBrain : MonoBehaviour
    {
        public static readonly List<EnemyBrain> All = new List<EnemyBrain>();
        static int s_NextId;

        [Header("Batas platform")]
        public float minX;
        public float maxX;

        [Header("Perilaku")]
        public float patrolSpeed = 1.5f;
        public float detectRange = 5f;
        public float chaseSpeed = 3f;
        public float attackRange = 1.6f;
        public float attackWindup = 0.5f;
        public float attackCooldown = 1.5f;
        public float lungeDistance = 2.2f;
        public float lungeDuration = 0.2f;
        public float loseSightTime = 1.0f;

        public int Id { get; private set; }
        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public float StateTime => Time.time - _stateEnter;
        public PlayerHealth Target { get; set; }

        [Header("Tampilan (diisi prefab)")]
        [Tooltip("Renderer tubuh yang berganti material sesuai state.")]
        public Renderer bodyRenderer;
        [Tooltip("Label state di atas kepala (opsional).")]
        public TextMesh stateLabel;
        public Material patrolMaterial;
        public Material chaseMaterial;
        public Material attackMaterial;
        public Material returnMaterial;
        [Tooltip("Selisih tinggi pivot terhadap titik spawn (titik spawn = permukaan platform + 1).")]
        public float heightOffset = -0.2f;

        static readonly Color PatrolColor = new Color(0.35f, 0.8f, 0.4f);
        static readonly Color ChaseColor = new Color(1f, 0.6f, 0.15f);
        static readonly Color AttackColor = new Color(1f, 0.15f, 0.15f);
        static readonly Color ReturnColor = new Color(0.35f, 0.55f, 1f);

        float _homeX;
        float _y;
        float _dir = 1f;
        float _stateEnter;
        float _cooldownUntil;
        float _lastSeen;
        bool _lunging;
        bool _hitThisAttack;
        float _lungeStartX;

        /// <summary>Buat enemy dari prefab PrefabLibrary, atau primitif bila prefab kosong.</summary>
        public static EnemyBrain Create(Vector3 position, float minX, float maxX, PlayerHealth target, Transform parent)
        {
            EnemyBrain e = null;
            var prefab = Data.PrefabLibrary.Get(l => l.enemy);
            if (prefab != null) e = Prims.Spawn(prefab, position, parent).GetComponent<EnemyBrain>();
            if (e == null) e = CreatePrimitive(position, parent);

            e.minX = minX;
            e.maxX = Mathf.Max(minX, maxX);
            e.Target = target;
            e._homeX = Mathf.Clamp(position.x, e.minX, e.maxX);
            e._y = position.y + e.heightOffset;
            e.Id = ++s_NextId;
            e.ApplyAggression(Data.GameplayParameters.Active.aggression);
            e.EnterState(EnemyState.Patrol);
            return e;
        }

        /// <summary>Versi primitif dari kode (fallback, juga dipakai generator prefab).</summary>
        public static EnemyBrain CreatePrimitive(Vector3 position, Transform parent)
        {
            var go = Prims.Create(PrimitiveType.Capsule, "Enemy", position, new Vector3(0.9f, 0.8f, 0.9f),
                PatrolColor, parent);
            go.GetComponent<Collider>().isTrigger = true;
            Prims.Create(PrimitiveType.Cube, "Visor", position + new Vector3(0f, 0.25f, -0.4f),
                new Vector3(0.6f, 0.15f, 0.1f), Color.black, go.transform, false);
            var e = go.AddComponent<EnemyBrain>();
            e.bodyRenderer = go.GetComponent<Renderer>();
            e.stateLabel = Prims.Label("", position + Vector3.up * 1.4f, go.transform, Color.white, 0.06f);
            return e;
        }

        /// <summary>Agresivitas yang sedang dipakai enemy ini (0..1).</summary>
        public float Aggression { get; private set; } = -1f;

        /// <summary>
        /// Terjemahkan agresivitas (dari DDA lewat GameplayParameters) ke perilaku:
        /// jarak deteksi makin jauh, kejaran makin cepat, ancang-ancang dan jeda serangan makin singkat.
        /// </summary>
        public void ApplyAggression(float a)
        {
            a = Mathf.Clamp01(a);
            Aggression = a;
            detectRange = Mathf.Lerp(3f, 9f, a);
            chaseSpeed = Mathf.Lerp(1.8f, 5.5f, a);
            attackCooldown = Mathf.Lerp(2.4f, 0.6f, a);
            attackWindup = Mathf.Lerp(0.75f, 0.3f, a);
            patrolSpeed = Mathf.Lerp(1.0f, 2.2f, a);
            loseSightTime = Mathf.Lerp(0.6f, 2.5f, a);
        }

        void OnEnable()
        {
            All.Add(this);
            Data.GameplayParameters.Changed += OnParametersChanged;
            ApplyAggression(Data.GameplayParameters.Active.aggression);
        }

        void OnDisable()
        {
            All.Remove(this);
            Data.GameplayParameters.Changed -= OnParametersChanged;
        }

        // Enemy yang sudah ada langsung memakai agresivitas terbaru hasil DDA.
        void OnParametersChanged() => ApplyAggression(Data.GameplayParameters.Active.aggression);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            s_NextId = 0;
        }

        void EnterState(EnemyState s)
        {
            State = s;
            _stateEnter = Time.time;
            _lunging = false;
            _hitThisAttack = false;
            if (bodyRenderer != null)
            {
                Material m = s == EnemyState.Patrol ? patrolMaterial : s == EnemyState.Chase ? chaseMaterial
                    : s == EnemyState.Attack ? attackMaterial : returnMaterial;
                bodyRenderer.sharedMaterial = Prims.MatOr(m, StateColor(s));
            }
            if (stateLabel != null) stateLabel.text = "E" + Id + " " + StateLabel(s);
        }

        public static string StateLabel(EnemyState s)
        {
            switch (s)
            {
                case EnemyState.Patrol: return "PATROL";
                case EnemyState.Chase: return "CHASE";
                case EnemyState.Attack: return "ATTACK";
                default: return "RETURN";
            }
        }

        /// <summary>Pemain terlihat jika dalam jarak deteksi, setinggi platform, dan berada di area platform.</summary>
        bool CanSeeTarget()
        {
            if (Target == null) return false;
            Vector3 p = Target.transform.position;
            float dx = p.x - transform.position.x;
            float dy = p.y - transform.position.y;
            return Mathf.Abs(dx) <= detectRange && Mathf.Abs(dy) <= 2.5f && p.x >= minX - 1.5f && p.x <= maxX + 1.5f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool sees = CanSeeTarget();
            if (sees) _lastSeen = Time.time;
            float x = transform.position.x;

            switch (State)
            {
                case EnemyState.Patrol:
                    // Bolak-balik di atas platform.
                    x += _dir * patrolSpeed * dt;
                    if (x >= maxX) { x = maxX; _dir = -1f; }
                    else if (x <= minX) { x = minX; _dir = 1f; }
                    if (sees) EnterState(EnemyState.Chase);
                    break;

                case EnemyState.Chase:
                {
                    float tx = Target.transform.position.x;
                    _dir = Mathf.Sign(tx - x);
                    x = Mathf.MoveTowards(x, Mathf.Clamp(tx, minX, maxX), chaseSpeed * dt);
                    if (sees && Mathf.Abs(tx - x) <= attackRange && Time.time >= _cooldownUntil)
                        EnterState(EnemyState.Attack);
                    else if (!sees && Time.time - _lastSeen > loseSightTime)
                        EnterState(EnemyState.Return);
                    break;
                }

                case EnemyState.Attack:
                    x = UpdateAttack(x);
                    break;

                case EnemyState.Return:
                    _dir = Mathf.Sign(_homeX - x);
                    x = Mathf.MoveTowards(x, _homeX, patrolSpeed * 1.5f * dt);
                    if (sees) EnterState(EnemyState.Chase);
                    else if (Mathf.Abs(x - _homeX) < 0.05f) EnterState(EnemyState.Patrol);
                    break;
            }

            x = Mathf.Clamp(x, minX, maxX);
            transform.position = new Vector3(x, _y + (State == EnemyState.Attack && !_lunging ? 0.1f * Mathf.Sin(Time.time * 60f) : 0f), 0f);
        }

        public static Color StateColor(EnemyState s) =>
            s == EnemyState.Patrol ? PatrolColor : s == EnemyState.Chase ? ChaseColor
            : s == EnemyState.Attack ? AttackColor : ReturnColor;

        // Gizmos: jarak deteksi (warna state), jarak serang, dan batas patroli platform.
        void OnDrawGizmos()
        {
            Vector3 p = transform.position;
            Gizmos.color = StateColor(State);
            Gizmos.DrawWireSphere(p, detectRange);
            Gizmos.color = AttackColor;
            Gizmos.DrawWireSphere(p, attackRange);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(new Vector3(minX, p.y - 0.9f, 0f), new Vector3(maxX, p.y - 0.9f, 0f));
            Gizmos.DrawLine(new Vector3(minX, p.y - 1.2f, 0f), new Vector3(minX, p.y - 0.6f, 0f));
            Gizmos.DrawLine(new Vector3(maxX, p.y - 1.2f, 0f), new Vector3(maxX, p.y - 0.6f, 0f));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(p + Vector3.up * 1.8f, "E" + Id + " " + StateLabel(State));
#endif
        }

        /// <summary>Serangan: ancang-ancang (windup, bergetar merah) lalu menerjang searah pemain.</summary>
        float UpdateAttack(float x)
        {
            float t = StateTime;
            if (!_lunging)
            {
                if (Target != null) _dir = Mathf.Sign(Target.transform.position.x - x);
                if (t >= attackWindup)
                {
                    _lunging = true;
                    _lungeStartX = x;
                }
                return x;
            }

            float k = Mathf.Clamp01((t - attackWindup) / lungeDuration);
            x = Mathf.Clamp(_lungeStartX + _dir * lungeDistance * k, minX, maxX);
            if (!_hitThisAttack && Target != null)
            {
                Vector3 d = Target.transform.position - new Vector3(x, _y, 0f);
                if (Mathf.Abs(d.x) < 0.9f && Mathf.Abs(d.y) < 1.4f)
                {
                    _hitThisAttack = true;
                    Target.TakeDamage(1, new Vector3(x, _y, 0f));
                }
            }
            if (k >= 1f)
            {
                _cooldownUntil = Time.time + attackCooldown;
                EnterState(CanSeeTarget() ? EnemyState.Chase : EnemyState.Return);
            }
            return x;
        }
    }
}
