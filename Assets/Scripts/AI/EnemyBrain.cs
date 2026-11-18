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
        Renderer _renderer;
        TextMesh _label;

        public static EnemyBrain Create(Vector3 position, float minX, float maxX, PlayerHealth target, Transform parent)
        {
            var go = Prims.Create(PrimitiveType.Capsule, "Enemy", position, new Vector3(0.9f, 0.8f, 0.9f),
                PatrolColor, parent);
            go.GetComponent<Collider>().isTrigger = true;
            Prims.Create(PrimitiveType.Cube, "Visor", position + new Vector3(0f, 0.25f, -0.4f),
                new Vector3(0.6f, 0.15f, 0.1f), Color.black, go.transform, false);
            var e = go.AddComponent<EnemyBrain>();
            e.minX = minX;
            e.maxX = Mathf.Max(minX, maxX);
            e.Target = target;
            e._homeX = Mathf.Clamp(position.x, e.minX, e.maxX);
            e._y = position.y - 0.2f;
            e._renderer = go.GetComponent<Renderer>();
            e._label = Prims.Label("", position + Vector3.up * 1.4f, go.transform, Color.white, 0.06f);
            e.Id = ++s_NextId;
            e.EnterState(EnemyState.Patrol);
            return e;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

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
            if (_renderer != null)
            {
                Color c = s == EnemyState.Patrol ? PatrolColor : s == EnemyState.Chase ? ChaseColor
                    : s == EnemyState.Attack ? AttackColor : ReturnColor;
                _renderer.sharedMaterial = Prims.Mat(c);
            }
            if (_label != null) _label.text = "E" + Id + " " + StateLabel(s);
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
