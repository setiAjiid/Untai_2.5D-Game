# Untai (nama sementara)

Prototipe platformer 2.5D untuk **Project EAS mata kuliah Game Cerdas**, kelompok **Mi Ayam**.

- Engine: **Unity 6000.3.23f1** (URP, 3D)
- Dosen: Dr. Eng. Darlis Herumurti, S.Kom., M.Kom.

## Anggota

| Nama | NRP |
|---|---|
| Dimas Setiaji | 5025241056 |
| Indra Wahyu Tirtayasa | 5025241108 |
| Brave Juliada | 5025241140 |

## Konsep

Platformer 2.5D: model dan kamera 3D, gerak hanya di bidang X dan Y (sumbu Z dikunci).
Pemain membawa **benang** berpanjang terbatas yang terikat pada checkpoint terakhir dan dapat
dipakai untuk **mengayun dari jangkar** melewati celah. Level dirakit otomatis dari **chunk**
berdasarkan **seed** (PCG), tingkat kesulitan disesuaikan oleh **DDA** dari performa pemain,
dan **enemy** memiliki AI (FSM) yang agresivitasnya dipengaruhi DDA.

Alur integrasi yang ditampilkan di layar:

```
Player Performance -> DDA -> parameter PCG & Game AI -> Perubahan Gameplay
```

Inspirasi mekanik benang: *Unravel Two* (Coldwood Interactive). Untai **bukan tiruan**:
tidak memakai nama, karakter, level, maupun aset dari game tersebut. Seluruh visual saat ini
berupa primitif (kapsul dan kubus) sebagai placeholder.

## Peta Modul

| Modul | Folder | Peran |
|---|---|---|
| Player | `Assets/Scripts/Player` | Gerak samping, lompat variabel, nyawa, respawn |
| Thread | `Assets/Scripts/Thread` | Sumber daya benang, render benang, ayunan jangkar |
| Level | `Assets/Scripts/Level` | Checkpoint, kill zone, goal, jebakan, `LevelBuilder`/`ChunkBuilder` (instansiasi prefab per chunk) |
| PCG | `Assets/Scripts/PCG` | Data chunk, perakit chunk berbobot berbasis seed, validasi jangkauan (BFS) |
| DDA | `Assets/Scripts/DDA` | Pencatat performa, skor kesulitan rolling window, pemetaan ke parameter |
| AI | `Assets/Scripts/AI` | FSM enemy (Patrol, Chase, Attack, Return) |
| Data | `Assets/Scripts/Data` | `GameplayParameters` (sumber tunggal parameter) dan `PrefabLibrary` (daftar prefab) |
| Debug | `Assets/Scripts/Debug` | HUD debug dan tombol demo |
| CameraRig | `Assets/Scripts/CameraRig` | Kamera follow samping |
| Core | `Assets/Scripts/Core` | `GameDirector` (perakit sistem dan game loop), event global, input |
| Editor | `Assets/Scripts/Editor` | Menu Untai: generator chunk, prefab, material, dan isi scene |
| Prefab | `Assets/Prefabs/{Characters,Level,Thread}` | Player, Enemy / Platform, Checkpoint, Goal, Trap, KillZone / Anchor |
| Material | `Assets/Materials` | Satu material per warna (pemain, state enemy, tingkat platform, dll.) |

Detail struktur folder: [docs/STRUCTURE.md](docs/STRUCTURE.md).

## Status

Prototipe siap demo (Project EAS). Yang sudah berjalan:

- [x] Gerak samping 2.5D, lompat variabel, coyote time, kamera follow
- [x] Benang terikat checkpoint (sumber daya), render benang, ayunan jangkar sederhana
- [x] Checkpoint, kill zone, jebakan, goal, respawn
- [x] **PCG**: 12 chunk (mudah/sedang/sulit), perakit weighted ber-seed, tolak sambungan, validasi BFS
- [x] **DDA**: rolling window performa, tiga kondisi, pemetaan parameter dengan clamp dan step limit
- [x] **Game AI**: FSM enemy (Patrol, Chase, Attack, Return), agresivitas dari DDA
- [x] HUD debug (alur Performance -> DDA -> Parameter -> Gameplay), tombol demo, game loop antar level
- [ ] Companion Utility AI, aset visual, audio, efek (belum, opsional)

## Cara Menjalankan

1. Buka proyek dengan **Unity 6000.3.23f1**.
2. Buka scene `Assets/Scenes/Untai_Main.unity` lalu tekan **Play**.
   Hierarchy scene berisi `GameDirector`, `Player` (instance prefab), `Main Camera` (dengan
   `SideFollowCamera`), `KillZone`, `Directional Light`, dan `Global Volume`. Objek level hasil PCG
   dibuat saat Play di bawah `GeneratedLevel`.
3. Scene lain (misal `SampleScene`) juga bisa: bootstrap otomatis membuat game saat Play.
4. Prefab, material, `PrefabLibrary`, dan isi scene sudah ada di repo. Bila ada yang terhapus,
   jalankan menu **Untai > Build Prefabs and Scene** (aman diulang: prefab yang sudah diedit tidak ditimpa).
   Aset chunk dibuat lewat **Untai > Generate Sample Chunks** (juga otomatis saat Editor dimuat).

## Langkah Manual

Sebagian besar pengaturan sudah otomatis. Yang masih perlu dilakukan sendiri:

