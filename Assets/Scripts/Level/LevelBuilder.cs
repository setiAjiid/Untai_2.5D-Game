using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Untai.Core;
using Untai.Data;
using Untai.PCG;
using Untai.Player;
using Untai.Thread;

namespace Untai.Level
{
    /// <summary>Informasi chunk yang sudah dibangun di dunia.</summary>
    public class BuiltChunk
    {
        public int index;
        public ChunkDefinition def;
        public float startX;
        public float endX;
        public int attempts;
        public bool fallback;
        public bool hasCheckpoint;
        public int trapCount;
        public int enemyCount;
        public bool hasPickup;
        public string note;
        public string paramSummary;
    }

    /// <summary>
    /// Pembangun level streaming: chunk dirakit satu per satu beberapa langkah di depan pemain
    /// sehingga chunk berikutnya selalu memakai parameter terbaru (hasil DDA).
    /// Checkpoint dipasang tiap checkpointInterval chunk, dan dipaksa lebih rapat bila
    /// panjang benang dari checkpoint terakhir tidak cukup untuk mencapai checkpoint berikutnya.
    /// Benang diukur sepanjang jalur, sehingga jarak horizontal dikali GameplayParameters.PathFactor.
    /// </summary>
    public class LevelBuilder : MonoBehaviour
    {
        public int chunksPerLevel = 8;
        public int lookahead = 2;
        public float killHeight = -6f;

        public static LevelBuilder Instance { get; private set; }

        public PlayerMotor player;
        public ThreadController thread;

        public int Seed { get; private set; }
        public int LevelNumber { get; private set; }
        public int PlayerChunkIndex { get; private set; }
        public readonly List<BuiltChunk> Chunks = new List<BuiltChunk>();
        public string Sequence { get; private set; } = "";
        public string LastNote { get; private set; } = "";
        public ChunkAssembler Assembler => _assembler;
        /// <summary>Chunk tutorial ayun (indeks 1) pada level ini.</summary>
        public BuiltChunk TutorialChunk { get; private set; }
        /// <summary>Parent semua objek level hasil PCG ("GeneratedLevel").</summary>
        public Transform Root => _root;

        /// <summary>Diminta saat chunk dibangun; modul AI yang membuat enemy-nya.</summary>
        public event Action<EnemySpawnInfo> EnemySpawnRequested;
        /// <summary>Level baru mulai dibangun (enemy lama perlu dibersihkan).</summary>
        public event Action LevelCleared;

        List<ChunkDefinition> _library;
        ChunkAssembler _assembler;
        readonly ReachabilityValidator _validator = new ReachabilityValidator();
        ReachabilityValidator.Node _lastNode;
        Transform _root;
        float _cursorX;
        float _prevExit;
        ChunkDefinition _prevDef;
        float _distSinceCheckpoint;
        int _chunksSinceCheckpoint;
        float _checkpointThread;
        bool _goalBuilt;
        Checkpoint _firstCheckpoint;
        readonly List<EnemySpawnInfo> _spawnBuffer = new List<EnemySpawnInfo>(8);
        readonly StringBuilder _seq = new StringBuilder(128);

        void Awake()
        {
            Instance = this;
            _library = ChunkLibrary.Load();
            _assembler = new ChunkAssembler(_library);
        }

        public void BuildLevel(int seed, int levelNumber)
        {
            Seed = seed;
            LevelNumber = levelNumber;
            if (_root != null) Destroy(_root.gameObject);
            LevelCleared?.Invoke();
            // Semua objek hasil PCG berada di bawah satu parent agar Hierarchy rapi.
            _root = new GameObject("GeneratedLevel").transform;
            Chunks.Clear();
            _seq.Length = 0;
            _cursorX = 0f;
            _prevExit = 0f;
            _prevDef = null;
            _distSinceCheckpoint = 0f;
            _chunksSinceCheckpoint = 0;
            _checkpointThread = GameplayParameters.Active.threadLength;
            _goalBuilt = false;
            _firstCheckpoint = null;

            Place(ChunkLibrary.CreateStart(), new ChunkAssembler.PickResult { attempts = 1, note = "awal" }, true);
            // Chunk pertama setelah S0 selalu tutorial ayun sederhana (tetap, tidak memakai seed).
            TutorialChunk = Place(ChunkLibrary.CreateTutorial(),
                new ChunkAssembler.PickResult { attempts = 1, note = "tutorial ayun" }, false);
            PlayerChunkIndex = 0;
            EnsureAhead(0);

            if (player != null)
            {
                player.TeleportTo(new Vector3(2.5f, 1.2f, 0f));
                if (thread != null && _firstCheckpoint != null) _firstCheckpoint.Activate(thread, true);
            }
        }

        void Update()
        {
            if (player == null || Chunks.Count == 0) return;
            float x = player.transform.position.x;
            int idx = PlayerChunkIndex;
            while (idx + 1 < Chunks.Count && x >= Chunks[idx + 1].startX) idx++;
            if (idx > PlayerChunkIndex)
            {
                PlayerChunkIndex = idx;
                // Urutan penting: event dulu (tracker -> DDA memperbarui parameter),
                // baru chunk di depan dirakit sehingga memakai parameter terbaru.
                GameEvents.RaiseChunkEntered(idx);
                EnsureAhead(idx);
            }
        }

        void EnsureAhead(int idx)
        {
            while (!_goalBuilt && Chunks.Count <= idx + lookahead) GenerateNext();
        }

