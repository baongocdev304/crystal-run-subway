# Phân tích và thiết kế game "Endless Runner" – Chạy Đua Tinh Thể

Đồ án: Tương tác giữa các đối tượng trong môi trường ảo (Unity)

## 1. Giới thiệu game

**Tên game:** Chạy Đua Tinh Thể (Crystal Run)

**Cốt truyện:** Một nhà thám hiểm trẻ lạc vào ngôi đền cổ ẩn sâu trong rừng già. Khi anh chạm vào viên tinh thể trung tâm, cả ngôi đền bắt đầu rung chuyển và sụp đổ. Sàn đá nứt vỡ, bẫy cổ xưa bị kích hoạt, những tảng đá khổng lồ lăn xuống từ phía sau. Anh chỉ có một lối thoát là chạy thẳng về phía trước, né mọi cạm bẫy và gom những mảnh tinh thể rơi vãi trên đường để tích lũy sức mạnh. Chạy được càng xa, anh càng có cơ hội thoát khỏi ngôi đền.

**Thể loại:** Game 3D chạy vô tận (Endless Runner), góc nhìn thứ ba, phong cách đồ họa low-poly.

**Mục tiêu:** Sống sót lâu nhất có thể và đạt điểm số cao nhất.

## 2. Các quy luật chơi chính

### 2.1. Di chuyển

- Nhân vật tự động chạy về phía trước, người chơi không điều khiển được việc dừng lại.
- Đường chạy chia thành 3 làn. Người chơi chuyển làn trái hoặc phải bằng phím A/D hoặc phím mũi tên.
- Phím Space để nhảy qua vật cản thấp hoặc hố. Phím S hoặc mũi tên xuống để trượt qua vật cản treo cao.
- Tốc độ chạy tăng dần theo quãng đường (ví dụ cứ 500 m tăng 10%) đến một mức tối đa. Càng về sau, người chơi càng ít thời gian phản ứng.
- Camera bám theo nhân vật từ phía sau, hơi nghiêng xuống để quan sát đường phía trước.

### 2.2. Chướng ngại vật

- **Vật cản thấp (rào đá, hộp gỗ):** phải nhảy qua hoặc chuyển làn.
- **Vật cản treo cao (xà ngang, lưỡi rìu):** phải trượt qua.
- **Hố sâu:** phải nhảy qua, rơi xuống là thua ngay.
- **Tảng đá lăn:** di chuyển ngang qua các làn theo chu kỳ, phải canh thời điểm để vượt qua.
- Mỗi lần va chạm với chướng ngại vật, người chơi mất 1 mạng. Người chơi có 3 mạng, sau va chạm có khoảng 2 giây bất tử nhấp nháy để thoát khỏi vật cản.

### 2.3. Thu thập vật phẩm

- **Xu vàng:** xếp thành hàng hoặc vòng cung trên đường, mỗi xu cộng 10 điểm.
- **Mảnh tinh thể:** hiếm hơn, cộng 50 điểm. Gom đủ 10 mảnh sẽ hồi 1 mạng.
- **Khiên:** chặn được 1 lần va chạm, tồn tại tối đa 8 giây.
- **Nam châm:** tự hút xu trong bán kính nhất định, kéo dài 8 giây.
- **Nhân đôi điểm:** điểm nhận được gấp đôi trong 10 giây.

### 2.4. Sinh đối tượng và độ khó

- Đường chạy được ghép từ các đoạn đường có sẵn (module), mỗi đoạn chứa một kiểu bố trí chướng ngại vật và xu.
- Hệ thống sinh đường chọn ngẫu nhiên đoạn tiếp theo, đoạn đã đi qua phía sau sẽ bị xóa để tiết kiệm bộ nhớ.
- Độ khó chia theo giai đoạn:
  - Giai đoạn 1 (0 đến 500 m): chỉ có vật cản thấp, tốc độ chậm.
  - Giai đoạn 2 (500 đến 1500 m): xuất hiện hố sâu và vật cản treo cao.
  - Giai đoạn 3 (từ 1500 m): thêm tảng đá lăn, các chướng ngại vật đặt dày hơn.
- Vật phẩm hỗ trợ xuất hiện ngẫu nhiên với tần suất thấp, và bị giảm khi người chơi đang có sẵn hiệu ứng.

### 2.5. Tính điểm

- Điểm = quãng đường đã chạy + điểm từ xu và vật phẩm.
- Điểm cao nhất được lưu lại và hiển thị ở màn hình chính.

### 2.6. Điều kiện thắng và thua

- **Thua:** mất hết 3 mạng hoặc rơi xuống hố.
- **Kết thúc:** hiển thị màn hình Game Over với điểm, quãng đường, số xu thu được, kèm nút Chơi lại và Thoát.
- Game không có điểm kết thúc cố định, người chơi cạnh tranh bằng điểm số.

## 3. Bảng tương tác giữa các đối tượng

| Đối tượng A | Đối tượng B | Kiểu va chạm | Kết quả |
| --- | --- | --- | --- |
| Nhân vật | Xu vàng | Trigger | Cộng 10 điểm, xu biến mất, phát âm thanh |
| Nhân vật | Mảnh tinh thể | Trigger | Cộng 50 điểm, tăng bộ đếm hồi mạng |
| Nhân vật | Vật phẩm hỗ trợ | Trigger | Kích hoạt hiệu ứng, bắt đầu đếm ngược |
| Nhân vật | Chướng ngại vật | Collision | Trừ 1 mạng, hoặc mất khiên nếu đang có khiên |
| Nhân vật | Hố sâu | Trigger | Thua ngay |
| Nhân vật | Mặt đường | Collision | Cho phép chạy, nhảy rồi tiếp đất |
| Nam châm (hiệu ứng) | Xu vàng | Phạm vi (OverlapSphere) | Xu bay về phía nhân vật |
| Tảng đá lăn | Nhân vật | Collision (Rigidbody) | Trừ 1 mạng |
| Bộ sinh đường | Đoạn đường | Spawner | Tạo đoạn mới phía trước, xóa đoạn phía sau |
| Camera | Nhân vật | Follow | Bám theo vị trí, di chuyển mượt |

## 4. Giao diện người dùng

- **Màn hình chính:** tên game, nút Bắt đầu, nút Thoát, điểm cao nhất.
- **Màn hình chơi:** điểm hiện tại, số xu, số mạng, thanh đếm ngược thời gian hiệu ứng.
- **Màn hình tạm dừng:** nút Tiếp tục, Chơi lại, Về menu.
- **Màn hình Game Over:** thống kê kết quả và nút Chơi lại.

## 5. Công nghệ và thành phần kỹ thuật

- **Công cụ:** Unity (C#), Visual Studio Code.
- **Vật lý:** Rigidbody, Collider, Trigger để xử lý va chạm.
- **Script chính:** PlayerController, ObstacleController, CoinCollector, PowerUpManager, TrackSpawner, GameManager, UIManager.
- **Âm thanh và hiệu ứng:** nhạc nền, âm thanh nhặt xu và va chạm, hiệu ứng hạt (Particle System) khi nhặt vật phẩm.
