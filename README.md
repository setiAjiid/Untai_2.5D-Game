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
Pemain membawa **benang** berpanjang terbatas yang terikat pada checkpoint terakhir dan terurai
**mengikuti jalur yang dilalui pemain**, serta dapat dipakai untuk **mengayun dari jangkar** melewati celah. Level dirakit otomatis dari **chunk**
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
| UI | `Assets/Scripts/UI` | `PlayerHud`: nyawa, bar benang, level dan seed, umpan balik damage |
| Title | `Assets/Scripts/Title` | `TitleManager`: tombol Title Screen dan kolom seed opsional |
| CameraRig | `Assets/Scripts/CameraRig` | Kamera follow samping |
| Core | `Assets/Scripts/Core` | `GameDirector` (perakit sistem dan game loop), event global, input, `GameLaunch` (seed dari Title Screen) |
| Editor | `Assets/Scripts/Editor` | Menu Untai: generator chunk, prefab, material, dan isi scene |
| Prefab | `Assets/Prefabs/{Characters,Level,Thread}` | Player, Enemy / Platform, Checkpoint, Goal, Trap, KillZone / Anchor, ThreadPickup |
| Material | `Assets/Materials` | Satu material per warna (pemain, state enemy, tingkat platform, dll.) |

Detail struktur folder: [docs/STRUCTURE.md](docs/STRUCTURE.md).

## Status

Prototipe siap demo (Project EAS). Yang sudah berjalan:

- [x] Gerak samping 2.5D, lompat variabel, coyote time, kamera follow
- [x] Benang terikat checkpoint (sumber daya), render benang, ayunan jangkar sederhana
- [x] Checkpoint, kill zone, jebakan, goal, respawn
- [x] **PCG**: 19 chunk (mudah/sedang/sulit, 4 ayun wajib, 2 benang boros, 1 peti) + tutorial ayun,
  perakit weighted ber-seed, tolak sambungan, validasi BFS (ayunan wajib dan tinggi peti diperhitungkan)
- [x] **DDA**: rolling window performa, tiga kondisi, pemetaan parameter dengan clamp dan step limit
- [x] **Game AI**: FSM enemy (Patrol, Chase, Attack, Return), agresivitas dari DDA
- [x] HUD debug (alur Performance -> DDA -> Parameter -> Gameplay), tombol demo, game loop antar level
- [x] Benang berbasis jalur (terurai mengikuti jejak, tergulung saat mundur), pickup gulungan benang
- [x] Benang sebagai alat wajib: anggaran ketat, chunk ayun wajib, tarik peti untuk pijakan, peringatan benang menipis
- [x] HUD pemain (nyawa, bar benang, level dan seed) dengan umpan balik damage
- [x] Title Screen ke Untai_Main (seed acak, seed tetap, atau seed dari kolom input), Esc kembali ke Title
- [ ] Companion Utility AI, aset visual, audio, efek (belum, opsional)

## Cara Menjalankan

1. Buka proyek dengan **Unity 6000.3.23f1**.
2. Buka scene `Assets/Scenes/TitleScreen.unity` lalu tekan **Play**. Alur scene:
   - **Start Game**: membuka `Untai_Main` dengan seed acak (atau seed dari kolom input bila diisi).
   - **Start Game - Set Seed**: membuka `Untai_Main` dengan seed tetap 12345 (atau seed dari kolom input).
   - **Exit**: keluar dari game. Di dalam permainan, **Esc** kembali ke Title Screen.
3. `Untai_Main.unity` juga bisa dimainkan langsung (seed acak karena `GameDirector.randomStartSeed = true`).
   Hierarchy scene berisi `GameDirector`, `Player` (instance prefab), `Main Camera` (dengan
   `SideFollowCamera`), `KillZone`, `Directional Light`, dan `Global Volume`. Objek level hasil PCG
   dibuat saat Play di bawah `GeneratedLevel`. Scene tanpa `GameDirector` (misal `SampleScene`) tidak
   lagi membuat game otomatis.
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
- **Build Settings:** urutan otomatis `TitleScreen` (indeks 0) lalu `Untai_Main`. Jika belum, jalankan
  **Untai > Fix Build Settings Scene Order**.
- **Title Screen:** bila tombol tidak membuka `Untai_Main` atau kolom seed belum ada, jalankan
  **Untai > Fix Title Wiring** (mengatur `TitleManager`, `randomStartSeed`, Build Settings, dan membuat
  kolom input seed).
- **Prefab pickup benang dan peti tarik:** jalankan **Untai > Build Prefabs and Scene** sekali agar
  `ThreadPickup.prefab` dan `PullCrate.prefab` dibuat dan dipasang di `PrefabLibrary`. Tanpa prefab,
  keduanya tetap muncul sebagai primitif.
- **Aset chunk baru:** chunk W1 sampai W4, B1, B2, dan C1 langsung dipakai dari definisi kode. Jalankan
  **Untai > Generate Sample Chunks** bila ingin membuat atau memperbarui aset `.asset`-nya untuk diedit di Inspector.
- **Input:** proyek memakai Input System saja (`Project Settings > Player > Active Input Handling` =
  Input System Package). Jangan diganti ke Input Manager lama, karena input dibaca lewat Input System.
- **Ruang disk:** Unity butuh ruang kosong untuk cache dan impor paket. Bila muncul error `ENOSPC` atau
  `no space left on device`, kosongkan drive tempat proyek dan drive C: (cache Unity).
