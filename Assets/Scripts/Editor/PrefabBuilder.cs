using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Untai.AI;
using Untai.CameraRig;
using Untai.Core;
using Untai.Data;
using Untai.DDA;
using Untai.DebugTools;
using Untai.Level;
using Untai.Player;
using Untai.Thread;
using Object = UnityEngine.Object;

namespace Untai.EditorTools
{
    /// <summary>
    /// Menu Untai > Build Prefabs and Scene.
    /// 1. Membuat material (satu per warna) di Assets/Materials.
    /// 2. Membuat prefab di Assets/Prefabs/{Characters,Level,Thread} dari versi primitif yang sama
    ///    dengan fallback runtime, lalu memasang material aset.
    /// 3. Membuat/melengkapi PrefabLibrary di Assets/Data/Resources/Untai.
    /// 4. Mengisi Hierarchy scene Untai_Main (Player, Main Camera, GameDirector, KillZone, Light).
    /// Idempoten: material/prefab yang sudah ada TIDAK ditimpa (aman untuk prefab yang sudah diedit),
    /// slot PrefabLibrary yang sudah terisi tidak diubah, dan objek scene yang sudah ada dipakai ulang.
    /// </summary>
    public static class PrefabBuilder
    {
        const string MaterialDir = "Assets/Materials";
        const string PrefabDir = "Assets/Prefabs";
        const string CharacterDir = PrefabDir + "/Characters";
        const string LevelDir = PrefabDir + "/Level";
        const string ThreadDir = PrefabDir + "/Thread";
        const string LibraryPath = "Assets/Data/Resources/Untai/PrefabLibrary.asset";
        const string ParametersPath = "Assets/Data/Resources/Untai/GameplayParameters.asset";
        const string ScenePath = "Assets/Scenes/Untai_Main.unity";

        [MenuItem("Untai/Build Prefabs and Scene")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var lib = BuildAssets();
            BuildScene(lib);
            Debug.Log("[Untai] Prefab, material, PrefabLibrary, dan scene Untai_Main siap.");
        }

        [MenuItem("Untai/Build Prefabs Only")]
        public static void BuildPrefabsOnly() => BuildAssets();

        /// <summary>Entry point untuk -executeMethod (batchmode).</summary>
        public static void BuildAllBatch()
        {
            var lib = BuildAssets();
            BuildScene(lib);
            Validate(lib);
        }

        // ------------------------------------------------------------------ aset

