using System;
using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Các loại vật phẩm bổ trợ (Power-Up) trong CrystalRun.
    /// </summary>
    public enum PowerUpType
    {
        /// <summary>Khiên bảo vệ - Chặn 1 lần va chạm, tồn tại tối đa 8s</summary>
        Shield,

        /// <summary>Nam châm - Tự động hút xu vàng trong bán kính 7m, kéo dài 8s</summary>
        Magnet,

        /// <summary>Nhân đôi điểm - Tăng gấp đôi điểm nhận được từ xu và tinh thể trong 10s</summary>
        DoubleScore
    }

    /// <summary>
    /// Đại diện cho một vật phẩm bổ trợ đặt trên đường chạy:
    /// - Khi nhặt, kích hoạt hiệu ứng tương ứng trong PowerUpManager.
    /// - Gắn Tag chuẩn: "PowerUp".
    /// </summary>
    public class PowerUp : Collectible
    {
        [Header("=== Cấu hình Loại Vật Phẩm ===")]
        [Tooltip("Loại hiệu ứng hỗ trợ của vật phẩm này")]
        [SerializeField] private PowerUpType powerUpType = PowerUpType.Shield;

        #region Static Events

        /// <summary>Kích hoạt khi người chơi thu thập bất kỳ Power-Up nào</summary>
        public static event Action<PowerUpType> OnPowerUpCollected;

        #endregion

        #region Properties

        /// <summary>Loại Power-Up của đối tượng này</summary>
        public PowerUpType Type => powerUpType;

        #endregion

        protected override void Reset()
        {
            base.Reset();
            gameObject.tag = "PowerUp";
        }

        protected override void Awake()
        {
            base.Awake();
            if (!gameObject.CompareTag("PowerUp"))
            {
                gameObject.tag = "PowerUp";
            }
        }

        protected override void OnCollected(GameObject player)
        {
            if (PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.ActivatePowerUp(powerUpType);
            }

            OnPowerUpCollected?.Invoke(powerUpType);
        }

        /// <summary>
        /// Thiết lập loại Power-Up nhanh từ script dựng cảnh Editor.
        /// </summary>
        public void SetType(PowerUpType type)
        {
            powerUpType = type;
        }
    }
}