**Wajib (sekali, setelah clone/pull)**

1. Buka proyek di **Unity 6000.3.23f1** dan tunggu impor selesai. Jika Editor sudah terbuka saat pull,
   klik jendela Unity agar file baru ter-import dan skrip ter-compile.
2. Buka `Assets/Scenes/Untai_Main.unity`. Unity membuka scene terakhir yang dipakai (sering
   `SampleScene`), jadi scene utama harus dibuka manual sekali. Scene lain tetap bisa dimainkan,
   tetapi Hierarchy-nya tidak berisi objek Untai.
3. Tekan **Play** dan pastikan HUD muncul (seed, panel [1] sampai [5]). Klik jendela Game agar tombol
   keyboard (A/D, Space, F1 sampai F9) terbaca.

**Opsional / bila diperlukan**

- **Aset chunk:** Editor membuat `Assets/Data/Resources/Untai/Chunks/*.asset` otomatis saat proyek dimuat.
  File ini tidak wajib (game memakai definisi chunk dari kode bila tidak ada), tetapi boleh di-commit:
  `git add Assets/Data/Resources/Untai/Chunks`.
- **Prefab atau material terhapus:** jalankan **Untai > Build Prefabs and Scene**. Hanya aset yang hilang
  yang dibuat ulang; prefab yang sudah diedit dan slot PrefabLibrary yang sudah terisi tidak ditimpa.
- **Objek tampil sebagai primitif polos, bukan prefab:** pilih `GameDirector` di Hierarchy dan pastikan field
  `Prefabs` berisi `PrefabLibrary` dan `Parameters` berisi `GameplayParameters` (keduanya di
  `Assets/Data/Resources/Untai`). Jika kosong, game memakai fallback primitif.
- **Build Settings:** `Untai_Main` otomatis dipasang sebagai scene pertama. Jika belum, jalankan
  **Untai > Add Main Scene To Build Settings**.
- **Input:** proyek memakai Input System saja (`Project Settings > Player > Active Input Handling` =
  Input System Package). Jangan diganti ke Input Manager lama, karena input dibaca lewat Input System.
- **Ruang disk:** Unity butuh ruang kosong untuk cache dan impor paket. Bila muncul error `ENOSPC` atau
  `no space left on device`, kosongkan drive tempat proyek dan drive C: (cache Unity).
- **Mengganti model dummy:** lihat bagian [Prefab dan PrefabLibrary](#prefab-dan-prefablibrary) di bawah.
  Setelah mengedit prefab, simpan prefab tersebut. Tidak perlu menjalankan ulang menu builder.

## Prefab dan PrefabLibrary

```
Assets/Prefabs/Characters/  Player.prefab   Enemy.prefab
Assets/Prefabs/Level/       Platform.prefab Checkpoint.prefab Goal.prefab Trap.prefab KillZone.prefab
Assets/Prefabs/Thread/      Anchor.prefab
Assets/Data/Resources/Untai/PrefabLibrary.asset   (referensi semua prefab + ukuran dasar platform)
```

- **Cara kerja:** `GameDirector` memegang referensi `PrefabLibrary` dan `GameplayParameters` di Inspector.
  Builder runtime (`LevelBuilder`, `ChunkBuilder`, `EnemySpawner`, `PlayerMotor.Spawn`) meng-*Instantiate*
  prefab dari slot pustaka; posisi dan skala platform tetap dari data chunk. Jika pustaka atau slot kosong,
  builder membuat primitif dari kode (fallback), jadi game tetap jalan.
- **Mengubah warna:** edit material di `Assets/Materials` (misal `M_PlatformHard`, `M_EnemyChase`) atau ganti
  field material pada komponen prefab (`PlatformVisual`, `EnemyBrain`, `Anchor`, `Checkpoint`).
- **Mengganti model dummy:** buka prefab (misal `Player.prefab`), tambahkan model sebagai child, lalu matikan
  atau hapus `MeshRenderer` kapsul. **Jangan hapus** collider dan komponen skripnya (`PlayerMotor`,
  `ThreadController`, `ThreadRenderer`, `PlayerHealth`, `ThreadSwing`, `Rigidbody`). Untuk enemy, arahkan
  `EnemyBrain.bodyRenderer` ke renderer model baru agar warna state tetap terlihat.
- **Prefab baru:** buat prefab sendiri lalu seret ke slot di `PrefabLibrary`; slot yang terisi tidak diubah
  oleh menu builder.

## Kontrol

| Tombol | Fungsi |
|---|---|
| A/D atau panah | Gerak |
| Space | Lompat (tahan = lebih tinggi); saat mengayun = lepas dengan dorongan |
| Tahan Shift / J | Kaitkan benang ke jangkar (bola biru, menyala kuning bila terjangkau) |
| W/S | Gulung/ulur tali saat mengayun |
| R | Respawn di checkpoint |

## Tombol Demo

| Tombol | Fungsi |
|---|---|
| F1 | Tampilkan/sembunyikan HUD |
| F2 | Simulasi mati |
| F3 / F4 | Simulasi performa bagus / buruk |
| F5 | Seed acak baru |
| F6 | Ulang level dengan seed sama |
| F7 | Level berikutnya |
| F8 | Reset DDA |
| F9 | Lompat ke chunk berikutnya |

Alur sistem, keputusan desain per fase, dan skenario demo: [docs/DEV_LOG.md](docs/DEV_LOG.md).