        public static PrefabLibrary BuildAssets()
        {
            EnsureFolder(MaterialDir);
            EnsureFolder(CharacterDir);
            EnsureFolder(LevelDir);
            EnsureFolder(ThreadDir);

            // Material: satu per warna yang dipakai sekarang.
            var mPlayer = Mat("M_Player", PlayerMotor.DefaultColor);
            var mPlayerB = Mat("M_PlayerB", PlayerMotor.DefaultColorB);
            var mWhite = Mat("M_White", Color.white);
            var mBlack = Mat("M_Black", Color.black);
            var mPatrol = Mat("M_EnemyPatrol", EnemyBrain.StateColor(EnemyState.Patrol));
            var mChase = Mat("M_EnemyChase", EnemyBrain.StateColor(EnemyState.Chase));
            var mAttack = Mat("M_EnemyAttack", EnemyBrain.StateColor(EnemyState.Attack));
            var mReturn = Mat("M_EnemyReturn", EnemyBrain.StateColor(EnemyState.Return));
            var mEasy = Mat("M_PlatformEasy", PlatformVisual.DefaultTierColors[0]);
            var mMedium = Mat("M_PlatformMedium", PlatformVisual.DefaultTierColors[1]);
            var mHard = Mat("M_PlatformHard", PlatformVisual.DefaultTierColors[2]);
            var mAnchorIdle = Mat("M_AnchorIdle", Anchor.DefaultIdleColor);
            var mAnchorHi = Mat("M_AnchorHighlight", Anchor.DefaultHighlightColor);
            var mCpIdle = Mat("M_CheckpointIdle", Checkpoint.DefaultIdleColor);
            var mCpActive = Mat("M_CheckpointActive", Checkpoint.DefaultActiveColor);
            var mPole = Mat("M_CheckpointPole", Checkpoint.PoleColor);
            var mGoal = Mat("M_Goal", Goal.DefaultColor);
            var mTrap = Mat("M_Trap", Trap.DefaultColor);
            var mThread = Mat("M_Thread", Color.white, "Sprites/Default");
            var noFriction = PhysicsMat("PM_PlayerNoFriction");

            // Prefab karakter.
            // Dua karakter Tandem (beda warna); model dummy kapsul bisa diganti per prefab.
            var characterA = MakePrefab(CharacterDir + "/CharacterA.prefab", () => BuildCharacter("CharacterA", mPlayer, mWhite, noFriction));
            var characterB = MakePrefab(CharacterDir + "/CharacterB.prefab", () => BuildCharacter("CharacterB", mPlayerB, mWhite, noFriction));

            var enemy = MakePrefab(CharacterDir + "/Enemy.prefab", () =>
            {
                var e = EnemyBrain.CreatePrimitive(Vector3.zero, null);
                e.patrolMaterial = mPatrol;
                e.chaseMaterial = mChase;
                e.attackMaterial = mAttack;
                e.returnMaterial = mReturn;
                SetMat(e.gameObject, mPatrol);
                SetMat(e.transform.Find("Visor"), mBlack);
                return e.gameObject;
            });

            // Prefab level.
            var platform = MakePrefab(LevelDir + "/Platform.prefab", () =>
            {
                var go = Prims.Box("Platform", Vector3.zero, Vector3.one, PlatformVisual.DefaultTierColors[0], null);
                var v = go.AddComponent<PlatformVisual>();
                v.targetRenderer = go.GetComponent<Renderer>();
                v.easyMaterial = mEasy;
                v.mediumMaterial = mMedium;
                v.hardMaterial = mHard;
                SetMat(go, mEasy);
                return go;
            });

            var checkpoint = MakePrefab(LevelDir + "/Checkpoint.prefab", () =>
            {
                var cp = Checkpoint.CreatePrimitive(Vector3.zero, null);
                cp.idleMaterial = mCpIdle;
                cp.activeMaterial = mCpActive;
                SetMat(cp.transform.Find("Pole"), mPole);
                SetMat(cp.transform.Find("Spool"), mCpIdle);
                return cp.gameObject;
            });

            var goal = MakePrefab(LevelDir + "/Goal.prefab", () =>
            {
                var g = Goal.CreatePrimitive(Vector3.zero, null);
                PaintAllExceptText(g.gameObject, mGoal);
                return g.gameObject;
            });

            var trap = MakePrefab(LevelDir + "/Trap.prefab", () =>
            {
                var t = Trap.CreatePrimitive(Vector3.zero, null);
                PaintAllExceptText(t.gameObject, mTrap);
                return t.gameObject;
            });

            var killZone = MakePrefab(LevelDir + "/KillZone.prefab", () =>
            {
                var k = new GameObject("KillZone").AddComponent<KillZone>();
                k.killHeight = -6f;
                return k.gameObject;
            });

            // Prefab benang.
            var anchor = MakePrefab(ThreadDir + "/Anchor.prefab", () =>
            {
                var a = Anchor.CreatePrimitive(Vector3.zero, null);
                a.idleMaterial = mAnchorIdle;
                a.highlightMaterial = mAnchorHi;
                SetMat(a.gameObject, mAnchorIdle);
                return a.gameObject;
            });

            // Untai pengikat: Tandem + TandemRope + LineRenderer.
            var tandem = MakePrefab(ThreadDir + "/Tandem.prefab", () =>
            {
                var go = new GameObject("Tandem");
                go.AddComponent<Tandem>();
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = mThread;
                lr.widthMultiplier = 0.08f;
                var rope = go.AddComponent<TandemRope>();
                rope.line = lr;
                rope.lineMaterial = mThread;
                return go;
            });

            // PrefabLibrary: buat bila belum ada, isi hanya slot yang kosong.
            var lib = AssetDatabase.LoadAssetAtPath<PrefabLibrary>(LibraryPath);
            if (lib == null)
            {
                EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
                lib = ScriptableObject.CreateInstance<PrefabLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            if (lib.characterA == null) lib.characterA = characterA;
            if (lib.characterB == null) lib.characterB = characterB;
            if (lib.tandem == null) lib.tandem = tandem;
            if (lib.enemy == null) lib.enemy = enemy;
            if (lib.platform == null) lib.platform = platform;
            if (lib.checkpoint == null) lib.checkpoint = checkpoint;
            if (lib.goal == null) lib.goal = goal;
            if (lib.killZone == null) lib.killZone = killZone;
            if (lib.trap == null) lib.trap = trap;
            if (lib.anchor == null) lib.anchor = anchor;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        // ------------------------------------------------------------------ scene

        public static void BuildScene(PrefabLibrary lib)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // GameDirector: objek terpisah dengan semua komponen sistem.
            var director = Object.FindAnyObjectByType<GameDirector>();
            var dgo = director != null ? director.gameObject : new GameObject("GameDirector");
            dgo.name = "GameDirector";
            director = GetOrAdd<GameDirector>(dgo);
            GetOrAdd<PerformanceTracker>(dgo);
            GetOrAdd<DDAController>(dgo);
            var level = GetOrAdd<LevelBuilder>(dgo);
            GetOrAdd<EnemySpawner>(dgo);
            var hud = GetOrAdd<DebugHud>(dgo);
            GetOrAdd<DemoHotkeys>(dgo);
            director.parameters = AssetDatabase.LoadAssetAtPath<GameplayParameters>(ParametersPath);
            director.prefabs = lib;

            // Hapus objek pemain lama (versi satu karakter) dan instance prefab yang asetnya sudah hilang.
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Player" || PrefabUtility.IsPrefabAssetMissing(root)) Object.DestroyImmediate(root);
            foreach (var old in Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
                if (old.name != "CharacterA" && old.name != "CharacterB") Object.DestroyImmediate(old.gameObject);

            // Dua karakter Tandem dan objek untai: instance prefab.
            var a = FindOrInstantiate<PlayerMotor>("CharacterA", lib.characterA, scene, new Vector3(2.5f, 1.2f, 0f));
            var b = FindOrInstantiate<PlayerMotor>("CharacterB", lib.characterB, scene, new Vector3(1.3f, 1.2f, 0f));
            var rope = FindOrInstantiate<TandemRope>("Tandem", lib.tandem, scene, Vector3.zero);
            director.characterA = a;
            director.characterB = b;
            director.rope = rope;
            if (rope != null)
            {
                var t = rope.GetComponent<Tandem>();
                if (t != null) { t.characterA = a; t.characterB = b; }
                level.tandem = t;
            }
            var player = a;

            // KillZone: instance prefab.
            var kill = Object.FindAnyObjectByType<KillZone>();
            if (kill == null && lib.killZone != null)
                kill = ((GameObject)PrefabUtility.InstantiatePrefab(lib.killZone, scene)).GetComponent<KillZone>();
            if (kill != null)
            {
                director.killZone = kill;
                kill.killHeight = level.killHeight;
                if (player != null) kill.player = player.GetComponent<PlayerHealth>();
            }

            // Main Camera dengan SideFollowCamera.
            var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            cam.gameObject.name = "Main Camera";
            cam.fieldOfView = 45f;
            var follow = GetOrAdd<SideFollowCamera>(cam.gameObject);
            if (player != null)
            {
                follow.target = player.transform;
                cam.transform.position = player.transform.position + follow.offset;
                cam.transform.rotation = Quaternion.Euler(follow.pitch, 0f, 0f);
            }

            // Directional Light.
            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var lgo = new GameObject("Directional Light");
                lgo.AddComponent<Light>().type = LightType.Directional;
                lgo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------ validasi

        static void Validate(PrefabLibrary lib)
        {
            int missing = 0;
            void Check(string name, Object o)
            {
                if (o == null) { missing++; Debug.LogError("[Untai] Kosong: " + name); }
            }
            Check("characterA", lib.characterA); Check("characterB", lib.characterB); Check("tandem", lib.tandem);
            Check("enemy", lib.enemy); Check("platform", lib.platform);
            Check("checkpoint", lib.checkpoint); Check("goal", lib.goal); Check("killZone", lib.killZone);
            Check("trap", lib.trap); Check("anchor", lib.anchor);
            Check("CharacterA.PlayerMotor", lib.characterA != null ? lib.characterA.GetComponent<PlayerMotor>() : null);
            Check("CharacterB.PlayerHealth", lib.characterB != null ? lib.characterB.GetComponent<PlayerHealth>() : null);
            Check("Tandem.TandemRope", lib.tandem != null ? lib.tandem.GetComponent<TandemRope>() : null);
            Check("Enemy.EnemyBrain", lib.enemy != null ? lib.enemy.GetComponent<EnemyBrain>() : null);
            Check("Platform.PlatformVisual", lib.platform != null ? lib.platform.GetComponent<PlatformVisual>() : null);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.sharedMaterial == null) { missing++; Debug.LogError("[Untai] Material kosong di scene: " + r.name); }
            Debug.Log(missing == 0 ? "[Untai] VALIDASI OK" : "[Untai] VALIDASI GAGAL: " + missing);
        }

        // ------------------------------------------------------------------ helper

        static GameObject BuildCharacter(string name, Material body, Material eye, PhysicsMaterial noFriction)
        {
            var m = PlayerMotor.CreatePlayer(Vector3.zero);
            m.name = name;
            PlayerMotor.EnsureComponents(m.gameObject);
            m.GetComponent<CapsuleCollider>().sharedMaterial = noFriction;
            SetMat(m.gameObject, body);
            SetMat(m.transform.Find("Eye"), eye);
            return m.gameObject;
        }

        /// <summary>Pakai objek scene bernama "name" bila ada, jika tidak instansiasi prefab.</summary>
        static T FindOrInstantiate<T>(string name, GameObject prefab, Scene scene, Vector3 position) where T : Component
        {
            var existing = GameObject.Find(name);
            if (existing != null && existing.GetComponent<T>() != null) return existing.GetComponent<T>();
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = name;
            go.transform.position = position;
            return go.GetComponent<T>();
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Material Mat(string name, Color color, string shaderName = "Universal Render Pipeline/Lit")
        {
            string path = MaterialDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static PhysicsMaterial PhysicsMat(string name)
        {
            string path = MaterialDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null) return existing;
            var pm = PlayerMotor.CreateNoFrictionMaterial();
            pm.name = name;
            AssetDatabase.CreateAsset(pm, path);
            return pm;
        }

        /// <summary>Simpan hasil build sebagai prefab; prefab yang sudah ada dibiarkan (tidak ditimpa).</summary>
        static GameObject MakePrefab(string path, Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var go = build();
            go.transform.position = Vector3.zero;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void SetMat(Component c, Material m)
        {
            if (c != null) SetMat(c.gameObject, m);
        }

        static void SetMat(GameObject go, Material m)
        {
            if (go == null) return;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = m;
        }

        static void PaintAllExceptText(GameObject root, Material m)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.GetComponent<TextMesh>() == null) r.sharedMaterial = m;
        }
    }
}
