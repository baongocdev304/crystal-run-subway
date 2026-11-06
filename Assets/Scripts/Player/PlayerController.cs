using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrystalRun
{
    /// <summary>
    /// Điều khiển toàn bộ hành vi của nhân vật chính trong game chạy vô tận CrystalRun:
    /// - Tự động chạy tiến theo trục +Z với tốc độ tăng dần theo GameConfig.
    /// - Chuyển đổi mượt mà giữa 3 làn chạy (-1: Trái, 0: Giữa, 1: Phải).
    /// - Nhảy (Space/Up) và trượt (S/Down), hỗ trợ rơi nhanh (Fast-fall) khi đang trên không.
    /// - Quản lý trạng thái bất tử nhấp nháy khi va chạm chướng ngại vật.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        [Header("=== Cấu hình Game ===")]
        [Tooltip("Asset ScriptableObject chứa toàn bộ tham số cân bằng (bắt buộc gán trong Inspector khi dựng scene/prefab)")]
        [SerializeField] private GameConfig config;

        [Header("=== Cài đặt Kiểm tra Mặt đất ===")]
        [Tooltip("LayerMask quy định các mặt sàn/đường chạy được coi là mặt đất (bắt buộc cấu hình khi dựng scene)")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("Khoảng cách tia kiểm tra mặt đất từ đáy nhân vật")]
        [SerializeField] private float groundCheckDistance = 0.25f;

        [Tooltip("Bán kính hình cầu kiểm tra mặt đất (SphereCast)")]
        [SerializeField] private float groundCheckRadius = 0.25f;

        [Header("=== Cài đặt Nhảy & Trọng lực ===")]
        [Tooltip("Hệ số gia tốc trọng lực bổ sung khi rơi xuống để cú nhảy gọn gàng, dứt khoát")]
        [SerializeField] private float fallGravityMultiplier = 2.5f;

        [Tooltip("Vận tốc rơi nhanh thẳng đứng khi bấm phím xuống lúc đang trên không (m/s)")]
        [SerializeField] private float fastFallSpeed = 22.0f;

        [Header("=== Trạng thái Gameplay & Input ===")]
        [Tooltip("Cờ trạng thái kiểm soát nhân vật (tự động đồng bộ với GameManager.Instance.State nếu có)")]
        public bool isPlaying = true;

        #region Events (Sự kiện cho âm thanh, VFX và UI)

        /// <summary>Kích hoạt khi nhân vật bắt đầu nhảy lên</summary>
        public event Action OnJump;

        /// <summary>Kích hoạt khi nhân vật bắt đầu trượt</summary>
        public event Action OnSlide;

        /// <summary>Kích hoạt khi nhân vật tiếp đất sau khi ở trên không</summary>
        public event Action OnLand;

        /// <summary>Kích hoạt khi nhân vật chuyển làn chạy</summary>
        public event Action OnLaneChange;

        /// <summary>Kích hoạt kèm chỉ số làn mới (-1, 0, 1)</summary>
        public event Action<int> OnLaneChangedWithIndex;

        #endregion

        #region Properties

        /// <summary>Quãng đường đã chạy tính theo mét từ vị trí Z ban đầu</summary>
        public float DistanceTravelled => Mathf.Max(0f, transform.position.z - startZPosition);

        /// <summary>Tốc độ chạy hiện tại (m/s)</summary>
        public float CurrentSpeed => currentSpeed;

        /// <summary>Làn chạy hiện tại (-1: Trái, 0: Giữa, 1: Phải)</summary>
        public int CurrentLane => currentLane;

        /// <summary>Nhân vật có đang chạm mặt đất hay không</summary>
        public bool IsGrounded => isGrounded;

        /// <summary>Nhân vật có đang trong trạng thái trượt hay không</summary>
        public bool IsSliding => isSliding;

        /// <summary>Nhân vật có đang trong thời gian bất tử hay không</summary>
        public bool IsInvincible => isInvincible;

        #endregion

        // Component references
        private Rigidbody rb;
        private CapsuleCollider capsuleCollider;
        private Renderer[] childRenderers;

        // Vị trí và tốc độ
        private float startZPosition;
        private float currentSpeed;
        private int currentLane = 0; // -1: Trái, 0: Giữa, 1: Phải

        // Trạng thái nhảy & mặt đất
        private bool isGrounded = true;
        private bool wasGroundedLastFrame = true;

        // Trạng thái trượt
        private bool isSliding = false;
        private bool pendingSlideOnLanding = false;
        private float originalColliderHeight;
        private Vector3 originalColliderCenter;
        private Coroutine slideCoroutine;

        // Trạng thái bất tử
        private bool isInvincible = false;
        private Coroutine invincibilityCoroutine;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();
            childRenderers = GetComponentsInChildren<Renderer>(true);

            // Cấu hình Rigidbody theo yêu cầu của game endless runner
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Lưu kích thước collider ban đầu để phục hồi sau khi trượt
            originalColliderHeight = capsuleCollider.height;
            originalColliderCenter = capsuleCollider.center;

            startZPosition = transform.position.z;
        }

        private void Start()
        {
            // Thiết lập giá trị mặc định cho groundLayer nếu chưa được gán trong Inspector
            if (groundLayer.value == 0)
            {
                // Mặc định kiểm tra va chạm với tất cả layer ngoại trừ Player và Ignore Raycast
                groundLayer = ~LayerMask.GetMask("Player", "Ignore Raycast");
            }

            // Khởi tạo tốc độ ban đầu
            currentSpeed = (config != null) ? config.InitialSpeed : 10.0f;
        }

        private void Update()
        {
            // Kiểm tra trạng thái chơi
            if (!CanProcessInput())
            {
                return;
            }

            HandleInput();
        }

        private void FixedUpdate()
        {
            UpdateGroundCheck();

            if (!CanProcessInput())
            {
                // Khi tạm dừng hoặc chưa bắt đầu, giữ nguyên vị trí ngang và dừng chạy tiến
                Vector3 stopVel = rb.linearVelocity;
                stopVel.x = 0f;
                stopVel.z = 0f;
                rb.linearVelocity = stopVel;
                return;
            }

            UpdateSpeed();
            MoveCharacter();
            ApplyCustomGravity();
        }

        private void OnDisable()
        {
            // Dọn dẹp trạng thái khi GameObject bị tắt hoặc reset
            if (isSliding)
            {
                StopSlide();
            }

            if (isInvincible)
            {
                if (invincibilityCoroutine != null)
                {
                    StopCoroutine(invincibilityCoroutine);
                    invincibilityCoroutine = null;
                }
                SetRenderersVisibility(true);
                int playerLayer = LayerMask.NameToLayer("Player");
                int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                if (playerLayer != -1 && obstacleLayer != -1)
                {
                    Physics.IgnoreLayerCollision(playerLayer, obstacleLayer, false);
                }
                isInvincible = false;
            }
        }

        #region Xử lý Input

        // Trạng thái nhận diện thao tác vuốt cảm ứng (Touch Swipe)
        private Vector2 touchStartPos;
        private bool isTrackingTouch;
        private bool touchSwipeConsumed;
        private const float MinSwipeDistancePixels = 35f;

        /// <summary>
        /// Đọc input từ Unity Input System mới (Keyboard.current &amp; Touchscreen.current).
        /// </summary>
        private void HandleInput()
        {
            HandleKeyboardInput();
            HandleTouchSwipeInput();
        }

        private void HandleKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Chuyển làn sang trái (Phím A hoặc Mũi tên trái)
            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                ChangeLane(-1);
            }
            // Chuyển làn sang phải (Phím D hoặc Mũi tên phải)
            else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                ChangeLane(1);
            }

            // Nhảy (Phím Space hoặc Mũi tên lên)
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                Jump();
            }

            // Trượt hoặc Rơi nhanh (Phím S hoặc Mũi tên xuống)
            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                SlideOrFastFall();
            }
        }

        /// <summary>
        /// Nhận diện vuốt màn hình cảm ứng (Trái / Phải / Lên / Xuống) trên điện thoại.
        /// </summary>
        private void HandleTouchSwipeInput()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null) return;

            var primaryTouch = touchscreen.primaryTouch;
            if (primaryTouch.press.wasPressedThisFrame)
            {
                touchStartPos = primaryTouch.position.ReadValue();
                isTrackingTouch = true;
                touchSwipeConsumed = false;
            }
            else if (isTrackingTouch && primaryTouch.press.isPressed && !touchSwipeConsumed)
            {
                Vector2 currentPos = primaryTouch.position.ReadValue();
                Vector2 delta = currentPos - touchStartPos;
                if (delta.magnitude >= MinSwipeDistancePixels)
                {
                    touchSwipeConsumed = true;
                    ExecuteSwipeDelta(delta);
                }
            }
            else if (isTrackingTouch && primaryTouch.press.wasReleasedThisFrame)
            {
                if (!touchSwipeConsumed)
                {
                    Vector2 endPos = primaryTouch.position.ReadValue();
                    Vector2 delta = endPos - touchStartPos;
                    if (delta.magnitude >= MinSwipeDistancePixels * 0.6f)
                    {
                        ExecuteSwipeDelta(delta);
                    }
                }
                isTrackingTouch = false;
                touchSwipeConsumed = false;
            }
        }

        private void ExecuteSwipeDelta(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                ChangeLane(delta.x < 0f ? -1 : 1);
            }
            else
            {
                if (delta.y > 0f)
                {
                    Jump();
                }
                else
                {
                    SlideOrFastFall();
                }
            }
        }

        public void SwipeLeft()
        {
            if (CanProcessInput()) ChangeLane(-1);
        }

        public void SwipeRight()
        {
            if (CanProcessInput()) ChangeLane(1);
        }

        public void SwipeUp()
        {
            if (CanProcessInput()) Jump();
        }

        public void SwipeDown()
        {
            if (CanProcessInput()) SlideOrFastFall();
        }

        /// <summary>
        /// Thay đổi làn chạy với hướng: -1 (trái) hoặc +1 (phải).
        /// </summary>
        private void ChangeLane(int direction)
        {
            int targetLane = Mathf.Clamp(currentLane + direction, -1, 1);
            if (targetLane != currentLane)
            {
                currentLane = targetLane;
                OnLaneChange?.Invoke();
                OnLaneChangedWithIndex?.Invoke(currentLane);
            }
        }

        /// <summary>
        /// Thực hiện nhảy nếu đang ở trên mặt đất.
        /// </summary>
        private void Jump()
        {
            if (!isGrounded) return;

            // Nếu đang trượt thì hủy trượt ngay để chuyển sang nhảy
            if (isSliding)
            {
                StopSlide();
            }

            float jumpForce = (config != null) ? config.JumpForce : 8.5f;

            Vector3 vel = rb.linearVelocity;
            vel.y = jumpForce;
            rb.linearVelocity = vel;

            isGrounded = false;
            OnJump?.Invoke();
        }

        /// <summary>
        /// Trượt nếu đang trên mặt đất; nếu đang ở trên không thì rơi nhanh (fast fall) rồi trượt khi chạm đất.
        /// </summary>
        private void SlideOrFastFall()
        {
            if (isGrounded)
            {
                StartSlide();
            }
            else
            {
                // Đang ở trên không: kích hoạt rơi nhanh thẳng đứng
                Vector3 vel = rb.linearVelocity;
                vel.y = -fastFallSpeed;
                rb.linearVelocity = vel;
                pendingSlideOnLanding = true;
            }
        }

        #endregion

        #region Di chuyển & Vật lý

        /// <summary>
        /// Cập nhật tốc độ chạy tiến dựa trên quãng đường đã đi từ GameConfig.
        /// </summary>
        private void UpdateSpeed()
        {
            if (config != null)
            {
                currentSpeed = config.CalculateSpeed(DistanceTravelled);
            }
        }

        /// <summary>
        /// Điều khiển di chuyển của Rigidbody:
        /// - Trục X: Chuyển làn mượt mà bằng Mathf.MoveTowards.
        /// - Trục Z: Tự động chạy tiến đều đặn theo CurrentSpeed.
        /// </summary>
        private void MoveCharacter()
        {
            float laneDist = (config != null) ? config.LaneDistance : 2.5f;
            float laneSpeed = (config != null) ? config.LaneChangeSpeed : 15.0f;

            float targetX = currentLane * laneDist;
            float currentX = rb.position.x;

            // Di chuyển mượt mà X sang vị trí làn đích
            float newX = Mathf.MoveTowards(currentX, targetX, laneSpeed * Time.fixedDeltaTime);
            float xVelocity = (newX - currentX) / Time.fixedDeltaTime;

            Vector3 vel = rb.linearVelocity;
            vel.x = xVelocity;
            vel.z = CanProcessInput() ? currentSpeed : 0f;
            rb.linearVelocity = vel;
        }

        /// <summary>
        /// Áp dụng trọng lực bổ sung khi rơi để cú nhảy có cảm giác dứt khoát, không bị lơ lửng.
        /// </summary>
        private void ApplyCustomGravity()
        {
            if (!isGrounded)
            {
                // Khi rơi xuống (vận tốc Y < 0), bổ sung thêm lực kéo xuống
                if (rb.linearVelocity.y < 0f)
                {
                    rb.AddForce(Vector3.up * (Physics.gravity.y * (fallGravityMultiplier - 1.0f)), ForceMode.Acceleration);
                }
            }
        }

        /// <summary>
        /// Kiểm tra mặt đất bằng SphereCast từ đáy CapsuleCollider (tự thích ứng theo pivot chân hoặc tâm).
        /// </summary>
        private void UpdateGroundCheck()
        {
            // Tính toán chính xác vị trí đáy của CapsuleCollider dù pivot của model đặt ở bàn chân hay ở tâm
            float capsuleBottomY = capsuleCollider.center.y - (capsuleCollider.height * 0.5f);
            Vector3 castOrigin = transform.position + new Vector3(
                capsuleCollider.center.x,
                capsuleBottomY + groundCheckRadius + 0.05f,
                capsuleCollider.center.z
            );
            float castDistance = groundCheckDistance + 0.05f;

            bool hitGround = Physics.SphereCast(
                castOrigin,
                groundCheckRadius,
                Vector3.down,
                out RaycastHit hit,
                castDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            // Kiểm tra thêm Raycast thẳng đứng nếu vận tốc Y gần 0 để chống trượt phát hiện trên mặt phẳng
            if (!hitGround && Mathf.Abs(rb.linearVelocity.y) < 0.1f)
            {
                Vector3 rayOrigin = transform.position + new Vector3(
                    capsuleCollider.center.x,
                    capsuleBottomY + 0.1f,
                    capsuleCollider.center.z
                );
                hitGround = Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    castDistance + 0.1f,
                    groundLayer,
                    QueryTriggerInteraction.Ignore
                );
            }

            isGrounded = hitGround;

            // Sự kiện tiếp đất (Landing)
            if (!wasGroundedLastFrame && isGrounded)
            {
                OnLand?.Invoke();

                // Nếu trước đó đang rơi nhanh kèm lệnh trượt thì trượt ngay khi tiếp đất
                if (pendingSlideOnLanding)
                {
                    pendingSlideOnLanding = false;
                    StartSlide();
                }
            }

            wasGroundedLastFrame = isGrounded;
        }

        #endregion

        #region Cơ chế Trượt (Slide)

        /// <summary>
        /// Bắt đầu coroutine trượt: thu nhỏ CapsuleCollider và hạ thấp trọng tâm.
        /// </summary>
        private void StartSlide()
        {
            if (slideCoroutine != null)
            {
                StopCoroutine(slideCoroutine);
            }
            slideCoroutine = StartCoroutine(SlideRoutine());
        }

        private void StopSlide()
        {
            if (slideCoroutine != null)
            {
                StopCoroutine(slideCoroutine);
                slideCoroutine = null;
            }
            RestoreCollider();
            isSliding = false;
        }

        private IEnumerator SlideRoutine()
        {
            isSliding = true;
            OnSlide?.Invoke();

            float slideHeight = (config != null) ? config.SlideColliderHeight : 0.6f;
            float slideDuration = (config != null) ? config.SlideDuration : 0.8f;

            if (slideDuration <= 0f) slideDuration = 0.8f;
            if (slideHeight <= 0.1f) slideHeight = 0.6f;

            // Hạ chiều cao và tâm của collider để nhân vật chui lọt vật cản treo cao
            capsuleCollider.height = slideHeight;
            float heightDifference = originalColliderHeight - slideHeight;
            capsuleCollider.center = new Vector3(
                originalColliderCenter.x,
                originalColliderCenter.y - (heightDifference * 0.5f),
                originalColliderCenter.z
            );

            yield return new WaitForSeconds(slideDuration);

            RestoreCollider();
            isSliding = false;
            slideCoroutine = null;
        }

        private void RestoreCollider()
        {
            capsuleCollider.height = originalColliderHeight;
            capsuleCollider.center = originalColliderCenter;
        }

        #endregion

        #region Cơ chế Bất tử (Invincibility)

        /// <summary>
        /// Kích hoạt trạng thái bất tử tạm thời: nhấp nháy mô hình và bỏ qua va chạm giữa layer Player và Obstacle.
        /// </summary>
        /// <param name="seconds">Thời gian bất tử (nếu <= 0 sẽ lấy từ GameConfig)</param>
        public void SetInvincible(float seconds = -1f)
        {
            if (seconds <= 0f)
            {
                seconds = (config != null) ? config.InvincibilityDuration : 2.0f;
            }

            if (seconds <= 0f)
            {
                seconds = 2.0f;
            }

            if (invincibilityCoroutine != null)
            {
                StopCoroutine(invincibilityCoroutine);
            }
            invincibilityCoroutine = StartCoroutine(InvincibilityRoutine(seconds));
        }

        private IEnumerator InvincibilityRoutine(float duration)
        {
            isInvincible = true;

            int playerLayer = LayerMask.NameToLayer("Player");
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");

            // Bỏ qua va chạm vật lý giữa người chơi và chướng ngại vật
            if (playerLayer != -1 && obstacleLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, obstacleLayer, true);
            }

            float blinkInterval = (config != null) ? config.InvincibilityBlinkRate : 0.1f;
            if (blinkInterval <= 0.01f) blinkInterval = 0.1f;
            if (duration <= 0f) duration = 2.0f;

            float elapsed = 0f;
            bool isVisible = true;

            // Đảm bảo lấy danh sách renderers mới nhất nếu có thay đổi
            if (childRenderers == null || childRenderers.Length == 0)
            {
                childRenderers = GetComponentsInChildren<Renderer>(true);
            }

            // Giới hạn an toàn chống vòng lặp vô hạn nếu Time.timeScale bị gián đoạn
            int maxBlinks = Mathf.CeilToInt(duration / blinkInterval) + 10;
            int blinkCount = 0;

            while (elapsed < duration && blinkCount < maxBlinks)
            {
                blinkCount++;
                isVisible = !isVisible;
                SetRenderersVisibility(isVisible);

                yield return new WaitForSeconds(blinkInterval);
                elapsed += blinkInterval;
            }

            // Kết thúc bất tử: bật lại hiển thị và phục hồi va chạm layer
            SetRenderersVisibility(true);

            if (playerLayer != -1 && obstacleLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, obstacleLayer, false);
            }

            isInvincible = false;
            invincibilityCoroutine = null;
        }

        private void SetRenderersVisibility(bool visible)
        {
            if (childRenderers == null) return;
            for (int i = 0; i < childRenderers.Length; i++)
            {
                if (childRenderers[i] != null)
                {
                    childRenderers[i].enabled = visible;
                }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Kiểm tra xem nhân vật có thể nhận input điều khiển hay không (liên kết với GameManager.Instance.IsPlaying).
        /// </summary>
        private bool CanProcessInput()
        {
            if (GameManager.Instance != null)
            {
                return GameManager.Instance.IsPlaying;
            }
            return isPlaying;
        }

        /// <summary>
        /// Thiết lập cấu hình GameConfig từ bên ngoài (hữu ích cho Editor script khi dựng scene).
        /// </summary>
        public void SetConfig(GameConfig newConfig)
        {
            config = newConfig;
        }

        #endregion
    }
}

