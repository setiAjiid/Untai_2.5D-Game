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
Pemain mengendalikan **dua karakter (Tandem) yang terikat satu untai**, dimainkan satu orang dengan
berganti karakter (Tab/Q). Untai adalah mekanik utama: karakter yang berdiri menjadi **tumpuan**
sehingga pasangannya bisa **menggantung dan berayun** menyeberangi jurang, **memanjat untai**,
**menarik pasangan**, atau **mengait jangkar** untuk naik ke tebing. Level dirakit otomatis dari **chunk**
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
| Player | `Assets/Scripts/Player` | Gerak samping, lompat variabel, nyawa, `Tandem` (dua karakter, ganti karakter, respawn bersama) |
| Thread | `Assets/Scripts/Thread` | `TandemRope` (untai pengikat: tumpuan, ayun, panjat, tarik, kait jangkar, render) dan `Anchor` |
| Level | `Assets/Scripts/Level` | Checkpoint, kill zone, goal, jebakan, `LevelBuilder`/`ChunkBuilder` (instansiasi prefab per chunk) |
| PCG | `Assets/Scripts/PCG` | Data chunk, perakit chunk berbobot berbasis seed, validasi jangkauan (BFS) |
| DDA | `Assets/Scripts/DDA` | Pencatat performa, skor kesulitan rolling window, pemetaan ke parameter |
| AI | `Assets/Scripts/AI` | FSM enemy (Patrol, Chase, Attack, Return) |
| Data | `Assets/Scripts/Data` | `GameplayParameters` (sumber tunggal parameter) dan `PrefabLibrary` (daftar prefab) |
| Debug | `Assets/Scripts/Debug` | HUD debug dan tombol demo |
| CameraRig | `Assets/Scripts/CameraRig` | Kamera follow samping |
| Core | `Assets/Scripts/Core` | `GameDirector` (perakit sistem dan game loop), event global, input |
| Editor | `Assets/Scripts/Editor` | Menu Untai: generator chunk, prefab, material, dan isi scene |
| Prefab | `Assets/Prefabs/{Characters,Level,Thread}` | CharacterA, CharacterB, Enemy / Platform, Checkpoint, Goal, Trap, KillZone / Anchor, Tandem |
| Material | `Assets/Materials` | Satu material per warna (pemain, state enemy, tingkat platform, dll.) |

Detail struktur folder: [docs/STRUCTURE.md](docs/STRUCTURE.md).

## Status

Prototipe siap demo (Project EAS). Yang sudah berjalan:

- [x] Gerak samping 2.5D, lompat variabel, coyote time, kamera follow
- [x] Tandem: dua karakter terikat untai, ganti karakter, menggantung/berayun, panjat, tarik pasangan, kait jangkar
- [x] **Partner AI (Utility AI)**: karakter yang tidak dikendalikan berjalan sendiri (Follow, Wait, Assist), perintah manual E, tingkat bantuan diatur DDA
- [x] Checkpoint, kill zone, jebakan, goal, respawn
- [x] **PCG**: 12 chunk (mudah/sedang/sulit), perakit weighted ber-seed, tolak sambungan, validasi BFS
- [x] **DDA**: rolling window performa, tiga kondisi, pemetaan parameter dengan clamp dan step limit
- [x] **Game AI**: FSM enemy (Patrol, Chase, Attack, Return), agresivitas dari DDA
- [x] HUD debug (alur Performance -> DDA -> Parameter -> Gameplay), tombol demo, game loop antar level
- [ ] Companion Utility AI, aset visual, audio, efek (belum, opsional)

## Cara Menjalankan

