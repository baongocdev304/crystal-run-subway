using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Hoạt ảnh thủ tục (Procedural Animation) cho nhân vật phong cách Street Runner (Subway Surfers):
    /// - Khi chạy: Vung tay và chân ngược pha theo tốc độ thực tế, thân người nhấp nhô và hơi đổ về trước.
    /// - Khi chuyển làn: Nghiêng người mượt mà (Tilt Z & Yaw Y) về hướng làn đang rẽ.
    /// - Khi nhảy: Co chân tư thế parkour trên không và giơ cao tay.
    /// - Khi trượt (Slide): Hạ thấp trọng tâm và xoay nhào lộn 360 độ về phía trước.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerVisualAnimator : MonoBehaviour
    {
        [Header("=== Tham chiếu Điều khiển ===")]
        [Tooltip("PlayerController trên cùng đối tượng Player")]
        [SerializeField] private PlayerController playerController;

        [Header("=== Các Khớp Mô hình (Visual Hierarchy) ===")]
        [Tooltip("Transform gốc chứa toàn bộ phần nhìn của nhân vật")]
        [SerializeField] private Transform visualRoot;

        [Tooltip("Khớp thân trên (Áo Hoodie & Balo)")]
        [SerializeField] private Transform torso;

        [Tooltip("Khớp đầu & mũ lưỡi trai")]
        [SerializeField] private Transform head;

        [Tooltip("Khớp vai/tay trái")]
        [SerializeField] private Transform leftArm;

        [Tooltip("Khớp vai/tay phải")]
        [SerializeField] private Transform rightArm;

        [Tooltip("Khớp hông/chân trái")]
        [SerializeField] private Transform leftLeg;

        [Tooltip("Khớp hông/chân phải")]
        [SerializeField] private Transform rightLeg;

        [Header("=== Thông số Hoạt ảnh Chạy ===")]
        [Tooltip("Hệ số tần số bước chạy nhân với tốc độ di chuyển")]
        [SerializeField] private float strideFrequencyMultiplier = 1.45f;

        [Tooltip("Biên độ góc vung tay tối đa (độ)")]
        [SerializeField] private float armSwingAngle = 48.0f;

        [Tooltip("Biên độ góc sải chân tối đa (độ)")]
        [SerializeField] private float legSwingAngle = 55.0f;

        [Tooltip("Góc đổ người về phía trước khi chạy (độ)")]
        [SerializeField] private float runForwardTilt = 8.5f;

        [Tooltip("Góc nghiêng người tối đa khi rẽ làn (độ)")]
        [SerializeField] private float maxLaneLeanAngle = 16.0f;

        private float runCyclePhase = 0f;
        private float currentLeanZ = 0f;
        private float currentYawY = 0f;
        private float slideRollProgress = 0f;
        private float previousX = 0f;

        private Vector3 initialVisualRootLocalPos;
        private Vector3 initialTorsoLocalPos;

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            if (visualRoot != null)
            {
                initialVisualRootLocalPos = visualRoot.localPosition;
            }

            if (torso != null)
            {
                initialTorsoLocalPos = torso.localPosition;
            }

            previousX = transform.position.x;
        }

        private void Update()
        {
            if (playerController == null || visualRoot == null) return;

            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            // Tính vận tốc ngang để nghiêng người khi chuyển làn
            float currentX = transform.position.x;
            float horizontalVel = (currentX - previousX) / dt;
            previousX = currentX;

            float normalizedHorizontal = Mathf.Clamp(horizontalVel / 12.0f, -1.0f, 1.0f);
            float targetLeanZ = -normalizedHorizontal * maxLaneLeanAngle;
            float targetYawY = normalizedHorizontal * (maxLaneLeanAngle * 0.75f);

            currentLeanZ = Mathf.Lerp(currentLeanZ, targetLeanZ, dt * 12.0f);
            currentYawY = Mathf.Lerp(currentYawY, targetYawY, dt * 12.0f);

            if (playerController.IsSliding)
            {
                AnimateSlideRoll(dt);
            }
            else if (!playerController.IsGrounded)
            {
                slideRollProgress = 0f;
                AnimateJumpPose(dt);
            }
            else
            {
                slideRollProgress = 0f;
                AnimateRunCycle(dt);
            }
        }

        private void AnimateRunCycle(float dt)
        {
            float speed = Mathf.Max(8.0f, playerController.CurrentSpeed);
            runCyclePhase += dt * speed * strideFrequencyMultiplier;

            float swing = Mathf.Sin(runCyclePhase);
            float bounce = Mathf.Abs(Mathf.Cos(runCyclePhase)) * 0.055f;

            // Phục hồi vị trí gốc kèm độ nảy bước chạy
            visualRoot.localPosition = Vector3.Lerp(
                visualRoot.localPosition,
                initialVisualRootLocalPos + new Vector3(0f, bounce, 0f),
                dt * 16.0f
            );

            // Thân đổ nhẹ về trước + nghiêng theo làn
            visualRoot.localRotation = Quaternion.Lerp(
                visualRoot.localRotation,
                Quaternion.Euler(runForwardTilt, currentYawY, currentLeanZ),
                dt * 15.0f
            );

            // Vung tay ngược chiều chân
            if (leftArm != null)
            {
                leftArm.localRotation = Quaternion.Euler(swing * armSwingAngle, 0f, 6f);
            }
            if (rightArm != null)
            {
                rightArm.localRotation = Quaternion.Euler(-swing * armSwingAngle, 0f, -6f);
            }

            if (leftLeg != null)
            {
                leftLeg.localRotation = Quaternion.Euler(-swing * legSwingAngle, 0f, 0f);
            }
            if (rightLeg != null)
            {
                rightLeg.localRotation = Quaternion.Euler(swing * legSwingAngle, 0f, 0f);
            }

            if (head != null)
            {
                head.localRotation = Quaternion.Euler(-4f, -currentYawY * 0.3f, 0f);
            }
        }

        private void AnimateJumpPose(float dt)
        {
            visualRoot.localPosition = Vector3.Lerp(
                visualRoot.localPosition,
                initialVisualRootLocalPos + new Vector3(0f, 0.08f, 0f),
                dt * 12.0f
            );

            visualRoot.localRotation = Quaternion.Lerp(
                visualRoot.localRotation,
                Quaternion.Euler(-6f, currentYawY, currentLeanZ),
                dt * 10.0f
            );

            // Tay vung cao giữ thăng bằng trên không
            if (leftArm != null)
            {
                leftArm.localRotation = Quaternion.Lerp(leftArm.localRotation, Quaternion.Euler(-115f, 0f, 22f), dt * 12.0f);
            }
            if (rightArm != null)
            {
                rightArm.localRotation = Quaternion.Lerp(rightArm.localRotation, Quaternion.Euler(-115f, 0f, -22f), dt * 12.0f);
            }

            // Chân co nhịp parkour
            if (leftLeg != null)
            {
                leftLeg.localRotation = Quaternion.Lerp(leftLeg.localRotation, Quaternion.Euler(-45f, 0f, 0f), dt * 12.0f);
            }
            if (rightLeg != null)
            {
                rightLeg.localRotation = Quaternion.Lerp(rightLeg.localRotation, Quaternion.Euler(32f, 0f, 0f), dt * 12.0f);
            }
        }

        private void AnimateSlideRoll(float dt)
        {
            // Xoay nhào lộn 360 độ về phía trước trong suốt thời gian trượt (~0.8s)
            slideRollProgress = Mathf.Min(1.0f, slideRollProgress + (dt / 0.75f));
            float rollAngle = slideRollProgress * 360.0f;

            visualRoot.localPosition = Vector3.Lerp(
                visualRoot.localPosition,
                initialVisualRootLocalPos + new Vector3(0f, -0.35f, 0f),
                dt * 18.0f
            );

            visualRoot.localRotation = Quaternion.Euler(rollAngle, currentYawY, currentLeanZ);

            // Thu gọn tay chân khi cuộn tròn
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-65f, 0f, 10f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-65f, 0f, -10f);
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(-55f, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-55f, 0f, 0f);
        }

        /// <summary>
        /// Gán tham chiếu các khớp mô hình từ công cụ dựng Prefab trong Editor.
        /// </summary>
        public void ConfigureParts(
            PlayerController controller,
            Transform root,
            Transform bodyTorso,
            Transform headPart,
            Transform lArm,
            Transform rArm,
            Transform lLeg,
            Transform rLeg)
        {
            playerController = controller;
            visualRoot = root;
            torso = bodyTorso;
            head = headPart;
            leftArm = lArm;
            rightArm = rArm;
            leftLeg = lLeg;
            rightLeg = rLeg;
        }
    }
}

