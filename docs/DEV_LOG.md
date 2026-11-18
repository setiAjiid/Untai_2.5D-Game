# DEV LOG Untai

Catatan pengembangan prototipe Untai (Project EAS Game Cerdas, kelompok Mi Ayam).
Satu entri per fase: tujuan, file utama, dan keputusan penting.

---

## Fase 0: Persiapan repositori

- **Tujuan:** repositori rapi dan struktur modul jelas sebelum menulis kode.
- **File utama:** `.gitignore`, `.gitattributes`, `docs/STRUCTURE.md`, `README.md`.
- **Keputusan:**
  - Template gitignore dan LFS bawaan Unity dipertahankan, hanya ditambah folder IDE dan format aset lain.
  - Namespace mengikuti `Untai.<Folder>`, kecuali folder `Debug` (`Untai.DebugTools`) dan `Editor`
    (`Untai.EditorTools`) agar tidak bentrok dengan `UnityEngine.Debug` dan `UnityEditor.Editor`.
  - Ditambah folder `Core` (event, input, game loop) dan `Data` (ScriptableObject parameter).

## Fase 1: Gerak dan kamera

- **Tujuan:** pemain bisa bergerak dan melompat di bidang X-Y dengan kamera samping.
- **File utama:** `Player/PlayerMotor.cs`, `CameraRig/SideFollowCamera.cs`, `Core/InputReader.cs`,
  `Core/Prims.cs`, `Core/GameBootstrap.cs`.
- **Keputusan:**
  - Proyek memakai Input System saja (`activeInputHandler: 1`), jadi input dibaca langsung dari
    `Keyboard.current`/`Gamepad.current` tanpa file action tambahan.
  - Rigidbody dengan Z dan rotasi dikunci, gravitasi tambahan, lompat variabel, coyote time,
    jump buffer, dan deteksi tanah SphereCast. Material fisika tanpa gesekan agar tidak menempel di dinding.
  - Kamera ditulis sendiri (SmoothDamp + look-ahead) daripada menambah Cinemachine, karena lebih cepat
    dan cukup untuk demo.
  - Bootstrap `RuntimeInitializeOnLoadMethod` membuat game otomatis di scene apa pun saat Play.

## Fase 2: Benang (versi cepat)

- **Tujuan:** mekanik benang yang terasa sebagai "benang" tanpa menghabiskan waktu menyetel fisika.
- **File utama:** `Thread/ThreadController.cs`, `Thread/ThreadRenderer.cs`, `Thread/ThreadSwing.cs`,
  `Thread/Anchor.cs`, `Level/Checkpoint.cs`, `Level/KillZone.cs`, `Level/Goal.cs`, `Player/PlayerHealth.cs`.
- **Keputusan:**
  - Sisa benang = panjang maksimum - jarak horizontal dari checkpoint aktif. Saat habis, pemain ditahan
    di batas (benang tegang, garis memerah). Checkpoint memindahkan titik tambat sehingga benang pulih.
  - Ayunan dibuat sebagai pendulum sederhana: batasan jarak ke jangkar + buang kecepatan radial yang
    menjauh; gravitasi yang mengayunkan. Tidak memakai Joint agar perilaku mudah dijelaskan dan stabil.
    Panjang tali dibatasi jangkauan jangkar dan sisa benang.
  - Kill zone memakai cek ketinggian (bukan collider raksasa) supaya berlaku untuk level streaming.

## Fase 3: Parameter, pencatat performa, HUD

- **Tujuan:** satu sumber parameter yang dibaca semua sistem, pencatat performa, dan HUD debug.
- **File utama:** `Data/GameplayParameters.cs`, `Assets/Data/Resources/Untai/GameplayParameters.asset`,
  `DDA/PerformanceTracker.cs`, `Debug/DebugHud.cs`, `Core/GameEvents.cs`.
- **Keputusan:**
  - Semua skrip membaca `GameplayParameters.Active`, yaitu salinan runtime dari aset. DDA mengubah
    salinan ini sehingga aset asli tidak berubah saat Play. Event `Changed` memberi tahu pendengar.
  - Event global (`GameEvents`) memisahkan sumber kejadian (Player, Level) dari pendengar (DDA, HUD).
  - HUD OnGUI dibangun ulang tiap 0,2 detik memakai StringBuilder dan GUIContent yang di-cache.

## Fase 4: PCG chunk berbasis seed

