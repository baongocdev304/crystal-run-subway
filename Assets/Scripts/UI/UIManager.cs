using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrystalRun.UI
{
    /// <summary>
    /// Điều khiển toàn bộ giao diện người dùng (UI) trong CrystalRun:
    /// - Quản lý 4 Panel chính: Màn hình chính (Main Menu), HUD trong game, Tạm dừng (Pause), và Kết thúc (Game Over).
    /// - Tự động đồng bộ với các sự kiện từ GameManager, ScoreManager, PlayerHealth và PowerUpManager.
    /// - Sử dụng hoàn toàn TextMeshPro (TMP_Text) theo quy ước dự án.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("=== Các Màn hình (Panels) ===")]
        [Tooltip("Panel màn hình chính ban đầu")]
        [SerializeField] private GameObject mainMenuPanel;

        [Tooltip("Panel thông số khi đang chơi game")]
        [SerializeField] private GameObject hudPanel;

        [Tooltip("Panel khi game bị tạm dừng")]
        [SerializeField] private GameObject pausePanel;

        [Tooltip("Panel khi người chơi tử vong / kết thúc game")]
        [SerializeField] private GameObject gameOverPanel;

        [Header("=== Màn hình Chính (Main Menu) ===")]
        [SerializeField] private TMP_Text menuHighScoreText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;

        [Header("=== HUD Trong Game ===")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text livesText;
        [SerializeField] private TMP_Text crystalsText;
        [SerializeField] private Button pauseButton;

        [Header("=== Thanh Hiển Thị Power-Up (HUD) ===")]
        [SerializeField] private GameObject shieldRoot;
        [SerializeField] private TMP_Text shieldTimerText;

        [SerializeField] private GameObject magnetRoot;
        [SerializeField] private TMP_Text magnetTimerText;

        [SerializeField] private GameObject doubleScoreRoot;
        [SerializeField] private TMP_Text doubleScoreTimerText;

        [Header("=== Màn hình Tạm dừng (Pause) ===")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseRestartButton;
        [SerializeField] private Button pauseQuitButton;

        [Header("=== Màn hình Kết thúc (Game Over) ===")]
        [SerializeField] private TMP_Text finalScoreText;
        [SerializeField] private TMP_Text finalDistanceText;
        [SerializeField] private TMP_Text finalCoinsText;
        [SerializeField] private TMP_Text gameOverHighScoreText;
        [SerializeField] private GameObject newRecordNotice;
        [SerializeField] private Button gameOverRestartButton;
        [SerializeField] private Button gameOverQuitButton;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.matchWidthOrHeight = 0.5f;
            }

            SetupButtonListeners();
        }

        private void Start()
        {
            SubscribeEvents();

            // Khởi tạo trạng thái ban đầu của các thanh đếm ngược power-up
            if (shieldRoot != null) shieldRoot.SetActive(false);
            if (magnetRoot != null) magnetRoot.SetActive(false);
            if (doubleScoreRoot != null) doubleScoreRoot.SetActive(false);

            // Đồng bộ giao diện với trạng thái hiện tại của GameManager
            if (GameManager.Instance != null)
            {
                UpdatePanelVisibility(GameManager.Instance.State);
            }
            else
            {
                UpdatePanelVisibility(GameState.Playing);
            }

            // Đồng bộ điểm cao ban đầu
            if (ScoreManager.Instance != null)
            {
                UpdateHighScore(ScoreManager.Instance.HighScore);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void SetupButtonListeners()
        {
            // Main Menu
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

            // HUD
            if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseClicked);

            // Pause
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (pauseRestartButton != null) pauseRestartButton.onClick.AddListener(OnRestartClicked);
            if (pauseQuitButton != null) pauseQuitButton.onClick.AddListener(OnQuitClicked);

            // Game Over
            if (gameOverRestartButton != null) gameOverRestartButton.onClick.AddListener(OnRestartClicked);
            if (gameOverQuitButton != null) gameOverQuitButton.onClick.AddListener(OnQuitClicked);
        }

        private void SubscribeEvents()
        {
            GameManager.OnStateChanged += HandleGameStateChanged;

            ScoreManager.OnScoreChanged += HandleScoreChanged;
            ScoreManager.OnDistanceChanged += HandleDistanceChanged;
            ScoreManager.OnCoinsChanged += HandleCoinsChanged;
            ScoreManager.OnHighScoreChanged += UpdateHighScore;

            Crystal.OnCrystalProgressChanged += HandleCrystalProgressChanged;

            PlayerHealth playerHealth = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnLivesChanged += HandleLivesChanged;
                HandleLivesChanged(playerHealth.CurrentLives);
            }

            PowerUpManager.OnPowerUpStarted += HandlePowerUpStarted;
            PowerUpManager.OnPowerUpTick += HandlePowerUpTick;
            PowerUpManager.OnPowerUpEnded += HandlePowerUpEnded;
        }

        private void UnsubscribeEvents()
        {
            GameManager.OnStateChanged -= HandleGameStateChanged;

            ScoreManager.OnScoreChanged -= HandleScoreChanged;
            ScoreManager.OnDistanceChanged -= HandleDistanceChanged;
            ScoreManager.OnCoinsChanged -= HandleCoinsChanged;
            ScoreManager.OnHighScoreChanged -= UpdateHighScore;

            Crystal.OnCrystalProgressChanged -= HandleCrystalProgressChanged;

            PlayerHealth playerHealth = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnLivesChanged -= HandleLivesChanged;
            }

            PowerUpManager.OnPowerUpStarted -= HandlePowerUpStarted;
            PowerUpManager.OnPowerUpTick -= HandlePowerUpTick;
            PowerUpManager.OnPowerUpEnded -= HandlePowerUpEnded;
        }

        #region Xử lý Chuyển đổi Panel

        private void HandleGameStateChanged(GameState state)
        {
            UpdatePanelVisibility(state);

            if (state == GameState.GameOver)
            {
                PopulateGameOverStats();
            }
        }

        private void UpdatePanelVisibility(GameState state)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(state == GameState.MainMenu);
            if (hudPanel != null) hudPanel.SetActive(state == GameState.Playing);
            if (pausePanel != null) pausePanel.SetActive(state == GameState.Paused);
            if (gameOverPanel != null) gameOverPanel.SetActive(state == GameState.GameOver);
        }

        private void PopulateGameOverStats()
        {
            if (ScoreManager.Instance != null)
            {
                if (finalScoreText != null)
                    finalScoreText.text = $"Điểm: {ScoreManager.Instance.CurrentScore:N0}";

                if (finalDistanceText != null)
                    finalDistanceText.text = $"Quãng đường: {ScoreManager.Instance.DistanceMeters}m";

                if (finalCoinsText != null)
                    finalCoinsText.text = $"Xu thu thập: {ScoreManager.Instance.CoinsCount}";

                if (gameOverHighScoreText != null)
                    gameOverHighScoreText.text = $"Kỷ lục: {ScoreManager.Instance.HighScore:N0}";

                if (newRecordNotice != null)
                    newRecordNotice.SetActive(ScoreManager.Instance.IsNewHighScore);
            }
        }

        #endregion

        #region Cập nhật HUD & Dữ liệu

        private int lastScore = -1;
        private int lastMeters = -1;
        private int lastCoins = -1;
        private int lastLives = -1;
        private int lastCrystalCurr = -1;
        private int lastCrystalTarget = -1;
        private int lastShieldSec = -1;
        private int lastMagnetSec = -1;
        private int lastDoubleSec = -1;

        private void HandleScoreChanged(int score)
        {
            if (score == lastScore) return;
            lastScore = score;
            if (scoreText != null)
            {
                scoreText.text = score.ToString("D6");
            }
        }

        private void HandleDistanceChanged(int meters)
        {
            if (meters == lastMeters) return;
            lastMeters = meters;
            if (distanceText != null)
            {
                distanceText.text = $"{meters}m";
            }
        }

        private void HandleCoinsChanged(int coins)
        {
            if (coins == lastCoins) return;
            lastCoins = coins;
            if (coinsText != null)
            {
                coinsText.text = $"XU: {coins}";
            }
        }

        private void HandleLivesChanged(int lives)
        {
            if (lives == lastLives) return;
            lastLives = lives;
            if (livesText != null)
            {
                livesText.text = $"MANG: {lives}";
            }
        }

        private void HandleCrystalProgressChanged(int current, int target)
        {
            if (current == lastCrystalCurr && target == lastCrystalTarget) return;
            lastCrystalCurr = current;
            lastCrystalTarget = target;
            if (crystalsText != null)
            {
                crystalsText.text = $"TINH THE: {current}/{target}";
            }
        }

        private void UpdateHighScore(int highScore)
        {
            if (menuHighScoreText != null)
            {
                menuHighScoreText.text = $"TOP RUN: {highScore:N0}";
            }
            if (gameOverHighScoreText != null)
            {
                gameOverHighScoreText.text = $"TOP RUN: {highScore:N0}";
            }
        }

        #endregion

        #region Cập nhật Power-Up HUD

        private void HandlePowerUpStarted(PowerUpType type, float duration)
        {
            ResetPowerUpCache(type);
            SetPowerUpRootActive(type, true);
            UpdatePowerUpTime(type, duration);
        }

        private void HandlePowerUpTick(PowerUpType type, float remaining)
        {
            UpdatePowerUpTime(type, remaining);
        }

        private void HandlePowerUpEnded(PowerUpType type)
        {
            ResetPowerUpCache(type);
            SetPowerUpRootActive(type, false);
        }

        private void ResetPowerUpCache(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield: lastShieldSec = -1; break;
                case PowerUpType.Magnet: lastMagnetSec = -1; break;
                case PowerUpType.DoubleScore: lastDoubleSec = -1; break;
            }
        }

        private void SetPowerUpRootActive(PowerUpType type, bool active)
        {
            switch (type)
            {
                case PowerUpType.Shield:
                    if (shieldRoot != null) shieldRoot.SetActive(active);
                    break;
                case PowerUpType.Magnet:
                    if (magnetRoot != null) magnetRoot.SetActive(active);
                    break;
                case PowerUpType.DoubleScore:
                    if (doubleScoreRoot != null) doubleScoreRoot.SetActive(active);
                    break;
            }
        }

        private void UpdatePowerUpTime(PowerUpType type, float remaining)
        {
            int sec = Mathf.CeilToInt(remaining);
            switch (type)
            {
                case PowerUpType.Shield:
                    if (sec == lastShieldSec) return;
                    lastShieldSec = sec;
                    if (shieldTimerText != null) shieldTimerText.text = $"VAN TRUOT: {sec}s";
                    break;
                case PowerUpType.Magnet:
                    if (sec == lastMagnetSec) return;
                    lastMagnetSec = sec;
                    if (magnetTimerText != null) magnetTimerText.text = $"NAM CHAM: {sec}s";
                    break;
                case PowerUpType.DoubleScore:
                    if (sec == lastDoubleSec) return;
                    lastDoubleSec = sec;
                    if (doubleScoreTimerText != null) doubleScoreTimerText.text = $"x2 DIEM: {sec}s";
                    break;
            }
        }

        #endregion

        #region Button Handlers

        private void OnStartClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartGame();
            }
        }

        private void OnPauseClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PauseGame();
            }
        }

        private void OnResumeClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResumeGame();
            }
        }

        private void OnRestartClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
        }

        private void OnQuitClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.QuitGame();
            }
        }

        #endregion

        #region Public Configuration Helper

        /// <summary>
        /// Gán các thành phần UI từ Editor script khi dựng Canvas tự động bằng code.
        /// </summary>
        public void ConfigurePanels(
            GameObject menu,
            GameObject hud,
            GameObject pause,
            GameObject over)
        {
            mainMenuPanel = menu;
            hudPanel = hud;
            pausePanel = pause;
            gameOverPanel = over;
        }

        public void ConfigureMainMenu(TMP_Text highScore, Button start, Button quit)
        {
            menuHighScoreText = highScore;
            startButton = start;
            quitButton = quit;
        }

        public void ConfigureHUD(
            TMP_Text score,
            TMP_Text dist,
            TMP_Text coins,
            TMP_Text lives,
            TMP_Text crystals,
            Button pause,
            GameObject shield, TMP_Text sText,
            GameObject magnet, TMP_Text mText,
            GameObject doubleScore, TMP_Text dsText)
        {
            scoreText = score;
            distanceText = dist;
            coinsText = coins;
            livesText = lives;
            crystalsText = crystals;
            pauseButton = pause;

            shieldRoot = shield;
            shieldTimerText = sText;

            magnetRoot = magnet;
            magnetTimerText = mText;

            doubleScoreRoot = doubleScore;
            doubleScoreTimerText = dsText;
        }

        public void ConfigurePause(Button resume, Button restart, Button quit)
        {
            resumeButton = resume;
            pauseRestartButton = restart;
            pauseQuitButton = quit;
        }

        public void ConfigureGameOver(
            TMP_Text score,
            TMP_Text dist,
            TMP_Text coins,
            TMP_Text high,
            GameObject newRecord,
            Button restart,
            Button quit)
        {
            finalScoreText = score;
            finalDistanceText = dist;
            finalCoinsText = coins;
            gameOverHighScoreText = high;
            newRecordNotice = newRecord;
            gameOverRestartButton = restart;
            gameOverQuitButton = quit;
        }

        #endregion
    }
}

