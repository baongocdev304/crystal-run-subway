using System;
using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Quản lý toàn bộ vòng đời và hiệu ứng của các vật phẩm bổ trợ (Power-Up) trong CrystalRun:
    /// - Khiên (Shield): Bật cờ khiên trong PlayerHealth, hiển thị vòng bảo vệ, kết thúc khi hết giờ (8s) hoặc khi chặn 1 va chạm.
    /// - Nam châm (Magnet): Tự động hút xu trong bán kính GameConfig bằng Physics.OverlapSphereNonAlloc (zero GC) mỗi FixedUpdate (8s).
    /// - Nhân đôi điểm (DoubleScore): Nhân đôi điểm nhận được từ xu và tinh thể trong 10s.
    /// - Quản lý thời gian đếm ngược độc lập cho từng hiệu ứng; nhặt lại cùng loại sẽ làm mới thời gian (không cộng dồn).
    /// - Bắn sự kiện OnPowerUpStarted, OnPowerUpTick, OnPowerUpEnded để UI hiển thị thanh đếm ngược thời gian thực.
    /// - Cung cấp hàm HasAnyActivePowerUp() để hệ thống sinh đường giảm tần suất xuất hiện item hỗ trợ khi người chơi đang có buff.
    /// </summary>
    public class PowerUpManager : MonoBehaviour
    {
        public static PowerUpManager Instance { get; private set; }

        [Header("=== Cấu hình Game ===")]
        [Tooltip("Asset GameConfig chứa thời gian hiệu lực và bán kính nam châm (bắt buộc gán trong Inspector)")]
        [SerializeField] private GameConfig config;

        [Header("=== Tham chiếu Đối tượng ===")]
        [Tooltip("Transform của nhân vật chính để làm tâm hút nam châm (tự tìm nếu để trống)")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("PlayerHealth của nhân vật để liên kết trạng thái khiên (tự tìm nếu để trống)")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("=== Hiệu ứng Hình ảnh Khiên ===")]
        [Tooltip("GameObject hiển thị vòng cầu hào quang bảo vệ bao quanh nhân vật khi đang có khiên")]
        [SerializeField] private GameObject shieldVisual;

        [Header("=== Cài đặt Nam châm (Magnet) ===")]
        [Tooltip("LayerMask lọc vật thể xu vàng để hút")]
        [SerializeField] private LayerMask coinLayerMask = ~0;

        [Tooltip("Kích thước tối đa bộ đệm phát hiện xu mỗi frame (chống cấp phát rác GC)")]
        [SerializeField] private int maxMagnetHits = 32;

        #region Events (Sự kiện cho UI và Âm thanh)

        /// <summary>Kích hoạt khi một hiệu ứng bắt đầu (tham số: loại hiệu ứng, tổng thời lượng giây)</summary>
        public static event Action<PowerUpType, float> OnPowerUpStarted;

        /// <summary>Kích hoạt mỗi frame đếm ngược thời gian còn lại (tham số: loại hiệu ứng, thời gian còn lại)</summary>
        public static event Action<PowerUpType, float> OnPowerUpTick;

        /// <summary>Kích hoạt khi một hiệu ứng kết thúc</summary>
        public static event Action<PowerUpType> OnPowerUpEnded;

        #endregion

        #region Properties

        /// <summary>Hệ số nhân điểm hiện tại (bằng 2 nếu DoubleScore đang hoạt động, ngược lại bằng 1)</summary>
        public int ScoreMultiplier => (doubleScoreTimer > 0f) ? 2 : 1;

        /// <summary>Trạng thái hiệu ứng Khiên</summary>
        public bool IsShieldActive => shieldTimer > 0f;

        /// <summary>Trạng thái hiệu ứng Nam châm</summary>
        public bool IsMagnetActive => magnetTimer > 0f;

        /// <summary>Trạng thái hiệu ứng Nhân đôi điểm</summary>
        public bool IsDoubleScoreActive => doubleScoreTimer > 0f;

        #endregion

        // Bộ đếm thời gian còn lại (giây) của từng hiệu ứng
        private float shieldTimer = 0f;
        private float magnetTimer = 0f;
        private float doubleScoreTimer = 0f;

        // Bộ đệm tĩnh tránh cấp phát rác GC cho Physics.OverlapSphereNonAlloc
        private Collider[] coinHitBuffer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (maxMagnetHits <= 0) maxMagnetHits = 32;
            coinHitBuffer = new Collider[maxMagnetHits];
        }

        private void Start()
        {
            FindPlayerReferences();

            // Ẩn vòng khiên ban đầu
            UpdateShieldVisual(false);
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.OnShieldChanged -= HandlePlayerShieldChanged;
            }
        }

        /// <summary>
        /// Tìm kiếm Transform và PlayerHealth của nhân vật nếu chưa được gán trong Inspector.
        /// </summary>
        private void FindPlayerReferences()
        {
            if (playerTransform == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    playerTransform = playerObj.transform;
                }
            }

            if (playerHealth == null && playerTransform != null)
            {
                playerHealth = playerTransform.GetComponent<PlayerHealth>();
            }

            if (playerHealth != null)
            {
                playerHealth.OnShieldChanged -= HandlePlayerShieldChanged;
                playerHealth.OnShieldChanged += HandlePlayerShieldChanged;
            }

            // Nếu chưa gán shieldVisual, thử tìm trong các đối tượng con của nhân vật
            if (shieldVisual == null && playerTransform != null)
            {
                Transform foundShield = playerTransform.Find("ShieldVisual");
                if (foundShield != null)
                {
                    shieldVisual = foundShield.gameObject;
                }
            }
        }

        private void Update()
        {
            UpdateTimers();
        }

        private void FixedUpdate()
        {
            // Xử lý hút xu vàng khi Nam châm đang hoạt động
            if (magnetTimer > 0f)
            {
                ProcessMagnetEffect();
            }
        }

        #region Xử lý Kích hoạt & Đếm ngược

        /// <summary>
        /// Kích hoạt một hiệu ứng hỗ trợ. Nhặt lại cùng loại sẽ làm mới lại thời gian, không cộng dồn.
        /// </summary>
        public void ActivatePowerUp(PowerUpType type)
        {
            EnsurePlayerReferences();

            float duration = 8.0f;

            switch (type)
            {
                case PowerUpType.Shield:
                    duration = (config != null) ? config.ShieldDuration : 8.0f;
                    shieldTimer = duration;
                    if (playerHealth != null)
                    {
                        playerHealth.SetShield(true);
                    }
                    UpdateShieldVisual(true);
                    break;

                case PowerUpType.Magnet:
                    duration = (config != null) ? config.MagnetDuration : 8.0f;
                    magnetTimer = duration;
                    break;

                case PowerUpType.DoubleScore:
                    duration = (config != null) ? config.DoubleScoreDuration : 10.0f;
                    doubleScoreTimer = duration;
                    break;
            }

            OnPowerUpStarted?.Invoke(type, duration);
        }

        /// <summary>
        /// Cập nhật thời gian còn lại của các buff đang hoạt động.
        /// </summary>
        private void UpdateTimers()
        {
            // 1. Khiên
            if (shieldTimer > 0f)
            {
                shieldTimer -= Time.deltaTime;
                OnPowerUpTick?.Invoke(PowerUpType.Shield, Mathf.Max(0f, shieldTimer));

                if (shieldTimer <= 0f)
                {
                    EndShield();
                }
            }

            // 2. Nam châm
            if (magnetTimer > 0f)
            {
                magnetTimer -= Time.deltaTime;
                OnPowerUpTick?.Invoke(PowerUpType.Magnet, Mathf.Max(0f, magnetTimer));

                if (magnetTimer <= 0f)
                {
                    EndMagnet();
                }
            }

            // 3. Nhân đôi điểm
            if (doubleScoreTimer > 0f)
            {
                doubleScoreTimer -= Time.deltaTime;
                OnPowerUpTick?.Invoke(PowerUpType.DoubleScore, Mathf.Max(0f, doubleScoreTimer));

                if (doubleScoreTimer <= 0f)
                {
                    EndDoubleScore();
                }
            }
        }

        private void EndShield()
        {
            shieldTimer = 0f;
            if (playerHealth != null)
            {
                playerHealth.SetShield(false);
            }
            UpdateShieldVisual(false);
            OnPowerUpEnded?.Invoke(PowerUpType.Shield);
        }

        private void EndMagnet()
        {
            magnetTimer = 0f;
            OnPowerUpEnded?.Invoke(PowerUpType.Magnet);
        }

        private void EndDoubleScore()
        {
            doubleScoreTimer = 0f;
            OnPowerUpEnded?.Invoke(PowerUpType.DoubleScore);
        }

        /// <summary>
        /// Lắng nghe sự kiện từ PlayerHealth: nếu khiên bị vỡ sớm do va chạm, hủy ngay timer khiên.
        /// </summary>
        private void HandlePlayerShieldChanged(bool hasShield)
        {
            if (!hasShield && shieldTimer > 0f)
            {
                shieldTimer = 0f;
                UpdateShieldVisual(false);
                OnPowerUpEnded?.Invoke(PowerUpType.Shield);
            }
        }

        private void UpdateShieldVisual(bool active)
        {
            if (shieldVisual != null)
            {
                shieldVisual.SetActive(active);
            }
        }

        #endregion

        #region Cơ chế Nam châm (Magnet)

        /// <summary>
        /// Sử dụng OverlapSphereNonAlloc để quét và kéo tất cả xu vàng trong bán kính về nhân vật (Zero GC).
        /// </summary>
        private void ProcessMagnetEffect()
        {
            if (playerTransform == null)
            {
                FindPlayerReferences();
                if (playerTransform == null) return;
            }

            if (coinHitBuffer == null || coinHitBuffer.Length == 0)
            {
                if (maxMagnetHits <= 0) maxMagnetHits = 32;
                coinHitBuffer = new Collider[maxMagnetHits];
            }

            float radius = (config != null) ? config.MagnetRadius : 7.0f;
            float pullSpeed = (config != null) ? config.MagnetPullSpeed : 18.0f;

            int hitCount = Physics.OverlapSphereNonAlloc(
                playerTransform.position,
                radius,
                coinHitBuffer,
                coinLayerMask,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = coinHitBuffer[i];
                if (hit != null && hit.CompareTag("Coin"))
                {
                    Coin coin = hit.GetComponent<Coin>();
                    if (coin != null && coin.gameObject.activeInHierarchy)
                    {
                        coin.PullTowards(playerTransform, pullSpeed);
                    }
                }
                // Dọn dẹp con trỏ buffer
                coinHitBuffer[i] = null;
            }
        }

        #endregion

        #region Public Helpers

        /// <summary>
        /// Kiểm tra xem người chơi hiện tại có đang sở hữu bất kỳ hiệu ứng buff nào hay không.
        /// Dùng để giảm tần suất xuất hiện của các item hỗ trợ trong TrackSegment/TrackSpawner.
        /// </summary>
        public bool HasAnyActivePowerUp()
        {
            return shieldTimer > 0f || magnetTimer > 0f || doubleScoreTimer > 0f;
        }

        /// <summary>
        /// Phương thức static kiểm tra nhanh xem người chơi có buff nào đang hoạt động hay không.
        /// </summary>
        public static bool HasActivePowerUp()
        {
            return Instance != null && Instance.HasAnyActivePowerUp();
        }

        /// <summary>
        /// Lấy thời gian còn lại của một loại Power-Up cụ thể.
        /// </summary>
        public float GetRemainingTime(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield: return Mathf.Max(0f, shieldTimer);
                case PowerUpType.Magnet: return Mathf.Max(0f, magnetTimer);
                case PowerUpType.DoubleScore: return Mathf.Max(0f, doubleScoreTimer);
                default: return 0f;
            }
        }

        /// <summary>
        /// Hủy bỏ toàn bộ hiệu ứng đang hoạt động khi bắt đầu ván mới hoặc kết thúc trò chơi.
        /// </summary>
        public void ResetAllPowerUps()
        {
            if (shieldTimer > 0f) EndShield();
            if (magnetTimer > 0f) EndMagnet();
            if (doubleScoreTimer > 0f) EndDoubleScore();

            shieldTimer = 0f;
            magnetTimer = 0f;
            doubleScoreTimer = 0f;

            UpdateShieldVisual(false);
        }

        private void EnsurePlayerReferences()
        {
            if (playerTransform == null || playerHealth == null)
            {
                FindPlayerReferences();
            }
        }

        /// <summary>
        /// Thiết lập tham chiếu visual khiên và player từ script dựng cảnh Editor.
        /// </summary>
        public void SetReferences(Transform player, PlayerHealth health, GameObject shieldObj, GameConfig newConfig)
        {
            playerTransform = player;
            playerHealth = health;
            shieldVisual = shieldObj;
            config = newConfig;

            if (playerHealth != null)
            {
                playerHealth.OnShieldChanged -= HandlePlayerShieldChanged;
                playerHealth.OnShieldChanged += HandlePlayerShieldChanged;
            }
        }

        #endregion
    }
}

