using System;
using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Mảnh tinh thể quý hiếm trên đường chạy:
    /// - Cộng điểm thưởng cao từ GameConfig (mặc định: 50 điểm, nhân đôi nếu có DoubleScore).
    /// - Tích lũy bộ đếm tinh thể; khi gom đủ số lượng cấu hình (mặc định: 10 mảnh), tự động gọi PlayerHealth.Heal(1) để hồi 1 mạng và đặt lại bộ đếm.
    /// - Phát các sự kiện tĩnh để UI cập nhật tiến trình thanh hồi máu.
    /// </summary>
    public class Crystal : Collectible
    {
        [Header("=== Cấu hình Game ===")]
        [Tooltip("Asset GameConfig chứa điểm tinh thể và số lượng cần để hồi mạng (bắt buộc gán trong Inspector)")]
        [SerializeField] private GameConfig config;

        #region Static Events & State

        /// <summary>Kích hoạt khi tinh thể được nhặt (tham số: số điểm nhận được)</summary>
        public static event Action<int> OnCrystalCollected;

        /// <summary>Kích hoạt khi tiến trình gom tinh thể thay đổi (tham số: số tinh thể hiện tại, số tinh thể mục tiêu)</summary>
        public static event Action<int, int> OnCrystalProgressChanged;

        /// <summary>Kích hoạt khi người chơi gom đủ 10 tinh thể và được hồi 1 mạng</summary>
        public static event Action OnCrystalHealedLife;

        // Bộ đếm tinh thể tích lũy xuyên suốt màn chơi
        private static int currentCollectedCrystals = 0;

        /// <summary>Số tinh thể hiện tại đang tích lũy</summary>
        public static int CurrentCollectedCrystals => currentCollectedCrystals;

        /// <summary>
        /// Đặt lại bộ đếm tinh thể (gọi khi bắt đầu ván mới).
        /// </summary>
        public static void ResetCrystalProgress()
        {
            currentCollectedCrystals = 0;
        }

        #endregion

        protected override void Reset()
        {
            base.Reset();
            gameObject.tag = "Crystal";
        }

        protected override void Awake()
        {
            base.Awake();
            if (!gameObject.CompareTag("Crystal"))
            {
                gameObject.tag = "Crystal";
            }
        }

        protected override void OnCollected(GameObject player)
        {
            // 1. Tính toán điểm thưởng
            int baseScore = (config != null) ? config.CrystalScore : 50;
            int multiplier = (PowerUpManager.Instance != null) ? PowerUpManager.Instance.ScoreMultiplier : 1;
            int finalScore = baseScore * multiplier;

            OnCrystalCollected?.Invoke(finalScore);

            // 2. Tích lũy tiến trình hồi mạng
            int targetCount = (config != null) ? config.CrystalsToHeal : 10;
            currentCollectedCrystals++;

            if (currentCollectedCrystals >= targetCount)
            {
                currentCollectedCrystals = 0;

                // Hồi 1 mạng sống cho nhân vật
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.Heal(1);
                }

                OnCrystalHealedLife?.Invoke();
            }

            OnCrystalProgressChanged?.Invoke(currentCollectedCrystals, targetCount);
        }

        /// <summary>
        /// Gán GameConfig từ bên ngoài.
        /// </summary>
        public void SetConfig(GameConfig newConfig)
        {
            config = newConfig;
        }
    }
}

