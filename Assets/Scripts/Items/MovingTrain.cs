using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Điều khiển đoàn tàu điện Tokyo chạy ngược chiều về phía người chơi (Subway Surfers style):
    /// - Đứng chờ cho tới khi người chơi tiến vào phạm vi kích hoạt (activationDistanceZ).
    /// - Lao thẳng về phía người chơi dọc theo trục -Z của làn đường ray.
    /// - Tự động đặt lại vị trí ban đầu khi TrackSegment được tái sử dụng từ Object Pool.
    /// </summary>
    [DisallowMultipleComponent]
    public class MovingTrain : MonoBehaviour
    {
        [Header("=== Cấu hình Di chuyển Đoàn tàu ===")]
        [Tooltip("Vận tốc di chuyển ngược chiều của đoàn tàu (m/s)")]
        [SerializeField] private float trainSpeed = 14.0f;

        [Tooltip("Khoảng cách trục Z từ người chơi tới đầu tàu để tàu bắt đầu chạy (mét)")]
        [SerializeField] private float activationDistanceZ = 55.0f;

        [Tooltip("Khoảng cách phía sau lưng người chơi để đoàn tàu ngừng di chuyển (mét)")]
        [SerializeField] private float stopDistanceBehindPlayer = 25.0f;

        [Header("=== Tham chiếu Mục tiêu ===")]
        [Tooltip("Transform của nhân vật chính (tự tìm trong Start nếu để trống)")]
        [SerializeField] private Transform targetPlayer;

        private TrackSegment parentSegment;
        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;
        private bool isMoving = false;
        private bool hasSearchedPlayer = false;

        private void Awake()
        {
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;

            parentSegment = GetComponentInParent<TrackSegment>();
            if (parentSegment != null)
            {
                parentSegment.OnResetSegment += ResetTrain;
            }
        }

        private void Start()
        {
            CachePlayerReference();
        }

        private void OnDestroy()
        {
            if (parentSegment != null)
            {
                parentSegment.OnResetSegment -= ResetTrain;
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                return;
            }

            if (targetPlayer == null)
            {
                if (!hasSearchedPlayer)
                {
                    CachePlayerReference();
                }
                if (targetPlayer == null) return;
            }

            float deltaZ = transform.position.z - targetPlayer.position.z;

            if (!isMoving)
            {
                // Kích hoạt đoàn tàu khi người chơi tiến vào vùng quan sát phía trước
                if (deltaZ <= activationDistanceZ && deltaZ > -stopDistanceBehindPlayer)
                {
                    isMoving = true;
                }
                return;
            }

            // Ngừng cập nhật khi tàu đã lướt qua hẳn phía sau người chơi
            if (deltaZ < -stopDistanceBehindPlayer)
            {
                return;
            }

            // Di chuyển đoàn tàu ngược chiều người chơi dọc theo làn đường (-Z)
            transform.localPosition += Vector3.back * (trainSpeed * Time.deltaTime);
        }

        private void CachePlayerReference()
        {
            hasSearchedPlayer = true;
            if (targetPlayer == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    targetPlayer = playerObj.transform;
                }
            }
        }

        /// <summary>
        /// Khôi phục vị trí ban đầu của đoàn tàu khi đoạn đường được lấy ra từ Object Pool.
        /// </summary>
        public void ResetTrain()
        {
            isMoving = false;
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
        }

        /// <summary>
        /// Cấu hình thông số vận tốc và khoảng cách kích hoạt từ script dựng cảnh Editor.
        /// </summary>
        public void Configure(float speed, float triggerDistance)
        {
            trainSpeed = speed;
            activationDistanceZ = triggerDistance;
        }
    }
}

