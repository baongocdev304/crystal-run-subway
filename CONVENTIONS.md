# QUY ƯỚC LẬP TRÌNH DỰ ÁN CRYSTALRUN (CONVENTIONS.MD)

Tài liệu này định nghĩa toàn bộ tiêu chuẩn, kiến trúc và quy ước code cho dự án game 3D Endless Runner **CrystalRun** ("Chạy Đua Tinh Thể") trên nền tảng **Unity 6 LTS (Universal 3D / URP)**, chạy trên macOS Apple Silicon.

Mọi mã nguồn C# và tài nguyên phát triển trong các giai đoạn tiếp theo bắt buộc phải tuân thủ nghiêm ngặt các quy tắc dưới đây để đảm bảo code luôn sạch, tối ưu và biên dịch đúng ngay lần đầu.

---

## 1. Cấu Trúc Thư Mục và Phân Chia Namespace

### 1.1. Cấu trúc thư mục mã nguồn (`Assets/Scripts/`)
- Mọi script C# phải được đặt đúng thư mục chức năng tương ứng:
  - `Assets/Scripts/Player/`: Điều khiển nhân vật, camera bám theo, trạng thái nhân vật.
  - `Assets/Scripts/World/`: Sinh đường chạy (TrackSpawner), di chuyển module, tái chế môi trường.
  - `Assets/Scripts/Items/`: Xu vàng, mảnh tinh thể, vật phẩm hỗ trợ (Khiên, Nam châm, Nhân đôi điểm).
  - `Assets/Scripts/Managers/`: `GameManager`, `AudioManager`, `PowerUpManager`, `GameConfig` (ScriptableObject).
  - `Assets/Scripts/UI/`: Điều khiển giao diện menu, HUD, tạm dừng, game over.
  - `Assets/Scripts/Editor/`: Script công cụ mở rộng Editor, tiện ích inspector.

### 1.2. Thư mục tài nguyên khác
- `Assets/Prefabs/`: Chứa prefab nhân vật, chướng ngại vật, đoạn đường (module), vật phẩm, UI.
- `Assets/Materials/`: Chứa vật liệu URP (Universal Render Pipeline).
- `Assets/Scenes/`: Chứa các màn chơi (MainMenu, Gameplay).

### 1.3. Quy ước Namespace
- Toàn bộ logic trò chơi, vật thể, quản lý nằm trong namespace:
  ```csharp
  namespace CrystalRun
  {
      // Code logic, Player, World, Items, Managers
  }
  ```
- Toàn bộ các script giao diện người dùng nằm trong:
  ```csharp
  namespace CrystalRun.UI
  {
      // Code HUD, Menu, UI Controllers
  }
  ```
- Toàn bộ các công cụ mở rộng trong Editor nằm trong:
  ```csharp
  namespace CrystalRun.Editor
  {
      // Code CustomEditor, PropertyDrawer, EditorWindows
  }
  ```

### 1.4. Đặt tên file và Class
- **Tên file C# phải trùng khớp 100% với tên class** (ví dụ: `PlayerController.cs` chứa `public class PlayerController : MonoBehaviour`).
- Đặt tên theo chuẩn **PascalCase** cho class, struct, enum, public method và public property.
- Đặt tên theo chuẩn **camelCase** hoặc `_camelCase` cho private/protected field.

---

## 2. Hệ Thống Điều Khiển (Input System)

- **BẮT BUỘC** sử dụng **Unity Input System mới** (`com.unity.inputsystem`).
  ```csharp
  using UnityEngine.InputSystem;
  ```
- Truy cập input trực tiếp qua các thiết bị phần cứng hiện đại:
  ```csharp
  // Bàn phím
  if (Keyboard.current != null)
  {
      if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
      {
          // Chuyển làn sang trái
      }
      if (Keyboard.current.spaceKey.wasPressedThisFrame)
      {
          // Nhảy
      }
  }
  ```
- **TUYỆT ĐỐI KHÔNG** sử dụng `UnityEngine.Input` cũ (ví dụ: `Input.GetKeyDown`, `Input.GetAxis`, `Input.GetKey`).

---

## 3. Giao Diện Người Dùng (UI)

- **BẮT BUỘC** sử dụng **TextMeshPro** cho hiển thị văn bản:
  ```csharp
  using TMPro;
  
  [SerializeField] private TMP_Text scoreText;
  ```
