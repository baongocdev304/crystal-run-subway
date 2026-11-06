using System;
using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Quản lý lượng mạng sống, khiên bảo vệ và trạng thái sinh tử của người chơi:
    /// - Tiếp nhận va chạm với chướng ngại vật (Tag: Obstacle).
    ///   + Nếu có khiên: triệt tiêu khiên, không mất mạng, kích hoạt bất tử ngắn.
    ///   + Nếu không có khiên: trừ mạng và kích hoạt thời gian bất tử theo GameConfig.
    /// - Tiếp nhận rơi xuống hố sâu (Tag: Pit): Thua ngay lập tức.
    /// - Quản lý hồi mạng (Heal) từ việc tích lũy tinh thể, giới hạn tối đa theo MaxLives.
    /// - Bắn các sự kiện (Events) để UI, AudioManager và GameManager phản hồi.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : MonoBehaviour
    {
        [Header("=== Cấu hình Game ===")]
        [Tooltip("Asset ScriptableObject chứa số mạng ban đầu, số mạng tối đa và thời gian bất tử (bắt buộc gán trong Inspector)")]
        [SerializeField] private GameConfig config;

        [Header("=== Trạng thái Hiện tại (Debug) ===")]
        [Tooltip("Số mạng hiện tại")]
        [SerializeField] private int currentLives = 3;

        [Tooltip("Có đang sở hữu khiên bảo vệ hay không")]
        [SerializeField] private bool hasShield = false;

        #region Events (Sự kiện cho UI, Âm thanh & GameManager)

        /// <summary>Kích hoạt khi nhân vật bị mất mạng (tham số: lượng sát thương)</summary>
        public event Action<int> OnDamaged;

        /// <summary>Kích hoạt khi số mạng thay đổi (tham số: số mạng mới)</summary>
        public event Action<int> OnLivesChanged;

        /// <summary>Kích hoạt khi trạng thái khiên thay đổi (tham số: true nếu có khiên, false nếu mất khiên)</summary>
        public event Action<bool> OnShieldChanged;

        /// <summary>Kích hoạt khi người chơi hết mạng hoặc rơi xuống hố (Game Over)</summary>
        public event Action OnDied;

        #endregion

        #region Properties

        /// <summary>Số mạng hiện tại của người chơi</summary>
        public int CurrentLives => currentLives;

        /// <summary>Số mạng tối đa người chơi có thể sở hữu</summary>
        public int MaxLives => (config != null) ? config.MaxLives : 5;

        /// <summary>Người chơi có đang sở hữu khiên hay không</summary>
        public bool HasShield => hasShield;

        /// <summary>Người chơi còn sống hay đã tử vong</summary>
        public bool IsAlive => isAlive;

        #endregion

        private PlayerController playerController;
        private bool isAlive = true;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
        }

        private void Start()
        {
            ResetHealth();
        }

        /// <summary>
        /// Khởi tạo lại lượng máu và trạng thái ban đầu khi bắt đầu chơi hoặc chơi lại.
        /// </summary>
        public void ResetHealth()
        {
            isAlive = true;
            hasShield = false;
            currentLives = (config != null) ? config.InitialLives : 3;

            OnLivesChanged?.Invoke(currentLives);
            OnShieldChanged?.Invoke(hasShield);
        }

        #region Xử lý Va chạm (Collisions & Triggers)

        private void OnCollisionEnter(Collision collision)
        {
            if (!isAlive) return;

            // Xử lý va chạm vật lý với chướng ngại vật (Tag: Obstacle)
            if (collision.gameObject.CompareTag("Obstacle"))
            {
                HandleObstacleHit(collision.gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isAlive) return;

            // Xử lý rơi xuống hố sâu (Tag: Pit) - Thua ngay lập tức
            if (other.CompareTag("Pit"))
            {
                HandlePitFall();
            }
            // Hỗ trợ trường hợp chướng ngại vật được đặt Collider ở dạng Trigger
            else if (other.CompareTag("Obstacle"))
            {
                HandleObstacleHit(other.gameObject);
            }
        }

        /// <summary>
        /// Xử lý khi va phải chướng ngại vật (rào đá, xà ngang, tảng đá lăn).
        /// </summary>
        private void HandleObstacleHit(GameObject obstacleObject)
        {
            // Nếu đang trong trạng thái bất tử nhấp nháy thì bỏ qua va chạm
            if (playerController != null && playerController.IsInvincible)
            {
                return;
            }

            // Trường hợp 1: Có Khiên bảo vệ -> Khiên hấp thụ toàn bộ đòn đánh
            if (hasShield)
            {
                hasShield = false;
                OnShieldChanged?.Invoke(false);

                // Kích hoạt thời gian bất tử ngắn (1.0s) để người chơi kịp thoát ra khỏi vùng va chạm của vật cản
                if (playerController != null)
                {
                    playerController.SetInvincible(1.0f);
                }

                // Sự kiện OnShieldChanged(false) đã được kích hoạt phía trên để AudioManager phát âm thanh vỡ khiên
                return;
            }

            // Trường hợp 2: Không có khiên -> Trừ mạng
            int damage = 1;
            Obstacle obstacleComponent = obstacleObject.GetComponent<Obstacle>();
            if (obstacleComponent != null)
            {
                damage = obstacleComponent.Damage;
            }

            currentLives = Mathf.Max(0, currentLives - damage);
            OnDamaged?.Invoke(damage);
            OnLivesChanged?.Invoke(currentLives);

            if (currentLives > 0)
            {
                // Vẫn còn mạng: Bật thời gian bất tử nhấp nháy từ GameConfig
                float invincibilityTime = (config != null) ? config.InvincibilityDuration : 2.0f;
                if (playerController != null)
                {
                    playerController.SetInvincible(invincibilityTime);
                }

                // Thông báo trạng thái bất tử sau khi chịu sát thương
            }
            else
            {
                // Hết mạng -> Tử vong
                Die();
            }
        }

        /// <summary>
        /// Xử lý rơi xuống hố sâu: Thua ngay lập tức mà không phụ thuộc số mạng hay khiên.
        /// </summary>
        private void HandlePitFall()
        {
            currentLives = 0;
            hasShield = false;

            OnLivesChanged?.Invoke(0);
            OnShieldChanged?.Invoke(false);

            Die();
        }

        /// <summary>
        /// Thực thi quy trình khi nhân vật tử nạn.
        /// </summary>
        private void Die()
        {
            if (!isAlive) return;

            isAlive = false;

            // Dừng điều khiển và vận tốc chạy tiến
            if (playerController != null)
            {
                playerController.isPlaying = false;
            }

            OnDied?.Invoke();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameOver();
            }
        }

        #endregion

        #region Hồi Mạng & Khiên (Public API)

        /// <summary>
        /// Hồi mạng cho người chơi (khi gom đủ 10 mảnh tinh thể), không vượt quá MaxLives.
        /// </summary>
        /// <param name="amount">Số mạng hồi (mặc định: 1)</param>
        public void Heal(int amount = 1)
        {
            if (!isAlive || amount <= 0) return;

            int max = MaxLives;
            if (currentLives < max)
            {
                currentLives = Mathf.Min(currentLives + amount, max);
                OnLivesChanged?.Invoke(currentLives);
            }
        }

        /// <summary>
        /// Kích hoạt khiên bảo vệ cho người chơi khi nhặt được Power-up Khiên.
        /// </summary>
        public void SetShield(bool active)
        {
            if (!isAlive) return;

            hasShield = active;
            OnShieldChanged?.Invoke(hasShield);
        }

        /// <summary>
        /// Gán asset GameConfig từ bên ngoài (cho script dựng cảnh Editor).
        /// </summary>
        public void SetConfig(GameConfig newConfig)
        {
            config = newConfig;
        }

        #endregion
    }
}