- **Tujuan:** level dirakit otomatis dari chunk; seed dan parameter yang sama menghasilkan level sama.
- **File utama:** `PCG/ChunkDefinition.cs`, `PCG/ChunkLibrary.cs`, `PCG/ChunkAssembler.cs`,
  `PCG/ReachabilityValidator.cs`, `Level/ChunkBuilder.cs`, `Level/LevelBuilder.cs`, `Level/Trap.cs`,
  `Editor/ChunkAssetGenerator.cs`.
- **Keputusan:**
  - 12 chunk contoh (4 mudah, 4 sedang, 4 sulit) ditulis di kode, lalu diekspor menjadi aset
    ScriptableObject oleh menu **Untai > Generate Sample Chunks** (juga otomatis saat Editor dimuat).
    Runtime memakai aset bila ada, dan definisi kode bila tidak, jadi tidak ada langkah manual.
  - Pemilihan chunk: `Random.InitState(seed turunan dari seed level, indeks chunk, nomor percobaan)` lalu
    weighted random dengan bobot tingkat dari parameter; chunk dengan celah melebihi `maxGapWidth` berbobot 0.
    Kandidat ditolak bila sambungan tinggi tidak cocok atau validasi gagal; maksimal `maxAttempts`,
    lalu fallback ke chunk paling aman.
  - Validasi: graf platform dengan sisi lompat (jarak dan tinggi lompat dari fisika) dan sisi ayun
    (jangkauan jangkar dan **sisa benang nyata** di tepi platform), BFS dari platform masuk ke platform akhir.
  - Level **streaming**: chunk dirakit 2 langkah di depan pemain. Ini disengaja agar chunk berikutnya
    selalu memakai parameter DDA terbaru. Konsekuensinya, seed sama menghasilkan level sama hanya bila
    parameternya juga sama (lihat skenario demo).
  - Checkpoint dipasang tiap `checkpointInterval` chunk dan dipaksa lebih rapat bila benang dari
    checkpoint terakhir tidak cukup sampai checkpoint berikutnya.

## Fase 5: DDA

- **Tujuan:** kesulitan menyesuaikan performa pemain dan terlihat di layar.
- **File utama:** `DDA/DDAManager.cs` (DifficultyScorer + DDAManager), `DDA/DifficultyMapper.cs`,
  `DDA/DDAController.cs`.
- **Keputusan:**
  - `DDAManager` dan `DifficultyScorer` adalah kelas murni yang hanya menerima `PerformanceSample`
    generik (gagal, damage, waktu, waktu wajar, rasio sumber daya), sehingga bisa dipakai game lain.
    `DDAController` adalah adapter khusus Untai.
  - Rolling window 4 segmen terakhir dengan bobot recency 0,7. Segmen ditutup oleh kematian (segmen gagal)
    atau selesainya chunk. Skor < 0,45 = kesulitan (turunkan), > 0,72 = terlalu mudah (naikkan),
    di antaranya seimbang. Perubahan skor per evaluasi dibatasi 0,04 sampai 0,12.
  - `DifficultyMapper` memetakan skor ke celah, jebakan, jumlah enemy, agresivitas, interval checkpoint,
    panjang benang, jangkauan jangkar, dan bobot chunk; tiap parameter punya clamp dan step limit.
  - Perbaikan setelah uji hitung: awalnya kematian dinilai bersama chunk berjalan sehingga teredam chunk
    sempurna sebelumnya; diganti menjadi satu segmen gagal per kematian.

## Fase 6: Enemy AI

- **Tujuan:** enemy dengan FSM yang terlihat dan agresivitas mengikuti DDA.
- **File utama:** `AI/EnemyBrain.cs`, `AI/EnemySpawner.cs`.
- **Keputusan:**
  - FSM Patrol, Chase, Attack (ancang-ancang bergetar lalu menerjang), Return; semua gerak di-clamp ke
    platform asal. Warna tubuh dan label di atas kepala menunjukkan state, juga Gizmos dan HUD.
  - Agresivitas memengaruhi jarak deteksi, kecepatan kejar, lama ancang-ancang, jeda serangan,
    kecepatan patroli, dan lama mengingat pemain. Enemy berlangganan `GameplayParameters.Changed`
    sehingga enemy yang sudah ada langsung berubah setelah keputusan DDA.

## Fase 7: Integrasi dan siap demo

- **Tujuan:** alur ujung ke ujung, game loop, dan tombol demo.
- **File utama:** `Core/GameDirector.cs`, `Debug/DemoHotkeys.cs`, `Assets/Scenes/Untai_Main.unity`,
  `Editor/BuildSettingsSetup.cs`.