- **TUYỆT ĐỐI KHÔNG** sử dụng `UnityEngine.UI.Text` cũ (Legacy Text) để tránh lỗi font và giảm chất lượng hiển thị.

---

## 4. Quản Lý Tham Số (ScriptableObject GameConfig)

- Tất cả các hằng số cân bằng game (game balance parameters) phải được tập trung trong một **ScriptableObject** duy nhất tên là `GameConfig`:
  - Tốc độ chạy ban đầu, tốc độ tối đa, mức tăng tốc mỗi 500m.
  - Khoảng cách giữa các làn, tốc độ chuyển làn.
  - Lực nhảy, trọng lực, thời gian trượt.
  - Số mạng ban đầu, thời gian bất tử sau va chạm.
  - Điểm số của xu, điểm số của tinh thể, số tinh thể để hồi 1 mạng.
  - Thời gian hiệu lực của khiên, nam châm, nhân đôi điểm.
  - Bán kính hút xu và tốc độ hút của nam châm.
  - Các mốc khoảng cách phân chia Giai đoạn 1, 2, 3.
- **TUYỆT ĐỐI KHÔNG** hard-code các con số ma thuật (magic numbers) rải rác bên trong các script gameplay. Mọi script cần tham số phải tham chiếu đến `GameConfig`.

---

## 5. Kiến Trúc Giao Tiếp & Tương Tác Giữa Các Đối Tượng

- **Mô hình Event-Driven (Sự kiện):**
  - Sử dụng C# events / `System.Action` để thông báo trạng thái giữa các hệ thống độc lập (ví dụ: khi nhặt xu, trừ mạng, thay đổi điểm số, kích hoạt power-up).
  ```csharp
  public static event System.Action<int> OnScoreChanged;
  public static event System.Action<int> OnLivesChanged;
  public static event System.Action OnGameOver;
  ```
- **Singleton Pattern:**
  - Áp dụng Singleton duy nhất cho các bộ quản lý trung tâm như `GameManager.Instance` hoặc `AudioManager.Instance`.
- **Hiệu năng và Vòng lặp Update:**
  - **TUYỆT ĐỐI KHÔNG** gọi `FindObjectOfType`, `GameObject.Find`, `GetComponent` liên tục bên trong hàm `Update()`, `FixedUpdate()` hoặc `LateUpdate()`.
  - Tất cả các tham chiếu component phải được cache sẵn trong `Awake()` hoặc `Start()`, hoặc gán trực tiếp qua Inspector (`[SerializeField]`).

---

## 6. Quy Ước Tags và Layers

Để xử lý va chạm chính xác theo ma trận tương tác của GAME_SPEC.md:

### 6.1. Tags
| Tag | Đối tượng áp dụng |
| --- | --- |
| `Player` | Nhân vật chính |
| `Coin` | Xu vàng nhặt trên đường |
| `Crystal` | Mảnh tinh thể hồi mạng & cộng điểm lớn |
| `PowerUp` | Vật phẩm hỗ trợ (Khiên, Nam châm, Nhân đôi điểm) |
| `Obstacle` | Chướng ngại vật (Rào chắn, xà ngang, tảng đá lăn) |
| `Pit` | Hố sâu rơi xuống là thua |

### 6.2. Layers
| Layer | Mục đích sử dụng |
| --- | --- |
| `Player` | Layer dành riêng cho nhân vật chính |
| `Obstacle` | Layer dành riêng cho chướng ngại vật để lọc va chạm vật lý |

---

## 7. Tiêu Chuẩn Viết Code và Comment C#

- **Ngôn ngữ bình luận:** Toàn bộ comment trong code sử dụng **tiếng Việt ngắn gọn, súc tích và đúng trọng tâm**.
- Mỗi class phải có comment tóm tắt vai trò và trách nhiệm.
- Các hàm logic cốt lõi (xử lý va chạm, chuyển làn, tính điểm, sinh đường) phải có comment giải thích ngắn gọn cơ chế hoạt động.
- Áp dụng đầy đủ thuộc tính Inspector:
  - `[Header("...")]` để nhóm các thuộc tính liên quan.
  - `[Tooltip("...")]` giải thích ý nghĩa tham số trong Inspector.
  - `[SerializeField]` cho biến private cần hiển thị trong Inspector.
- Đảm bảo mã nguồn tương thích hoàn toàn với C# 9.0+ trên Unity 6 LTS, không sinh cảnh báo (warnings) và không phụ thuộc vào code chưa khai báo.

