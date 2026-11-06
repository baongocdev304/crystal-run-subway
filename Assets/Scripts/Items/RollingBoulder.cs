using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Điều khiển hành vi của tảng đá khổng lồ lăn qua lại giữa các làn chạy:
    /// - Di chuyển ngang theo chu kỳ sin qua lại giữa làn Trái (X = -amplitude) và làn Phải (X = +amplitude).
    /// - Tự động xoay tròn ăn khớp với hướng lăn ngang.
    /// - Sử dụng Rigidbody Kinematic và SphereCollider để đảm bảo phát hiện va chạm vật lý chính xác.
    /// - Chỉ bắt đầu lăn khi người chơi chạy tới gần trong một khoảng cách nhất định (triggerDistanceZ) để người chơi có đủ thời gian quan sát và phản ứng.
    /// - Tự động khôi phục vị trí ban đầu khi TrackSegment được tái sử dụng từ Object Pool.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class RollingBoulder : MonoBehaviour
    {
        [Header("=== Cấu hình Chuyển động Ngang ===")]
        [Tooltip("Biên độ dao động ngang tính từ tâm (mét). Mặc định: 2.5m tương ứng với khoảng cách chuyển làn")]
        [SerializeField] private float amplitude = 2.5f;

        [Tooltip("Tốc độ dao động ngang qua lại giữa các làn")]
        [SerializeField] private float horizontalSpeed = 2.5f;

        [Header("=== Cấu hình Xoay & Lăn ===")]
        [Tooltip("Bán kính tảng đá (mét, dùng để tính toán góc xoay vật lý ăn khớp với quãng đường lăn)")]
        [SerializeField] private float boulderRadius = 1.0f;

        [Header("=== Kích hoạt Tiếp cận ===")]
        [Tooltip("Khoảng cách trục Z từ người chơi đến tảng đá để bắt đầu kích hoạt lăn (mét)")]
        [SerializeField] private float triggerDistanceZ = 35.0f;

        [Tooltip("Transform của nhân vật chính để đo khoảng cách kích hoạt (tự tìm Tag 'Player' nếu để trống)")]
        [SerializeField] private Transform targetPlayer;

        private Rigidbody rb;
        private SphereCollider sphereCollider;
        private TrackSegment parentSegment;

        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;

        private bool isRolling = false;
        private float elapsedTime = 0.0f;
        private float previousOffsetX = 0.0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();

            // Cấu hình vật lý: Kinematic để kiểm soát hoàn toàn quỹ đạo lăn mà không bị vấp cạnh sàn
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Đảm bảo gắn Tag chuẩn Obstacle
            if (!gameObject.CompareTag("Obstacle"))
            {
                gameObject.tag = "Obstacle";
            }

            // Đảm bảo component Obstacle đã được gắn với kiểu RollingBoulder
            Obstacle obstacleComponent = GetComponent<Obstacle>();
            if (obstacleComponent == null)
            {
                obstacleComponent = gameObject.AddComponent<Obstacle>();
                obstacleComponent.Configure(ObstacleType.RollingBoulder, 1);
            }

            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;

            // Đăng ký sự kiện reset khi đoạn đường TrackSegment được lấy ra từ Pool
            parentSegment = GetComponentInParent<TrackSegment>();
            if (parentSegment != null)
            {
                parentSegment.OnResetSegment += ResetBoulder;
            }
        }

        private void Start()
        {
            FindPlayerTarget();
        }

        private void OnDestroy()
        {
            if (parentSegment != null)
            {
                parentSegment.OnResetSegment -= ResetBoulder;
            }
        }

        private void Update()
        {
            // Nếu chưa bắt đầu lăn, kiểm tra khoảng cách với nhân vật
            if (!isRolling)
            {
                CheckPlayerProximity();
                return;
            }

            // Khi đã kích hoạt, thực thi chuyển động ngang và xoay
            UpdateRollingMovement();
        }

        /// <summary>
        /// Tìm Transform của nhân vật chính trong màn chơi nếu chưa được gán.
        /// </summary>
        private void FindPlayerTarget()
        {
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
        /// Kiểm tra xem nhân vật đã tiếp cận đủ gần để tảng đá bắt đầu lăn hay chưa.
        /// </summary>
        private void CheckPlayerProximity()
        {
            if (targetPlayer == null)
            {
                FindPlayerTarget();
                if (targetPlayer == null) return;
            }

            float distanceZ = transform.position.z - targetPlayer.position.z;

            // Kích hoạt khi nhân vật nằm trong tầm triggerDistanceZ phía trước tảng đá
            if (distanceZ <= triggerDistanceZ && distanceZ >= -5.0f)
            {
                isRolling = true;
                elapsedTime = 0.0f;
                previousOffsetX = 0.0f;
            }
        }

        /// <summary>
        /// Cập nhật vị trí lăn ngang và góc quay vật lý của quả cầu đá.
        /// </summary>
        private void UpdateRollingMovement()
        {
            elapsedTime += Time.deltaTime;

            // Tính độ lệch X theo hàm sin dao động êm ái
            float currentOffsetX = Mathf.Sin(elapsedTime * horizontalSpeed) * amplitude;
            float deltaX = currentOffsetX - previousOffsetX;

            // Cập nhật vị trí X local
            transform.localPosition = new Vector3(
                initialLocalPosition.x + currentOffsetX,
                initialLocalPosition.y,
                initialLocalPosition.z
            );

            // Tính góc xoay quanh trục Z theo hướng di chuyển ngang (săn khớp vật lý: omega = deltaX / radius)
            if (Mathf.Abs(deltaX) > 0.0001f && boulderRadius > 0.01f)
            {
                float rotationAngle = -(deltaX / boulderRadius) * Mathf.Rad2Deg;
                transform.Rotate(Vector3.forward, rotationAngle, Space.World);
            }

            previousOffsetX = currentOffsetX;
        }

        /// <summary>
        /// Khôi phục trạng thái ban đầu của tảng đá khi đoạn đường được tái sử dụng.
        /// </summary>
        public void ResetBoulder()
        {
            isRolling = false;
            elapsedTime = 0.0f;
            previousOffsetX = 0.0f;

            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
        }

        #region Public Methods

        /// <summary>
        /// Gán mục tiêu nhân vật từ script dựng cảnh hoặc GameManager.
        /// </summary>
        public void SetTarget(Transform player)
        {
            targetPlayer = player;
        }

        /// <summary>
        /// Thiết lập thông số lăn ngang từ bên ngoài.
        /// </summary>
        public void Configure(float newAmplitude, float newSpeed, float radius, float triggerDistance)
        {
            amplitude = newAmplitude;
            horizontalSpeed = newSpeed;
            boulderRadius = radius;
            triggerDistanceZ = triggerDistance;
        }

        #endregion
    }
}