1. Buka proyek dengan **Unity 6000.3.23f1**.
2. Buka scene `Assets/Scenes/Untai_Main.unity` lalu tekan **Play**.
   Hierarchy scene berisi `GameDirector`, `CharacterA`, `CharacterB`, `Tandem` (instance prefab), `Main Camera` (dengan
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

- **Aset chunk:** Editor membuat `Assets/Data/Resources/Untai/TandemChunks/*.asset` otomatis saat proyek dimuat (folder `Chunks` lama tidak dipakai lagi dan boleh dihapus).
  File ini tidak wajib (game memakai definisi chunk dari kode bila tidak ada), tetapi boleh di-commit:
  `git add Assets/Data/Resources/Untai/TandemChunks`.
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
Assets/Prefabs/Characters/  CharacterA.prefab   CharacterB.prefab   Enemy.prefab
Assets/Prefabs/Level/       Platform.prefab Checkpoint.prefab Goal.prefab Trap.prefab KillZone.prefab
Assets/Prefabs/Thread/      Anchor.prefab   Tandem.prefab (Tandem + TandemRope + LineRenderer)
Assets/Data/Resources/Untai/PrefabLibrary.asset   (referensi semua prefab + ukuran dasar platform)
```

- **Cara kerja:** `GameDirector` memegang referensi `PrefabLibrary` dan `GameplayParameters` di Inspector.
  Builder runtime (`LevelBuilder`, `ChunkBuilder`, `EnemySpawner`, `PlayerMotor.Spawn`) meng-*Instantiate*
  prefab dari slot pustaka; posisi dan skala platform tetap dari data chunk. Jika pustaka atau slot kosong,
  builder membuat primitif dari kode (fallback), jadi game tetap jalan.
- **Mengubah warna:** edit material di `Assets/Materials` (misal `M_PlatformHard`, `M_EnemyChase`) atau ganti
  field material pada komponen prefab (`PlatformVisual`, `EnemyBrain`, `Anchor`, `Checkpoint`).
- **Mengganti model dummy:** buka prefab (misal `CharacterA.prefab`), tambahkan model sebagai child, lalu matikan
  atau hapus `MeshRenderer` kapsul. **Jangan hapus** collider dan komponen skripnya (`PlayerMotor`,
  `PlayerHealth`, `Rigidbody`). Nama objek `CharacterA`/`CharacterB` di scene dipakai untuk mencari karakter. Untuk enemy, arahkan
  `EnemyBrain.bodyRenderer` ke renderer model baru agar warna state tetap terlihat.
- **Prefab baru:** buat prefab sendiri lalu seret ke slot di `PrefabLibrary`; slot yang terisi tidak diubah
  oleh menu builder.

## Kontrol

| Tombol | Fungsi |
|---|---|
| A/D atau panah | Gerak; saat menggantung: memompa ayunan |
| Space | Lompat (tahan = lebih tinggi); saat menggantung: lompat dari untai; saat mengait jangkar: lepas dengan dorongan |
| Tab / Q | Ganti karakter aktif; karakter lain dijalankan partner AI |
| E | Perintah partner bergantian: "Tunggu di sini" / "Ikuti saya" |
| W/S | Panjat / turuni untai saat menggantung (dari pasangan atau jangkar) |
| Tahan F | Gulung untai: tarik pasangan mendekat / naik |
| Tahan Shift / J | Kaitkan untai ke jangkar (bola biru, menyala kuning bila terjangkau) |
| R | Respawn keduanya di checkpoint |

### Partner AI

Cukup kendalikan satu karakter; karakter lain dijalankan **PartnerBrain** (Utility AI) lewat `PlayerMotor`
yang sama (input disimulasikan, tanpa teleport). Saat Tab ditekan, peran bertukar.

| Aksi | Perilaku |
|---|---|
| **Follow** | Mengikuti sampai jarak nyaman; melompati dinding rendah, celah kecil, dan jebakan; menyeberang jurang dengan menggantung lalu memanjat untai sendiri; minta ditarik bila tertahan di bawah tebing |
| **Wait** | Diam sebagai tumpuan untai (otomatis saat karakter aktif menggantung atau mengait jangkar) |
| **Assist** | Pergi ke tepi jurang di depan karakter aktif lalu menunggu sebagai tumpuan ayunan |

- Skor tiap aksi (0 sampai 1) dihitung dari jarak ke karakter aktif, kelonggaran untai (jarak / panjang
  untai), jurang atau tebing di depan karakter aktif, status menggantung/mengait jangkar, dan perintah E.
  Pergantian aksi memakai histeresis (jeda minimal 1 sampai 2 detik dan selisih skor 0,1), kecuali
  pengaman: partner langsung menjadi tumpuan saat karakter aktif bergantung pada untai.
- Perintah E menambah skor aksi terkait (+0,5) dan meredam aksi lain (x0,7): berpengaruh besar tetapi tidak
  mutlak.
- `partnerAssistLevel` (DDA) membuat partner lebih cepat menyusul, memanjat, bereaksi, dan pulih; pemain yang
  sering mati mendapat partner yang lebih cekatan dan menempel lebih dekat (`partnerFollowDistance`).
- Bila partner tidak maju beberapa detik (5 sampai 10 detik tergantung assist), ia dianggap terjebak: keduanya
  respawn bersama di dekat karakter aktif (atau di checkpoint bila karakter aktif sedang di udara).

### Cara menyelesaikan rintangan

- **Jurang lebar tanpa jangkar:** dekati tepi jurang; partner otomatis Assist (berdiri di tepi sebagai
  tumpuan). Jalan turun dari tepi, pompa ayunan (A/D), lalu lompat dari untai (Space) ke seberang. Partner
  menyusul sendiri: ia turun ke jurang, menggantung pada karaktermu, memanjat, lalu melompat naik.
  Tahan F untuk menariknya lebih cepat.
- **Tebing lebih tinggi dari lompatan:** dekati jangkar, tahan Shift untuk mengait, panjat (W), ayun, lalu
  Space untuk melompat ke atas tebing. Partner menunggu, lalu meminta ditarik naik dengan untai; tahan F
  untuk membantu.
- **Untai terlalu tegang:** karakter aktif tertahan bila terlalu jauh dari pasangannya; ganti karakter
  atau tarik pasangan untuk maju bersama.

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
| F10 | Paksa aksi partner bergiliran: Follow, Wait, Assist, lalu kembali bebas (Utility AI) |

Alur sistem, keputusan desain per fase, dan skenario demo: [docs/DEV_LOG.md](docs/DEV_LOG.md).
