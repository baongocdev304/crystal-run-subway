using System;
using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Xu vàng nhặt trên đường chạy:
    /// - Cộng điểm thưởng cơ bản từ GameConfig (mặc định: 10 điểm).
    /// - Tự động nhân đôi điểm nếu hiệu ứng DoubleScore đang hoạt động.
    /// - Hỗ trợ bay về phía người chơi khi hiệu ứng Nam châm (Magnet) đang kích hoạt.
    /// - Phát sự kiện OnCoinCollected để UI và GameManager cập nhật.
    /// </summary>
    public class Coin : Collectible
    {
        [Header("=== Cấu hình Game ===")]
        [Tooltip("Asset GameConfig chứa giá trị điểm thưởng mỗi xu (mặc định: 10)")]
        [SerializeField] private GameConfig config;

        #region Static Events

        /// <summary>Kích hoạt khi bất kỳ xu nào được người chơi thu thập (tham số: số điểm nhận được)</summary>
        public static event Action<int> OnCoinCollected;

        #endregion

        // Biến phục vụ hiệu ứng hút của nam châm
        private Transform magnetTarget;
        private float magnetPullSpeed = 18.0f;
        private bool isBeingPulled = false;

        protected override void Reset()
        {
            base.Reset();
            gameObject.tag = "Coin";
        }

        protected override void Awake()
        {
            base.Awake();
            if (!gameObject.CompareTag("Coin"))
            {
                gameObject.tag = "Coin";
            }
        }

        protected override void Update()
        {
            if (isCollected) return;

            // Nếu đang bị nam châm hút, bay thẳng về phía nhân vật
            if (isBeingPulled && magnetTarget != null)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    magnetTarget.position + Vector3.up * 0.9f,
                    magnetPullSpeed * Time.deltaTime
                );

                // Tự xoay nhanh hơn khi đang bị hút
                transform.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);

                // Khi đã bay rất gần nhân vật (dưới 0.5m), tự động kích hoạt thu thập
                if (Vector3.Distance(transform.position, magnetTarget.position + Vector3.up * 0.9f) < 0.6f)
                {
                    isCollected = true;
                    OnCollected(magnetTarget.gameObject);
                    gameObject.SetActive(false);
                }
                return;
            }

            // Chuyển động xoay và nhấp nhô bình thường
            base.Update();
        }

        /// <summary>
        /// Kích hoạt cơ chế hút xu bay về phía nhân vật từ PowerUpManager.
        /// </summary>
        /// <param name="target">Transform của nhân vật</param>
        /// <param name="speed">Tốc độ bay (m/s)</param>
        public void PullTowards(Transform target, float speed)
        {
            if (isCollected) return;

            magnetTarget = target;
            magnetPullSpeed = speed;
            isBeingPulled = true;
        }

        protected override void OnCollected(GameObject player)
        {
            int baseScore = (config != null) ? config.CoinScore : 10;
            int multiplier = (PowerUpManager.Instance != null) ? PowerUpManager.Instance.ScoreMultiplier : 1;
            int finalScore = baseScore * multiplier;

            OnCoinCollected?.Invoke(finalScore);
        }

        public override void ResetItem()
        {
            isBeingPulled = false;
            magnetTarget = null;
            base.ResetItem();
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