- **Keputusan:**
  - `GameDirector` merakit semua sistem dan mendokumentasikan alur. Event masuk chunk diproses sebelum
    chunk di depan dirakit, sehingga keputusan DDA langsung dipakai chunk berikutnya.
  - Goal memuat level berikutnya dengan seed turunan; riwayat DDA berlanjut antar level.
  - Simulasi performa (F3/F4) dimasukkan lewat `PerformanceTracker`, melewati jalur yang sama dengan
    permainan asli.

## Fase 8: Prefab dan scene yang bisa diedit

- **Tujuan:** objek game menjadi prefab yang bisa diedit di Editor, tanpa mengubah logika PCG, DDA, AI,
  maupun benang; Hierarchy scene utama tidak lagi kosong.
- **File utama:** `Data/PrefabLibrary.cs`, `Editor/PrefabBuilder.cs`, `Level/PlatformVisual.cs`,
  `Assets/Prefabs/**`, `Assets/Materials/**`, `Assets/Data/Resources/Untai/PrefabLibrary.asset`,
  `Assets/Scenes/Untai_Main.unity`.
- **Keputusan:**
  - Setiap pembuat objek (`Anchor.Create`, `Checkpoint.Create`, `Goal.Create`, `Trap.Create`,
    `EnemyBrain.Create`, `PlayerMotor.Spawn`, platform di `ChunkBuilder`) mencoba prefab dari `PrefabLibrary`
    lebih dulu, lalu fallback ke versi primitif lama (`CreatePrimitive`). Tanda tangan fungsi tidak berubah,
    jadi LevelBuilder, PCG, dan DDA tidak tersentuh.
  - Prefab dibangun oleh skrip Editor dari fungsi `CreatePrimitive` yang sama dengan fallback, sehingga
    prefab dan fallback identik. Builder idempoten: aset yang sudah ada tidak ditimpa.
  - Warna yang dulu tertanam di kode (state enemy, tingkat platform, jangkar, checkpoint) menjadi field
    `Material` di Inspector; nilai lama tetap dipakai sebagai warna fallback. Ukuran dasar platform
    (`platformDepth`, `pillarBottom`) dipindah ke `PrefabLibrary`.
  - Prefab, material, dan scene dibuat dengan menjalankan builder lewat Unity batchmode pada salinan proyek,
    lalu file hasilnya (beserta `.meta`) di-commit, sehingga tidak ada langkah manual.
  - Platform memakai satu prefab kubus 1x1x1 yang diskalakan data chunk; warnanya dipilih `PlatformVisual`
    berdasarkan tingkat chunk.
  - Objek level hasil PCG dikumpulkan di bawah parent `GeneratedLevel` (enemy di `GeneratedLevel/Enemies`).
  - `ThreadPickup` tidak dibuat karena objek tersebut belum ada di game.

### Struktur prefab

| Prefab | Isi | Komponen penting |
|---|---|---|
| `Characters/Player` | kapsul + child `Eye` | Rigidbody, CapsuleCollider (PM_PlayerNoFriction), PlayerMotor, ThreadController, ThreadRenderer, PlayerHealth, ThreadSwing |
| `Characters/Enemy` | kapsul (trigger) + `Visor` + `Label` | EnemyBrain (material per state, bodyRenderer, stateLabel) |
| `Level/Platform` | kubus 1x1x1 | BoxCollider, PlatformVisual (material mudah/sedang/sulit) |
| `Level/Checkpoint` | `Pole` + `Spool` | BoxCollider trigger, Checkpoint (material idle/aktif) |
| `Level/Goal` | dua tiang + palang + label | BoxCollider trigger, Goal |
| `Level/Trap` | tiga duri | BoxCollider trigger, Trap |
| `Level/KillZone` | kosong | KillZone (cek ketinggian) |
| `Thread/Anchor` | bola | Anchor (material idle/sorot) |

### Mengganti model dummy dengan aset asli

1. Buka prefab (klik dua kali di Project), tambahkan model sebagai child.
2. Matikan `MeshRenderer` primitif lama (atau hapus `MeshFilter` + `MeshRenderer`-nya), tetapi biarkan
   collider, Rigidbody, dan komponen skrip.
3. Arahkan field renderer ke model baru: `EnemyBrain.bodyRenderer`, `Anchor.targetRenderer`,
   `Checkpoint.spool`, `PlatformVisual.targetRenderer`.
4. Simpan prefab; perubahan langsung terpakai saat Play karena builder runtime meng-Instantiate prefab ini.

---

## Alur Sistem

