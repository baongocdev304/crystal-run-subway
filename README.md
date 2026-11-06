# Chạy Đua Tinh Thể (CrystalRun) — 3D Endless Runner

Dự án game 3D chạy vô tận (Endless Runner) góc nhìn thứ ba phong cách low-poly được phát triển trên nền tảng **Unity 6 LTS (Universal 3D / Universal Render Pipeline - URP)**, tối ưu cho macOS Apple Silicon.

---

## 1. Hướng Dẫn Dựng Toàn Bộ Project Bằng Code (Automated Build)

Toàn bộ tài nguyên, vật liệu, prefabs, hệ thống sinh đường, giao diện Canvas và màn chơi `Main.unity` đều được dựng hoàn toàn tự động bằng script Editor [`CrystalRunBuilder.cs`](file:///Users/nguyenbaongoc/Developer/Game-unity-baongoc/Assets/Scripts/Editor/CrystalRunBuilder.cs) mà **không cần bất kỳ thao tác kéo thả thủ công nào**:

1. **Mở Unity Editor:** Khởi chạy project bằng Unity Hub với phiên bản Unity 6 LTS (khuyến nghị `6000.6.8f1` hoặc mới hơn).
2. **Chờ Biên dịch:** Để Unity hoàn tất import và biên dịch các file mã nguồn C#.
3. **Thực thi Auto-Build:**
   - Trên thanh menu trên cùng của Unity Editor, chọn:
     **`Tools` > `Crystal Run` > `Build All`**
   - Script tự động chạy trong vài giây để:
     - Tạo và đăng ký các Tag (`Player`, `Coin`, `Crystal`, `PowerUp`, `Obstacle`, `Pit`) và Layer (`Player`, `Obstacle`, `Ground`) vào `ProjectSettings/TagManager.asset`.
     - Tạo bộ vật liệu màu đơn sắc URP Lit Shader tại `Assets/Materials/` (bật Emission cho xu/tinh thể, Transparent cho hào quang khiên).
     - Tạo asset cấu hình cân bằng game `Assets/GameConfig.asset`.
     - Tạo toàn bộ Prefab tại `Assets/Prefabs/`: Nhân vật, Xu vàng, Mảnh tinh thể, 3 loại Power-Up, 4 loại Chướng ngại vật, và 10 biến thể Đoạn đường (`TrackSegment`).
     - Tạo màn chơi hoàn chỉnh tại `Assets/Scenes/Main.unity` với đầy đủ Ánh sáng, Sương mù, Camera bám theo, TrackSpawner, bộ tứ Quản lý (GameManager, ScoreManager, PowerUpManager, UIManager), EventSystem (Input System) và Canvas giao diện 4 Panels.
     - Đăng ký `Main.unity` vào `EditorBuildSettings`.
4. **Trải nghiệm Game:**
   - Mở màn chơi tại [`Assets/Scenes/Main.unity`](file:///Users/nguyenbaongoc/Developer/Game-unity-baongoc/Assets/Scenes/Main.unity).
   - Nhấn nút **Play** (▶) trên thanh công cụ Unity để bắt đầu chơi!

---

## 2. Bảng Phím Điều Khiển (Input Controls)

Game sử dụng chuẩn **Unity Input System mới** (`UnityEngine.InputSystem`):

| Hành động | Bàn phím chính | Phím mũi tên thay thế | Ghi chú |
| --- | --- | --- | --- |
| **Đổi làn sang trái** | `A` | `Mũi tên trái` ($\leftarrow$) | Chuyển làn mượt mà, chặn ở làn ngoài cùng ($X = -2.5m$). |
| **Đổi làn sang phải** | `D` | `Mũi tên phải` ($\rightarrow$) | Chuyển làn mượt mà, chặn ở làn ngoài cùng ($X = +2.5m$). |
| **Nhảy lên** | `Space` | `Mũi tên lên` ($\uparrow$) | Nhảy qua vật cản thấp và hố sâu khi đang chạm đất. |
| **Trượt gầm** | `S` | `Mũi tên xuống` ($\downarrow$) | Thu nhỏ collider còn 0.6m để chui qua vật cản treo cao. |
| **Rơi nhanh (Fast-fall)** | `S` | `Mũi tên xuống` ($\downarrow$) | Nhấn khi đang ở trên không để cắm thẳng xuống đất rồi trượt. |
| **Tạm dừng / Tiếp tục** | `Escape` hoặc `P` | — | Đóng băng thời gian và mở menu Tạm dừng. |

---

## 3. Các Cơ Chế Chơi Chính

### 3.1. Chướng Ngại Vật
- **Vật cản thấp (Rào đá, thùng gỗ):** Cao 0.9m — Bấm `Space` để nhảy qua hoặc đổi làn né tránh.
- **Vật cản treo cao (Xà ngang, rìu cổ):** Đáy xà cách mặt sàn 1.1m — Bấm `S` để trượt người chui qua gầm an toàn.
- **Hố sâu (Pit):** Rơi xuống hố là **thua ngay lập tức**, không phụ thuộc vào khiên hay số mạng.
- **Tảng đá lăn (Rolling Boulder):** Lăn ngang qua lại giữa các làn theo chu kỳ hình sin, tự động kích hoạt khi người chơi chạy tới gần trong phạm vi 35m.

### 3.2. Thu Thập & Power-Up
- 🪙 **Xu vàng:** Cộng 10 điểm (được nam châm tự động hút về phía người chơi).
- 💎 **Mảnh tinh thể:** Cộng 50 điểm. Gom đủ **10 mảnh** sẽ tự động hồi **1 mạng** sống.
- 🛡️ **Khiên bảo vệ (Shield - 8s):** Hấp thụ hoàn toàn 1 lần va chạm với chướng ngại vật (vỡ khiên nhưng không mất mạng), kích hoạt bất tử ngắn 1s.
- 🧲 **Nam châm (Magnet - 8s):** Tự động hút toàn bộ xu vàng trong bán kính 7m bay về phía người chơi.
- ⚡ **Nhân đôi điểm (Double Score - 10s):** Gấp đôi điểm số nhận được từ cả xu và tinh thể trong suốt thời gian hiệu lực.

### 3.3. Các Giai Đoạn Độ Khó
- **Giai đoạn 1 (0 – 500m):** Tốc độ khởi điểm 10 m/s, chỉ gồm các vật cản thấp dễ né.
- **Giai đoạn 2 (500 – 1500m):** Xuất hiện hố sâu sụp sàn và vật cản treo cao bắt buộc phải trượt.
- **Giai đoạn 3 (Từ 1500m trở lên):** Bổ sung tảng đá lăn khổng lồ, mật độ cạm bẫy dày đặc, tốc độ tăng dần đến tối đa 25 m/s.

---

## 4. Cấu Trúc Mã Nguồn

```
Assets/
├── Materials/         # Vật liệu URP Lit (Mat_Ground, Mat_Player, Mat_Coin...)
├── Prefabs/           # Prefabs nhân vật, vật cản, item, đoạn đường TrackSegment
├── Scenes/            # Scene chính Main.unity
├── Scripts/
│   ├── Editor/        # CrystalRunBuilder.cs (Công cụ Auto-Build 1-Click)
│   ├── Items/         # Collectible.cs, Coin.cs, Crystal.cs, PowerUp.cs, Obstacle.cs, RollingBoulder.cs
│   ├── Managers/      # GameManager.cs, ScoreManager.cs, PowerUpManager.cs, GameConfig.cs
│   ├── Player/        # PlayerController.cs, CameraFollow.cs, PlayerHealth.cs
│   └── UI/            # UIManager.cs (TextMeshPro 4-Panels UI)
├── CONVENTIONS.md     # Quy ước lập trình C# chuẩn dự án
└── GAME_SPEC.md       # Thiết kế chi tiết game "Chạy Đua Tinh Thể"
```

