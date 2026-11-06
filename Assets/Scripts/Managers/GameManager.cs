using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CrystalRun
{
    /// <summary>
    /// Các trạng thái chính của vòng đời trò chơi.
    /// </summary>
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver
    }

    /// <summary>
    /// Quản lý trạng thái trò chơi trung tâm (Singleton, không sử dụng DontDestroyOnLoad):
    /// - Quản lý chuyển đổi giữa các trạng thái: MainMenu, Playing, Paused, GameOver.
    /// - Điều khiển tạm dừng (Pause / Resume) với Time.timeScale = 0 và phím Escape/P (Input System mới).
    /// - Lắng nghe PlayerHealth.OnDied để kết thúc trò chơi an toàn một lần duy nhất (chống gọi lặp).
    /// - Khởi động lại ván chơi (RestartGame) và thoát game (QuitGame).
    /// - Kích hoạt và dọn dẹp các hệ thống phụ trợ (TrackSpawner, ScoreManager, PowerUpManager).
    /// - Phát sự kiện OnStateChanged cho UI và các controller.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("=== Cấu hình Game ===")]
        [Tooltip("Asset GameConfig chứa tham số toàn cục (bắt buộc gán trong Inspector)")]
        [SerializeField] private GameConfig config;

        [Header("=== Tham chiếu Thành phần ===")]
        [Tooltip("Controller của nhân vật chính")]
        [SerializeField] private PlayerController playerController;

        [Tooltip("Hệ thống máu và va chạm của nhân vật")]
        [SerializeField] private PlayerHealth playerHealth;

        [Tooltip("Hệ thống sinh đường chạy vô tận")]
        [SerializeField] private TrackSpawner trackSpawner;

        [Tooltip("Hệ thống tính điểm")]
        [SerializeField] private ScoreManager scoreManager;

        [Tooltip("Hệ thống quản lý hiệu ứng Power-Up")]
        [SerializeField] private PowerUpManager powerUpManager;

        [Header("=== Cài đặt Khởi động ===")]
        [Tooltip("Tự động bắt đầu chơi ngay khi tải màn (tiện cho test gameplay trực tiếp)")]
        [SerializeField] private bool autoStartGame = true;

        #region Events

        /// <summary>Kích hoạt khi trạng thái trò chơi thay đổi</summary>
        public static event Action<GameState> OnStateChanged;

        #endregion

        #region Properties

        /// <summary>Trạng thái hiện tại của trò chơi</summary>
        public GameState State => currentState;

        /// <summary>Trò chơi có đang trong trạng thái Playing hay không</summary>
        public bool IsPlaying => currentState == GameState.Playing;

        /// <summary>Trò chơi có đang bị tạm dừng hay không</summary>
        public bool IsPaused => currentState == GameState.Paused;

        /// <summary>Trò chơi đã kết thúc (Game Over) hay chưa</summary>
        public bool IsGameOver => currentState == GameState.GameOver;

        #endregion

        private GameState currentState = GameState.MainMenu;
        private bool isTransitioningGameOver = false;

        private void Awake()
        {
            // Thiết lập Singleton (không sử dụng DontDestroyOnLoad theo yêu cầu)
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Đảm bảo Time.timeScale luôn được phục hồi về 1 khi Awake
            Time.timeScale = 1.0f;

            FindComponentReferences();
        }

        private void Start()
        {
            // Đăng ký nhận sự kiện tử nạn từ PlayerHealth
            if (playerHealth != null)
            {
                playerHealth.OnDied -= HandlePlayerDied;
                playerHealth.OnDied += HandlePlayerDied;
            }

            if (autoStartGame)
            {
                StartGame();
            }
            else
            {
                SetState(GameState.MainMenu);
            }
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDied -= HandlePlayerDied;
            }

            // Đảm bảo Time.timeScale luôn được phục hồi về 1 khi object bị hủy
            Time.timeScale = 1.0f;
        }

        private void Update()
        {
            HandlePauseInput();
        }

        /// <summary>
        /// Xử lý phím tắt Escape hoặc P để tạm dừng hoặc tiếp tục chơi bằng Input System mới.
        /// </summary>
        private void HandlePauseInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)
            {
                if (currentState == GameState.Playing)
                {
                    PauseGame();
                }
                else if (currentState == GameState.Paused)
                {
                    ResumeGame();
                }
            }
        }

        #region Quản lý Trạng thái Trò chơi

        /// <summary>
        /// Bắt đầu một ván chơi mới: thiết lập vận tốc, khởi tạo lại đường, điểm số và hiệu ứng.
        /// </summary>
        public void StartGame()
        {
            isTransitioningGameOver = false;
            Time.timeScale = 1.0f;

            FindComponentReferences();

            // 1. Reset bộ đếm tinh thể và buff
            Crystal.ResetCrystalProgress();

            if (PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.ResetAllPowerUps();
            }

            // 2. Reset người chơi
            if (playerHealth != null)
            {
                playerHealth.ResetHealth();
            }

            if (playerController != null)
            {
                playerController.isPlaying = true;
            }

            // 3. Khởi tạo đường chạy
            if (trackSpawner != null && playerController != null)
            {
                trackSpawner.Init(playerController.transform);
            }
            else
            {
                if (playerController == null)
                {
                    Debug.LogError("[GameManager] Không tìm thấy PlayerController trong scene! Hãy kiểm tra GameObject có tag 'Player'.");
                }
                if (trackSpawner == null)
                {
                    Debug.LogError("[GameManager] Không tìm thấy TrackSpawner trong scene! Hãy tạo đối tượng TrackSpawner hoặc chạy menu 'Tools > Crystal Run > Build All'.");
                }
            }

            // 4. Reset điểm số
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.ResetScore();
            }

            SetState(GameState.Playing);
        }

        /// <summary>
        /// Tạm dừng trò chơi (Pause): Đóng băng thời gian bằng Time.timeScale = 0.
        /// </summary>
        public void PauseGame()
        {
            if (currentState != GameState.Playing) return;

            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        /// <summary>
        /// Tiếp tục trò chơi (Resume): Khôi phục thời gian bằng Time.timeScale = 1.
        /// </summary>
        public void ResumeGame()
        {
            if (currentState != GameState.Paused) return;

            Time.timeScale = 1.0f;
            SetState(GameState.Playing);
        }

        /// <summary>
        /// Kết thúc ván chơi (Game Over): Chỉ kích hoạt một lần duy nhất, khôi phục timeScale = 1.
        /// </summary>
        public void GameOver()
        {
            if (isTransitioningGameOver || currentState == GameState.GameOver)
            {
                return; // Chống gọi lặp
            }

            isTransitioningGameOver = true;
            Time.timeScale = 1.0f; // Khôi phục timeScale đúng chuẩn khi GameOver

            // Dừng nhân vật
            if (playerController != null)
            {
                playerController.isPlaying = false;
            }

            // Lưu điểm cao kỷ lục
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.SaveHighScore();
            }

            SetState(GameState.GameOver);
        }

        /// <summary>
        /// Nạp lại scene hiện tại để chơi lại ván mới.
        /// </summary>
        public void RestartGame()
        {
            Time.timeScale = 1.0f;
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(currentSceneIndex);
        }

        /// <summary>
        /// Thoát ứng dụng (hỗ trợ cả dừng Play mode trong Unity Editor).
        /// </summary>
        public void QuitGame()
        {
            Time.timeScale = 1.0f;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            OnStateChanged?.Invoke(currentState);
        }

        private void HandlePlayerDied()
        {
            GameOver();
        }

        #endregion

        #region Helpers & Component Discovery

        /// <summary>
        /// Tự động tìm kiếm các thành phần cốt lõi trong scene nếu chưa được kéo vào Inspector.
        /// </summary>
        private void FindComponentReferences()
        {
            if (playerController == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null)
                {
                    playerController = p.GetComponent<PlayerController>();
                    playerHealth = p.GetComponent<PlayerHealth>();
                }
            }

            if (trackSpawner == null)
            {
                trackSpawner = UnityEngine.Object.FindAnyObjectByType<TrackSpawner>();
            }

            if (scoreManager == null)
            {
                scoreManager = UnityEngine.Object.FindAnyObjectByType<ScoreManager>();
            }

            if (powerUpManager == null)
            {
                powerUpManager = UnityEngine.Object.FindAnyObjectByType<PowerUpManager>();
            }
        }

        /// <summary>
        /// Cung cấp tham chiếu hệ thống từ script dựng scene của Editor.
        /// </summary>
        public void SetReferences(
            PlayerController player,
            PlayerHealth health,
            TrackSpawner spawner,
            ScoreManager score,
            PowerUpManager powerUp,
            GameConfig newConfig)
        {
            playerController = player;
            playerHealth = health;
            trackSpawner = spawner;
            scoreManager = score;
            powerUpManager = powerUp;
            config = newConfig;

            if (playerHealth != null)
            {
                playerHealth.OnDied -= HandlePlayerDied;
                playerHealth.OnDied += HandlePlayerDied;
            }
        }

        #endregion
    }
}
