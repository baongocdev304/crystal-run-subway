using System;
using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Quản lý hệ thống điểm số và kỷ lục trò chơi:
    /// - Công thức tính điểm: Điểm = Quãng đường (làm tròn mét) + Điểm xu + Điểm tinh thể (có nhân hệ số từ PowerUpManager).
    /// - Theo dõi sát sao: Điểm hiện tại, số xu thu được, số tinh thể thu được, quãng đường đã chạy.
    /// - Quản lý và lưu trữ điểm cao nhất (High Score) bằng PlayerPrefs, phát hiện và lưu kỷ lục mới khi GameOver.
    /// - Bắn sự kiện OnScoreChanged, OnCoinsChanged, OnHighScoreChanged cho UI cập nhật.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        private const string HIGH_SCORE_PREFS_KEY = "CrystalRun_HighScore";

        [Header("=== Tham chiếu ===")]
        [Tooltip("Controller của nhân vật để lấy quãng đường đã chạy")]
        [SerializeField] private PlayerController playerController;

        #region Events (Sự kiện cho UI)

        /// <summary>Kích hoạt khi tổng điểm thay đổi (tham số: tổng điểm mới)</summary>
        public static event Action<int> OnScoreChanged;

        /// <summary>Kích hoạt khi số lượng xu thay đổi (tham số: số xu mới)</summary>
        public static event Action<int> OnCoinsChanged;

        /// <summary>Kích hoạt khi số lượng tinh thể thay đổi (tham số: số tinh thể mới)</summary>
        public static event Action<int> OnCrystalsChanged;

        /// <summary>Kích hoạt khi quãng đường chạy thay đổi (tham số: mét)</summary>
        public static event Action<int> OnDistanceChanged;

        /// <summary>Kích hoạt khi điểm cao kỷ lục thay đổi (tham số: điểm kỷ lục)</summary>
        public static event Action<int> OnHighScoreChanged;

        /// <summary>Kích hoạt khi người chơi phá kỷ lục điểm cao mới trong ván</summary>
        public static event Action<int> OnNewHighScoreAchieved;

        #endregion

        #region Properties

        /// <summary>Tổng điểm số hiện tại của ván chơi</summary>
        public int CurrentScore => totalScore;

        /// <summary>Tổng số xu nhặt được trong ván chơi</summary>
        public int CoinsCount => currentCoins;

        /// <summary>Tổng số tinh thể nhặt được trong ván chơi</summary>
        public int CrystalsCount => currentCrystals;

        /// <summary>Quãng đường đã chạy (mét, làm tròn)</summary>
        public int DistanceMeters => distanceScore;

        /// <summary>Điểm số cao nhất từng đạt được</summary>
        public int HighScore => highScore;

        /// <summary>Ván chơi hiện tại có đạt kỷ lục mới hay không</summary>
        public bool IsNewHighScore => isNewRecord;

        #endregion

        private int currentCoins = 0;
        private int currentCrystals = 0;
        private int itemsScore = 0;
        private int distanceScore = 0;
        private int totalScore = 0;
        private int highScore = 0;
        private bool isNewRecord = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadHighScore();
        }

        private void Start()
        {
            FindPlayerController();

            // Đăng ký nhận sự kiện nhặt xu và tinh thể
            Coin.OnCoinCollected -= HandleCoinCollected;
            Coin.OnCoinCollected += HandleCoinCollected;

            Crystal.OnCrystalCollected -= HandleCrystalCollected;
            Crystal.OnCrystalCollected += HandleCrystalCollected;

            ResetScore();
        }

        private void OnDestroy()
        {
            Coin.OnCoinCollected -= HandleCoinCollected;
            Crystal.OnCrystalCollected -= HandleCrystalCollected;
        }

        private void Update()
        {
            // Chỉ cập nhật quãng đường khi trò chơi đang ở trạng thái Playing
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                return;
            }

            UpdateDistanceScore();
        }

        private void FindPlayerController()
        {
            if (playerController == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null)
                {
                    playerController = p.GetComponent<PlayerController>();
                }
            }
        }

        /// <summary>
        /// Cập nhật điểm nhận được từ quãng đường chạy (1 mét = 1 điểm).
        /// </summary>
        private void UpdateDistanceScore()
        {
            if (playerController == null)
            {
                FindPlayerController();
                if (playerController == null) return;
            }

            int currentMeters = Mathf.FloorToInt(playerController.DistanceTravelled);
            if (currentMeters != distanceScore)
            {
                distanceScore = currentMeters;
                OnDistanceChanged?.Invoke(distanceScore);
                RecalculateTotalScore();
            }
        }

        private void HandleCoinCollected(int points)
        {
            currentCoins++;
            itemsScore += points;

            OnCoinsChanged?.Invoke(currentCoins);
            RecalculateTotalScore();
        }

        private void HandleCrystalCollected(int points)
        {
            currentCrystals++;
            itemsScore += points;

            OnCrystalsChanged?.Invoke(currentCrystals);
            RecalculateTotalScore();
        }

        /// <summary>
        /// Tính toán lại tổng điểm: Quãng đường + Điểm vật phẩm.
        /// </summary>
        private void RecalculateTotalScore()
        {
            int newTotal = distanceScore + itemsScore;
            if (newTotal != totalScore)
            {
                totalScore = newTotal;
                OnScoreChanged?.Invoke(totalScore);

                // Kiểm tra điểm kỷ lục mới
                if (totalScore > highScore)
                {
                    highScore = totalScore;
                    if (!isNewRecord)
                    {
                        isNewRecord = true;
                        OnNewHighScoreAchieved?.Invoke(highScore);
                    }
                    OnHighScoreChanged?.Invoke(highScore);
                }
            }
        }

        #region Lưu Trữ & Quản lý High Score

        private void LoadHighScore()
        {
            highScore = PlayerPrefs.GetInt(HIGH_SCORE_PREFS_KEY, 0);
            OnHighScoreChanged?.Invoke(highScore);
        }

        /// <summary>
        /// Lưu điểm cao nhất vào PlayerPrefs khi ván chơi kết thúc (Game Over).
        /// </summary>
        public void SaveHighScore()
        {
            if (totalScore > PlayerPrefs.GetInt(HIGH_SCORE_PREFS_KEY, 0))
            {
                PlayerPrefs.SetInt(HIGH_SCORE_PREFS_KEY, totalScore);
                PlayerPrefs.Save();
                highScore = totalScore;
                OnHighScoreChanged?.Invoke(highScore);
            }
        }

        /// <summary>
        /// Đặt lại các bộ đếm điểm khi bắt đầu ván mới.
        /// </summary>
        public void ResetScore()
        {
            currentCoins = 0;
            currentCrystals = 0;
            itemsScore = 0;
            distanceScore = 0;
            totalScore = 0;
            isNewRecord = false;

            LoadHighScore();

            OnCoinsChanged?.Invoke(0);
            OnCrystalsChanged?.Invoke(0);
            OnDistanceChanged?.Invoke(0);
            OnScoreChanged?.Invoke(0);
        }

        /// <summary>
        /// Cung cấp tham chiếu PlayerController từ script bên ngoài.
        /// </summary>
        public void SetPlayerController(PlayerController player)
        {
            playerController = player;
        }

        #endregion
    }
}