        void GenerateNext()
        {
            int index = Chunks.Count;
            if (index > chunksPerLevel)
            {
                var goalChunk = ChunkLibrary.CreateGoal(_prevExit);
                var bc = Place(goalChunk, new ChunkAssembler.PickResult { attempts = 1, note = "goal" }, false);
                Goal.Create(new Vector3(bc.startX + 6f, goalChunk.exitHeight, 0f), _root);
                _goalBuilt = true;
                return;
            }
            var p = GameplayParameters.Active;
            var pick = _assembler.Pick(Seed, index, _prevDef, _prevExit, p, ValidateCandidate);
            // Jalankan ulang untuk chunk terpilih agar info BFS (node, panjang jalur) tercatat.
            if (!pick.fallback && _validator.Validate(_lastNode, pick.def, _cursorX, p, ThreadBudgetFor(pick.def, p)) == null)
                pick.note += " | BFS " + _validator.LastNodeCount + " node, jalur " + _validator.LastPathLength +
                             (_validator.LastSwingEdges > 0 ? ", ayun " + _validator.LastSwingEdges : "");
            Place(pick.def, pick, false);
        }

        /// <summary>Validasi kandidat dengan BFS jangkauan dari platform terakhir chunk sebelumnya.</summary>
        string ValidateCandidate(ChunkDefinition c)
        {
            var p = GameplayParameters.Active;
            return _validator.Validate(_lastNode, c, _cursorX, p, ThreadBudgetFor(c, p));
        }

        /// <summary>Apakah chunk ini akan diberi checkpoint di awalnya (aturan yang sama dengan Place).</summary>
        bool WouldPlaceCheckpoint(ChunkDefinition def, GameplayParameters p)
        {
            float pathEstimate = (_distSinceCheckpoint + def.width + 1.5f) * GameplayParameters.PathFactor;
            return _chunksSinceCheckpoint >= p.checkpointInterval || pathEstimate > _checkpointThread * 0.9f;
        }

        /// <summary>Sisa benang (satuan jalur) di awal chunk: dari checkpoint baru, atau sisa dari checkpoint sebelumnya.</summary>
        float ThreadBudgetFor(ChunkDefinition def, GameplayParameters p)
        {
            const float k = GameplayParameters.PathFactor;
            return WouldPlaceCheckpoint(def, p) ? p.threadLength - k : _checkpointThread - _distSinceCheckpoint * k;
        }

        BuiltChunk Place(ChunkDefinition def, ChunkAssembler.PickResult pick, bool forceCheckpoint)
        {
            var p = GameplayParameters.Active;
            int index = Chunks.Count;
            bool special = def.id == "S0" || def.id == "G0" || def.id == "T0";

            // Checkpoint: wajib bila interval tercapai atau benang tidak cukup sampai akhir chunk ini.
            bool needCheckpoint = forceCheckpoint || WouldPlaceCheckpoint(def, p);
            if (needCheckpoint)
            {
                var cp = Checkpoint.Create(new Vector3(_cursorX + 1f, def.entryHeight, 0f), p.threadLength, _root);
                if (_firstCheckpoint == null) _firstCheckpoint = cp;
                _checkpointThread = p.threadLength;
                _distSinceCheckpoint = def.width - 1f;
                _chunksSinceCheckpoint = 1;
            }
            else
            {
                _distSinceCheckpoint += def.width;
                _chunksSinceCheckpoint++;
            }

            _spawnBuffer.Clear();
            int chunkSeed = ChunkAssembler.DeriveSeed(Seed, index, 999);
            ChunkBuilder.Build(def, _cursorX, _root, special ? 0f : p.trapDensity, special ? 0 : p.enemyCount,
                special ? 0f : p.PickupChance((int)def.tier), chunkSeed, _spawnBuffer, out int traps, out bool pickup);

            var bc = new BuiltChunk
            {
                index = index, def = def, startX = _cursorX, endX = _cursorX + def.width,
                attempts = pick.attempts, fallback = pick.fallback, hasCheckpoint = needCheckpoint,
                trapCount = traps, enemyCount = _spawnBuffer.Count, hasPickup = pickup, note = pick.note,
                paramSummary = "gap<=" + p.maxGapWidth.ToString("0.0") + " trap " + p.trapDensity.ToString("0.00") +
                               " enemy " + p.enemyCount + " aggr " + p.aggression.ToString("0.00")
            };
            Chunks.Add(bc);
            LastNote = "#" + index + " " + def.id + ": " + pick.note;

            if (_seq.Length > 0) _seq.Append(' ');
            _seq.Append(def.id);
            Sequence = _seq.ToString();

            Prims.Label("#" + index + " " + def.id + " " + def.displayName + " [" + def.tier + "]\n" + bc.paramSummary,
                new Vector3(bc.startX + def.width * 0.5f, 8f, 0f), _root, new Color(1f, 1f, 1f, 0.85f), 0.07f);

            for (int i = 0; i < _spawnBuffer.Count; i++) EnemySpawnRequested?.Invoke(_spawnBuffer[i]);

            var last = def.platforms[def.platforms.Count - 1];
            _lastNode = new ReachabilityValidator.Node(_cursorX + last.x, _cursorX + last.Right, last.top);
            _cursorX += def.width;
            _prevExit = def.exitHeight;
            _prevDef = special ? null : def;
            return bc;
        }

        /// <summary>Indeks chunk berikutnya yang belum dibangun (untuk HUD).</summary>
        public int NextChunkIndex => Chunks.Count;
        public float LevelEndX => _cursorX;
    }
}
