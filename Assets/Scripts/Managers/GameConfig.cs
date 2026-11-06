using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// ScriptableObject chứa toàn bộ tham số cân bằng trò chơi CrystalRun (Chạy Đua Tinh Thể).
    /// Giúp tinh chỉnh các giá trị tốc độ, điểm số, thời gian hiệu ứng và mốc độ khó tại một nơi duy nhất.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "CrystalRun/GameConfig", order = 0)]
    public class GameConfig : ScriptableObject
    {
        [Header("--- Di chuyển & Tốc độ ---")]
        [Tooltip("Tốc độ chạy về phía trước ban đầu (m/s)")]
        [SerializeField] private float initialSpeed = 10.0f;

        [Tooltip("Tốc độ chạy tối đa có thể đạt được (m/s)")]
        [SerializeField] private float maxSpeed = 25.0f;

        [Tooltip("Khoảng cách (m) mỗi lần tăng tốc")]
        [SerializeField] private float speedIncreaseDistanceInterval = 500.0f;

        [Tooltip("Mức tăng tốc cộng thêm sau mỗi mốc khoảng cách (m/s)")]
        [SerializeField] private float speedIncreaseAmount = 1.0f;

        [Tooltip("Khoảng cách giữa các làn chạy (tính từ làn giữa x=0 sang 2 bên)")]
        [SerializeField] private float laneDistance = 2.5f;

        [Tooltip("Tốc độ chuyển đổi giữa các làn chạy")]
        [SerializeField] private float laneChangeSpeed = 15.0f;

        [Header("--- Nhảy & Trọng lực ---")]
        [Tooltip("Lực nhảy ban đầu của nhân vật")]
        [SerializeField] private float jumpForce = 8.5f;

        [Tooltip("Trọng lực tùy chỉnh áp dụng lên nhân vật khi ở trên không")]
        [SerializeField] private float gravity = -24.0f;

        [Header("--- Trượt ---")]
        [Tooltip("Thời gian duy trì trạng thái trượt (giây)")]
        [SerializeField] private float slideDuration = 0.8f;

        [Tooltip("Chiều cao collider nhân vật khi đang trượt")]
        [SerializeField] private float slideColliderHeight = 0.6f;

        [Tooltip("Chiều cao collider nhân vật ở trạng thái bình thường")]
        [SerializeField] private float normalColliderHeight = 1.8f;

        [Header("--- Mạng & Bất tử ---")]
        [Tooltip("Số mạng khởi điểm của người chơi")]
        [SerializeField] private int initialLives = 3;

        [Tooltip("Số mạng tối đa người chơi có thể sở hữu")]
        [SerializeField] private int maxLives = 5;

        [Tooltip("Thời gian bất tử sau khi va chạm với chướng ngại vật (giây)")]
        [SerializeField] private float invincibilityDuration = 2.0f;

        [Tooltip("Tần số nhấp nháy mô hình nhân vật trong trạng thái bất tử (giây)")]
        [SerializeField] private float invincibilityBlinkRate = 0.1f;

        [Header("--- Điểm số & Thu thập ---")]
        [Tooltip("Số điểm nhận được khi nhặt mỗi xu vàng")]
        [SerializeField] private int coinScore = 10;

        [Tooltip("Số điểm nhận được khi nhặt mỗi mảnh tinh thể")]
        [SerializeField] private int crystalScore = 50;

        [Tooltip("Số mảnh tinh thể cần tích lũy để hồi 1 mạng")]
        [SerializeField] private int crystalsToHeal = 10;

        [Header("--- Hiệu ứng Power-Up ---")]
        [Tooltip("Thời gian tồn tại của Khiên bảo vệ (giây)")]
        [SerializeField] private float shieldDuration = 8.0f;

        [Tooltip("Thời gian tồn tại của Nam châm hút xu (giây)")]
        [SerializeField] private float magnetDuration = 8.0f;

        [Tooltip("Thời gian tồn tại của hiệu ứng Nhân đôi điểm (giây)")]
        [SerializeField] private float doubleScoreDuration = 10.0f;

        [Tooltip("Bán kính hút xu vàng của Nam châm (mét)")]
        [SerializeField] private float magnetRadius = 7.0f;

        [Tooltip("Tốc độ bay của xu vàng về phía nhân vật khi bị nam châm hút (m/s)")]
        [SerializeField] private float magnetPullSpeed = 18.0f;

        [Header("--- Mốc độ khó (Giai đoạn) ---")]
        [Tooltip("Quãng đường bắt đầu Giai đoạn 2 (m) - Xuất hiện hố sâu và vật cản treo cao")]
        [SerializeField] private float stage2DistanceThreshold = 500.0f;

        [Tooltip("Quãng đường bắt đầu Giai đoạn 3 (m) - Xuất hiện tảng đá lăn và chướng ngại vật dày đặc")]
        [SerializeField] private float stage3DistanceThreshold = 1500.0f;

        #region Public Properties (Getters)

        // Di chuyển & Tốc độ
        public float InitialSpeed => initialSpeed;
        public float MaxSpeed => maxSpeed;
        public float SpeedIncreaseDistanceInterval => speedIncreaseDistanceInterval;
        public float SpeedIncreaseAmount => speedIncreaseAmount;
        public float LaneDistance => laneDistance;
        public float LaneChangeSpeed => laneChangeSpeed;

        // Nhảy & Trọng lực
        public float JumpForce => jumpForce;
        public float Gravity => gravity;

        // Trượt
        public float SlideDuration => slideDuration;
        public float SlideColliderHeight => slideColliderHeight;
        public float NormalColliderHeight => normalColliderHeight;

        // Mạng & Bất tử
        public int InitialLives => initialLives;
        public int MaxLives => maxLives;
        public float InvincibilityDuration => invincibilityDuration;
        public float InvincibilityBlinkRate => invincibilityBlinkRate;

        // Điểm số & Thu thập
        public int CoinScore => coinScore;
        public int CrystalScore => crystalScore;
        public int CrystalsToHeal => crystalsToHeal;

        // Hiệu ứng Power-Up
        public float ShieldDuration => shieldDuration;
        public float MagnetDuration => magnetDuration;
        public float DoubleScoreDuration => doubleScoreDuration;
        public float MagnetRadius => magnetRadius;
        public float MagnetPullSpeed => magnetPullSpeed;

        // Mốc độ khó
        public float Stage2DistanceThreshold => stage2DistanceThreshold;
        public float Stage3DistanceThreshold => stage3DistanceThreshold;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Tính toán tốc độ chạy hiện tại dựa trên quãng đường đã đi qua (mét).
        /// Cứ mỗi speedIncreaseDistanceInterval mét (500m), tốc độ tăng thêm speedIncreaseAmount.
        /// </summary>
        /// <param name="distanceTravelled">Quãng đường đã chạy (mét)</param>
        /// <returns>Tốc độ chạy được giới hạn tối đa bởi MaxSpeed</returns>
        public float CalculateSpeed(float distanceTravelled)
        {
            if (distanceTravelled <= 0f) return initialSpeed;

            int steps = Mathf.FloorToInt(distanceTravelled / speedIncreaseDistanceInterval);
            float calculatedSpeed = initialSpeed + (steps * speedIncreaseAmount);
            return Mathf.Min(calculatedSpeed, maxSpeed);
        }

        /// <summary>
        /// Xác định giai đoạn chơi hiện tại dựa trên quãng đường đã đi.
        /// 1: 0 - 500m (Giai đoạn cơ bản)
        /// 2: 500m - 1500m (Xuất hiện hố sâu và vật cản treo)
        /// 3: 1500m+ (Độ khó cao, tảng đá lăn)
        /// </summary>
        public int GetCurrentStage(float distanceTravelled)
        {
            if (distanceTravelled >= stage3DistanceThreshold)
            {
                return 3;
            }
            if (distanceTravelled >= stage2DistanceThreshold)
            {
                return 2;
            }
            return 1;
        }

        #endregion
    }
}

