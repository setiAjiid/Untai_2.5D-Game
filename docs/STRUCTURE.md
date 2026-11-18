# Struktur Folder Untai

| Folder | Namespace | Isi |
|---|---|---|
| `Assets/Scripts/Player` | `Untai.Player` | Gerak pemain, nyawa, respawn |
| `Assets/Scripts/Thread` | `Untai.Thread` | Sumber daya benang, render benang, ayunan jangkar |
| `Assets/Scripts/Level` | `Untai.Level` | Checkpoint, kill zone, goal, pembangun level runtime |
| `Assets/Scripts/PCG` | `Untai.PCG` | Data chunk, perakit chunk berbasis seed, validasi jangkauan |
| `Assets/Scripts/DDA` | `Untai.DDA` | Pencatat performa, skor kesulitan, pemetaan parameter |
| `Assets/Scripts/AI` | `Untai.AI` | FSM enemy |
| `Assets/Scripts/Debug` | `Untai.DebugTools` | HUD debug dan tombol demo |
| `Assets/Scripts/CameraRig` | `Untai.CameraRig` | Kamera samping |
| `Assets/Scripts/Core` | `Untai.Core` | Event global, input, game loop, utilitas primitif |
| `Assets/Scripts/Data` | `Untai.Data` | ScriptableObject parameter gameplay |
| `Assets/Scripts/Editor` | `Untai.EditorTools` | Menu dan generator aset (khusus Editor) |
| `Assets/Data/Resources/Untai` | - | Aset parameter dan chunk yang dimuat saat runtime |

Catatan: folder `Debug` memakai namespace `Untai.DebugTools` dan folder `Editor`
memakai `Untai.EditorTools` supaya tidak bentrok dengan `UnityEngine.Debug` dan
`UnityEditor.Editor` ketika dipanggil dari dalam namespace `Untai`.
