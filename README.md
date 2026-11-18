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
| Level | `Assets/Scripts/Level` | Checkpoint, kill zone, goal, pembangun chunk menjadi primitif |
| PCG | `Assets/Scripts/PCG` | Data chunk, perakit chunk berbobot berbasis seed, validasi jangkauan (BFS) |
| DDA | `Assets/Scripts/DDA` | Pencatat performa, skor kesulitan rolling window, pemetaan ke parameter |
| AI | `Assets/Scripts/AI` | FSM enemy (Patrol, Chase, Attack, Return) |
| Data | `Assets/Scripts/Data` | `GameplayParameters` (ScriptableObject), sumber tunggal parameter |
| Debug | `Assets/Scripts/Debug` | HUD debug dan tombol demo |
| CameraRig | `Assets/Scripts/CameraRig` | Kamera follow samping |
| Core | `Assets/Scripts/Core` | Event global, input, game loop |

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
   Scene lain (misal `SampleScene`) juga bisa: bootstrap otomatis membuat game saat Play.
3. Tidak ada langkah manual wajib. Aset chunk dibuat otomatis oleh Editor saat proyek dimuat
   (atau lewat menu **Untai > Generate Sample Chunks**); tanpa aset pun game memakai definisi bawaan.

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