- **Mengganti model dummy:** lihat bagian [Prefab dan PrefabLibrary](#prefab-dan-prefablibrary) di bawah.
  Setelah mengedit prefab, simpan prefab tersebut. Tidak perlu menjalankan ulang menu builder.

## Prefab dan PrefabLibrary

```
Assets/Prefabs/Characters/  Player.prefab   Enemy.prefab
Assets/Prefabs/Level/       Platform.prefab Checkpoint.prefab Goal.prefab Trap.prefab KillZone.prefab PullCrate.prefab
Assets/Prefabs/Thread/      Anchor.prefab   ThreadPickup.prefab
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

## Mekanik Benang

- Benang terikat pada checkpoint aktif dan **terurai mengikuti jalur** yang benar-benar dilalui pemain.
  `ThreadController` mencatat jejak (titik baru tiap pemain berpindah sekitar 0,25 unit) pada buffer tetap;
  benang terpakai = panjang jejak + segmen terakhir ke pemain. Lompatan, panjatan, dan ayunan ikut
  menghabiskan benang.
- **Gulung balik:** berjalan mundur menyusuri jejak membuang titik jejak sehingga benang pulih bertahap.
- **Benang tegang:** bila benang habis, pemain tidak bisa maju tetapi tetap boleh mundur.
- Checkpoint baru atau respawn mengosongkan jejak. **Pickup gulungan** (bola merah muda) menambah panjang
  benang maksimum sebesar `pickupThreadBonus` (awal 4 unit); muncul 0 sampai 1 per chunk, lebih sering di chunk sulit.
- PCG memperkirakan panjang jalur sebagai jarak horizontal x `PathFactor` (1,6) sehingga jarak antar
  checkpoint selalu muat dalam benang.

### Peran benang dalam desain level

1. **Rintangan nyata.** Panjang benang default 36 (DDA 40 mudah sampai 32 sulit). Jarak checkpoint rata-rata
   sekitar 14 unit x `PathFactor` 1,6 = sekitar 22 unit jalur, jadi pemain memakai sekitar 56 sampai 70 persen
   benang di antara dua checkpoint. Checkpoint tetap mengisi penuh. Di bawah 20 persen bar benang berdenyut
   merah, benang di dunia menipis dan memerah, dan muncul teks **BENANG TEGANG** saat benang habis.
2. **Ayunan wajib.** Chunk W1 sampai W4 punya celah minimal 7 unit (lebih lebar dari `JumpDistance` 5,38 x 1,25)
   dengan jangkar 3,5 unit ke kanan dan 3,5 unit di atas titik pegang di tepi platform, sehingga tali 4,95 unit
   tetap terjangkau pada jangkauan jangkar tersulit (5,0). Validator menandai lompatan melintasi celah pada chunk
   ini sebagai tidak mungkin dan hanya menerima jalur yang memakai ayunan. Chunk pertama setelah S0 selalu
   **T0 (tutorial ayun)** dengan petunjuk di layar yang hilang setelah pemain berhasil menyeberang.
3. **Benang boros.** Chunk B1 dan B2 punya jalur bawah lewat lembah (turun lalu naik lagi) yang menghabiskan
   banyak benang, dibanding jalur atas yang pendek lewat ayunan.
4. **Tarik peti.** Chunk C1 punya tebing 2,7 unit (lebih tinggi dari lompatan 2,47). Pemain berjalan ke kaki
   tebing, mengait peti di belakangnya (Shift/J), menahan **S** untuk menarik peti, lalu memanjat peti.
   Validator menambahkan tinggi peti (1,2) ke tinggi lompat efektif hanya pada chunk berpeti.
5. **Pickup di rute berisiko.** Pickup diletakkan di dekat titik terendah busur ayunan atau di atas celah,
   bukan di jalur aman.

### Tampilan benang

Benang wol (tebal 0,12) bergradasi dari titik tambat ke pemain dan dihaluskan dengan Catmull-Rom. Titik jejak
yang dicatat saat pemain di tanah digambar sedikit di atas permukaan. Segmen kendur ke pemain menggantung sesuai
sisa benang, garis kait ke jangkar atau peti hampir lurus, ada simpul kecil di checkpoint, dan partikel serat
muncul saat mengait atau melepas.

## HUD Pemain

`PlayerHud` selalu tampil (F1 hanya menyembunyikan HUD debug): kotak nyawa di kiri bawah, bar sisa benang
yang memerah saat hampir habis, serta level dan seed di pojok kanan bawah. Saat terkena damage layar berkedip
merah, hati yang hilang berkedip, dan karakter berkedip selama kebal. Pesan singkat muncul saat respawn.

## Kontrol

| Tombol | Fungsi |
|---|---|
| A/D atau panah | Gerak |
| Space | Lompat (tahan = lebih tinggi); saat mengayun = lepas dengan dorongan |
| Tahan Shift / J | Kaitkan benang ke jangkar (bola biru, menyala kuning bila terjangkau) |
| Shift / J dekat peti + tahan S | Kaitkan benang ke peti (menyala kuning), tahan S untuk menariknya; lepas Shift/J untuk melepas |
| W/S | Gulung/ulur tali saat mengayun |
| R | Respawn di checkpoint |
| Esc | Kembali ke Title Screen |

## Tombol Demo

| Tombol | Fungsi |
|---|---|
| F1 | Tampilkan/sembunyikan HUD debug (HUD pemain tetap tampil) |
| F2 | Simulasi mati |
| F3 / F4 | Simulasi performa bagus / buruk |
| F5 | Seed acak baru |
| F6 | Ulang level dengan seed sama |
| F7 | Level berikutnya |
| F8 | Reset DDA |
| F9 | Lompat ke chunk berikutnya |

Alur sistem, keputusan desain per fase, dan skenario demo: [docs/DEV_LOG.md](docs/DEV_LOG.md).