```
 [Pemain bermain]
       |  mati / jatuh / damage / waktu per chunk / sisa benang di checkpoint
       v
 PerformanceTracker  --(segmen: kematian atau chunk selesai)-->  DDAController
                                                                     |
                                     DifficultyScorer (rolling window) -> skor performa 0..1
                                     DDAManager -> kondisi + skor DDA 0..1 (step terbatas)
                                     DifficultyMapper -> GameplayParameters.Active (clamp + step)
                                                                     |
                     +-----------------------------------------------+------------------+
                     v                                                                  v
          LevelBuilder / ChunkAssembler (PCG)                                 EnemyBrain (Game AI)
          chunk berikutnya: bobot tingkat, batas celah,                      agresivitas -> deteksi,
          jebakan, jumlah enemy, checkpoint, benang                          kejar, ancang-ancang, jeda
                     |                                                                  |
                     +-------------------> Perubahan gameplay <-------------------------+
```

HUD menampilkan alur ini sebagai panel bernomor [1] Performance, [2] DDA, [3] Parameter, [4] Gameplay
(PCG), [5] Game AI, ditambah banner di kanan atas yang menyala berurutan setiap DDA mengambil keputusan.

## Tombol

| Tombol | Fungsi |
|---|---|
| A/D atau panah | Gerak |
| Space | Lompat (tahan untuk lompat lebih tinggi); saat mengayun: lepas dengan dorongan |
| Tahan Shift atau J | Kaitkan benang ke jangkar terdekat (bola biru menyala kuning bila terjangkau) |
| W/S | Gulung/ulur tali saat mengayun |
| R | Respawn di checkpoint terakhir |
| F1 | Tampilkan/sembunyikan HUD |
| F2 | Simulasi pemain mati |
| F3 | Simulasi performa bagus (chunk sempurna) |
| F4 | Simulasi performa buruk |
| F5 | Seed acak baru |
| F6 | Ulang level dengan seed yang sama |
| F7 | Level berikutnya |
| F8 | Reset DDA ke skor awal |
| F9 | Lompat ke chunk berikutnya |

## Skenario Demo yang Disarankan (sekitar 5 menit)

1. **Pembuka.** Tekan Play di scene `Untai_Main`. Tunjukkan HUD: seed 12345, panel [1] sampai [5],
   dan banner alur di kanan atas. Jelaskan label di atas tiap chunk (id, tingkat, parameter saat dirakit).
2. **Seed sama = level sama.** Catat baris "urutan" di panel [4]. Tekan **F8** (reset DDA) lalu **F6**
   (ulang seed). Urutan chunk identik. Tekan **F9** beberapa kali untuk memperlihatkan chunk lanjutan,
   ulangi F8 + F6 + F9 dengan jumlah yang sama, urutan tetap identik. Tekan **F5** untuk seed baru:
   urutan berbeda.
3. **Kesulitan turun.** Mati dengan sengaja (lompat ke jurang) atau tekan **F2** 3 kali. Di panel [2]
   kondisi menjadi KESULITAN, skor DDA turun, log memperlihatkan parameter yang berubah (celah, jebakan,
   enemy, agresivitas turun; benang dan jangkauan jangkar naik). Banner alur menyala berurutan.
   Tekan **F9**: chunk baru memakai parameter baru (label chunk, lebih banyak chunk "E", jebakan lebih sedikit).
4. **Kesulitan naik.** Main lancar beberapa chunk atau tekan **F3** 3 sampai 4 kali. Kondisi menjadi
   TERLALU MUDAH, skor naik, bobot chunk sulit (H) meningkat, celah maksimum melebar, enemy per chunk bertambah.
5. **Enemy makin agresif.** Dekati enemy: perhatikan label/warna state (hijau Patrol, oranye Chase,
   merah Attack, biru Return) dan panel [5]. Setelah F3 beberapa kali, jarak deteksi dan kecepatan kejar
   di panel [5] naik dan jeda serangan memendek; enemy yang sama langsung bereaksi lebih cepat.
6. **Penutup.** Capai goal: level berikutnya dimuat dengan seed turunan, DDA tetap berlanjut.

## Penyederhanaan yang Disengaja

- Ayunan adalah pendulum batasan jarak sederhana, bukan simulasi tali.
- Visual seluruhnya primitif; belum ada aset, animasi, audio, atau efek.
- Validasi BFS berjalan per chunk (dari platform terakhir chunk sebelumnya), menjamin keterhubungan
  seluruh level secara induktif karena level dirakit streaming.
- Companion dengan Utility AI belum dibuat (fitur opsional).
