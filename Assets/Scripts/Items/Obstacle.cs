using UnityEngine;

namespace CrystalRun
{
    /*
     * =========================================================================================
     * THÔNG SỐ KÍCH THƯỚC ĐỀ XUẤT CHO BƯỚC DỰNG PREFAB CHƯỚNG NGẠI VẬT:
     * 
     * 1. Vật cản thấp (Low Obstacle - Rào đá, thùng gỗ):
     *    - Kích thước: Rộng 2.2m (phủ 1 làn) hoặc 4.5m (phủ 2 làn). Cao: 0.8m - 1.0m. Sâu: 0.6m.
     *    - Chiều cao tối đa <= 1.0m (đỉnh cú nhảy nhân vật đạt khoảng 1.5m - 1.8m với jumpForce=8.5).
     *    - Cách vượt qua: Nhảy qua (Space/Up) hoặc chuyển làn né tránh (A/D).
     * 
     * 2. Vật cản treo cao (Overhead Obstacle - Xà ngang cổ, cành cây, lưỡi chém treo):
     *    - Kích thước: Rộng 2.5m - 7.5m (vắt ngang qua các làn).
     *    - Khoảng hở gầm (Clearance từ mặt sàn đến đáy vật cản): 0.9m - 1.1m.
     *    - Chiều cao đỉnh vật cản: 2.2m - 2.5m.
     *    - Khi đứng chạy: Chiều cao nhân vật là 1.8m -> va chạm với vật cản.
     *    - Khi trượt (Slide): Chiều cao collider thu nhỏ còn 0.6m -> chui lọt qua gầm 1.0m an toàn.
     * 
     * 3. Hố sâu (Pit - Khoảng sụp lún của sàn đền):
     *    - Kích thước: Rộng 7.5m (phủ cả 3 làn), Dài dọc trục Z: 4.0m - 6.0m, Sâu: -5.0m.
     *    - Đặt BoxCollider (IsTrigger = true) với Tag = "Pit" ở độ sâu khoảng Y = -1.5m.
     *    - Rơi xuống là thua ngay lập tức (không phụ thuộc vào khiên hay số mạng).
     * 
     * 4. Tảng đá lăn (Rolling Boulder - Quả cầu đá ngàn năm):
     *    - Đường kính: 2.0m - 2.4m (Bán kính 1.0m - 1.2m).
     *    - Di chuyển ngang qua lại giữa làn Trái (X = -2.5m) và làn Phải (X = +2.5m).
     *    - Cách vượt qua: Chờ thời điểm tảng đá lăn sang làn khác hoặc căn nhịp né tránh.
     * =========================================================================================
     */

    /// <summary>
    /// Các loại chướng ngại vật trong CrystalRun.
    /// </summary>
    public enum ObstacleType
    {
        /// <summary>Vật cản thấp (rào chắn đường sắt Đỏ-Trắng thấp) - Nhảy qua</summary>
        Low,

        /// <summary>Vật cản treo cao (rào chắn cao hở gầm) - Trượt/lăn qua gầm</summary>
        Overhead,

        /// <summary>Hố sâu sụp đường ray - Nhảy qua, rơi xuống thua ngay</summary>
        Pit,

        /// <summary>Tảng đá / vật cản di động ngang qua lại giữa các làn</summary>
        RollingBoulder,

        /// <summary>Toa tàu điện đứng yên (có thể leo dốc hoặc nhảy lên nóc tàu)</summary>
        Train,

        /// <summary>Đoàn tàu điện đang chạy ngược chiều về phía người chơi</summary>
        MovingTrain
    }

    /// <summary>
    /// Gắn trên prefab của mọi chướng ngại vật.
    /// Định danh phân loại chướng ngại vật và lượng sát thương gây ra khi va chạm với người chơi.
    /// </summary>
    [DisallowMultipleComponent]
    public class Obstacle : MonoBehaviour
    {
        [Header("=== Phân loại Chướng ngại vật ===")]
        [Tooltip("Loại chướng ngại vật")]
        [SerializeField] private ObstacleType obstacleType = ObstacleType.Low;

        [Header("=== Thông số Sát thương ===")]
        [Tooltip("Lượng mạng trừ đi khi người chơi va chạm (mặc định: 1)")]
        [SerializeField] private int damage = 1;

        #region Properties

        /// <summary>Loại chướng ngại vật</summary>
        public ObstacleType Type => obstacleType;

        /// <summary>Sát thương gây ra cho nhân vật</summary>
        public int Damage => damage;

        #endregion

        private void Reset()
        {
            // Tự động gán Tag chuẩn theo quy ước dự án
            if (obstacleType == ObstacleType.Pit)
            {
                gameObject.tag = "Pit";
            }
            else
            {
                gameObject.tag = "Obstacle";
            }
        }

        /// <summary>
        /// Thiết lập cấu hình chướng ngại vật nhanh từ script dựng cảnh.
        /// </summary>
        public void Configure(ObstacleType type, int dmg = 1)
        {
            obstacleType = type;
            damage = dmg;

            gameObject.tag = (obstacleType == ObstacleType.Pit) ? "Pit" : "Obstacle";
        }
    }
}

