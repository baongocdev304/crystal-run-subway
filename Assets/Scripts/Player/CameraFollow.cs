using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Điều khiển Camera bám theo nhân vật phong cách Subway Surfers:
    /// - Bám cứng theo trục Z để giữ khoảng cách cố định.
    /// - Bám mượt theo trục X và trục Y (hỗ trợ nâng góc nhìn êm ái khi nhân vật chạy trên nóc tàu Y = 2.6m).
    /// - Hiệu ứng rung chấn (Camera Shake) khi va chạm vật cản hoặc vỡ khiên/ván trượt.
    /// - Tự động mở rộng góc nhìn (Dynamic FOV 60 -> 68) khi tốc độ chạy tăng cao.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("=== Mục tiêu Theo dõi ===")]
        [Tooltip("Transform mục tiêu nhân vật (Player) cần bám theo")]
        [SerializeField] private Transform target;

        [Header("=== Khoảng cách Offset ===")]
        [Tooltip("Độ lệch vị trí (X, Y, Z) so với nhân vật")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 4.6f, -7.2f);

        [Header("=== Độ mượt mà (Smoothing) ===")]
        [Tooltip("Thời gian làm mượt bám theo trục X khi chuyển làn (giây)")]
        [SerializeField] private float smoothTimeX = 0.11f;

        [Tooltip("Thời gian làm mượt bám theo trục Y khi nhảy hoặc leo lên nóc tàu (giây)")]
        [SerializeField] private float smoothTimeY = 0.14f;

        [Header("=== Góc nhìn & Dynamic FOV ===")]
        [Tooltip("Góc nghiêng nhìn xuống (độ) để thấy rõ đường ray và nóc tàu phía trước")]
        [SerializeField] private float pitchAngle = 19.0f;

        [Tooltip("Khóa góc quay cố định theo pitchAngle")]
        [SerializeField] private bool lockPitchRotation = true;

        [Tooltip("FOV cơ bản ở tốc độ khởi điểm")]
        [SerializeField] private float baseFov = 60.0f;

        [Tooltip("FOV tối đa khi đạt tốc độ cao nhất")]
        [SerializeField] private float maxSpeedFov = 68.0f;

        private Camera cam;
        private PlayerController playerController;
        private PlayerHealth playerHealth;

        private float currentVelocityX;
        private float currentVelocityY;

        // Trạng thái rung camera (Camera Shake)
        private float shakeTimer = 0f;
        private float shakeMagnitude = 0.25f;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            if (target == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    target = playerObj.transform;
                }
            }

            BindPlayerComponents();

            if (target != null)
            {
                transform.position = target.position + offset;
                if (lockPitchRotation)
                {
                    transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
                }
            }
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged -= HandlePlayerDamaged;
                playerHealth.OnShieldChanged -= HandlePlayerShieldChanged;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    SetTarget(playerObj.transform);
                }
                else
                {
                    return;
                }
            }

            Vector3 currentPos = transform.position;

            // 1. Trục X: Bám mượt khoảng 75% độ lệch làn để giữ cảm giác trung tâm đường ray giống Subway Surfers
            float targetX = (target.position.x * 0.78f) + offset.x;
            float newX = Mathf.SmoothDamp(currentPos.x, targetX, ref currentVelocityX, smoothTimeX);

            // 2. Trục Y: Bám mượt theo độ cao nhân vật (đặc biệt khi leo lên nóc tàu Y = 2.6m)
            float extraRoofElevation = (target.position.y > 1.8f) ? 0.45f : 0f;
            float targetY = target.position.y + offset.y + extraRoofElevation;
            float newY = Mathf.SmoothDamp(currentPos.y, targetY, ref currentVelocityY, smoothTimeY);

            // 3. Trục Z: Bám cứng tuyệt đối
            float newZ = target.position.z + offset.z;

            Vector3 finalPos = new Vector3(newX, newY, newZ);

            // 4. Áp dụng rung chấn nếu đang trong thời gian Shake
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                Vector2 randOffset = Random.insideUnitCircle * shakeMagnitude;
                finalPos.x += randOffset.x;
                finalPos.y += randOffset.y;
            }

            transform.position = finalPos;

            // 5. Góc quay nhìn xuống
            if (lockPitchRotation)
            {
                transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
            }

            // 6. Cập nhật Dynamic FOV theo tốc độ chạy
            if (cam != null && playerController != null)
            {
                float speedRatio = Mathf.InverseLerp(10.0f, 25.0f, playerController.CurrentSpeed);
                float targetFov = Mathf.Lerp(baseFov, maxSpeedFov, speedRatio);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 5.0f);
            }
        }

        private void BindPlayerComponents()
        {
            if (target == null) return;

            playerController = target.GetComponent<PlayerController>();
            PlayerHealth newHealth = target.GetComponent<PlayerHealth>();

            if (newHealth != playerHealth)
            {
                if (playerHealth != null)
                {
                    playerHealth.OnDamaged -= HandlePlayerDamaged;
                    playerHealth.OnShieldChanged -= HandlePlayerShieldChanged;
                }

                playerHealth = newHealth;
                if (playerHealth != null)
                {
                    playerHealth.OnDamaged += HandlePlayerDamaged;
                    playerHealth.OnShieldChanged += HandlePlayerShieldChanged;
                }
            }
        }

        private void HandlePlayerDamaged(int damage)
        {
            TriggerShake(0.28f, 0.32f);
        }

        private void HandlePlayerShieldChanged(bool hasShield)
        {
            // Rung nhẹ khi khiên/ván trượt bị vỡ
            if (!hasShield && GameManager.Instance != null && GameManager.Instance.IsPlaying)
            {
                TriggerShake(0.20f, 0.20f);
            }
        }

        #region Public Methods

        /// <summary>
        /// Kích hoạt hiệu ứng rung lắc camera khi va chạm.
        /// </summary>
        public void TriggerShake(float duration = 0.25f, float magnitude = 0.25f)
        {
            shakeTimer = duration;
            shakeMagnitude = magnitude;
        }

        /// <summary>
        /// Gán mục tiêu mới cho camera.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            BindPlayerComponents();

            if (target != null)
            {
                transform.position = target.position + offset;
                if (lockPitchRotation)
                {
                    transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
                }
            }
        }

        /// <summary>
        /// Thiết lập offset vị trí camera từ script bên ngoài.
        /// </summary>
        public void SetOffset(Vector3 newOffset)
        {
            offset = newOffset;
        }

        #endregion
    }
}
