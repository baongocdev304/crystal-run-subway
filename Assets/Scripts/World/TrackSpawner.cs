using System.Collections.Generic;
using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Hệ thống sinh đường chạy vô tận (Endless Track Spawner) sử dụng Object Pool tự viết:
    /// - Quản lý danh sách Prefab đoạn đường chia theo từng giai đoạn độ khó (1, 2, 3) và đoạn an toàn.
    /// - Luôn duy trì số lượng đoạn cố định (6-8 đoạn) trước mặt người chơi.
    /// - Tự động thu hồi đoạn cũ về Object Pool khi nhân vật chạy qua và nối đoạn mới vào cuối.
    /// - Chọn đoạn ngẫu nhiên có trọng số theo giai đoạn hiện tại (quãng đường từ GameConfig).
    /// - Ngăn chặn xuất hiện 2 đoạn "nặng" liên tiếp để đảm bảo nhịp thở cho người chơi.
    /// - Đảm bảo 3 đoạn đầu tiên luôn là đoạn an toàn, không có bẫy.
    /// </summary>
    public class TrackSpawner : MonoBehaviour
    {
        [Header("=== Cấu hình Game ===")]
        [Tooltip("Asset ScriptableObject chứa các mốc khoảng cách giai đoạn độ khó (bắt buộc gán trong Inspector)")]
        [SerializeField] private GameConfig config;

        [Header("=== Mục tiêu Theo dõi ===")]
        [Tooltip("Transform của nhân vật chính để tính toán khoảng cách thu hồi và sinh đường (có thể gán qua Inspector hoặc hàm Init)")]
        [SerializeField] private Transform playerTransform;

        [Header("=== Danh sách Prefab Đoạn đường theo Giai đoạn ===")]
        [Tooltip("Danh sách prefab đoạn an toàn (không có chướng ngại vật) dùng cho đoạn xuất phát và chuyển tiếp")]
        [SerializeField] private List<TrackSegment> safePrefabs = new List<TrackSegment>();

        [Tooltip("Danh sách prefab Giai đoạn 1 (0 - 500m: chướng ngại vật thấp, dễ né)")]
        [SerializeField] private List<TrackSegment> stage1Prefabs = new List<TrackSegment>();

        [Tooltip("Danh sách prefab Giai đoạn 2 (500 - 1500m: xuất hiện hố sâu và vật cản treo cao)")]
        [SerializeField] private List<TrackSegment> stage2Prefabs = new List<TrackSegment>();

        [Tooltip("Danh sách prefab Giai đoạn 3 (từ 1500m: tảng đá lăn, chướng ngại vật dày đặc)")]
        [SerializeField] private List<TrackSegment> stage3Prefabs = new List<TrackSegment>();

        [Header("=== Cài đặt Sinh & Thu hồi ===")]
        [Tooltip("Số lượng đoạn đường luôn duy trì cùng lúc trong màn chơi (mặc định: 7 đoạn)")]
        [SerializeField] private int activeSegmentsCount = 7;

        [Tooltip("Số đoạn đường an toàn ban đầu tính từ vạch xuất phát (mặc định: 3 đoạn)")]
        [SerializeField] private int initialSafeSegmentsCount = 3;

        [Tooltip("Khoảng cách phía sau nhân vật để thu hồi đoạn đường cũ về pool (mét)")]
        [SerializeField] private float despawnDistanceBehindPlayer = 15.0f;

        [Tooltip("Transform chứa các đoạn đường được sinh ra để giữ Hierarchy gọn gàng")]
        [SerializeField] private Transform trackContainer;

        #region Properties

        /// <summary>Vị trí Z tiếp theo sẽ nối đoạn đường mới</summary>
        public float CurrentSpawnZ => currentSpawnZ;

        /// <summary>Số lượng đoạn đường đang hoạt động trên màn chơi</summary>
        public int ActiveSegmentCount => activeSegments.Count;

        #endregion

        // Giới hạn an toàn chống treo vĩnh viễn khi sinh đoạn đường
        private const int MAX_LOOP_SAFETY_LIMIT = 20;

        // Quản lý các đoạn đang hoạt động
        private readonly Queue<TrackSegment> activeSegments = new Queue<TrackSegment>();

        // Hệ thống Object Pool: mỗi Prefab gốc sở hữu một hàng đợi (Queue) các instance tái sử dụng
        private readonly Dictionary<TrackSegment, Queue<TrackSegment>> poolDictionary =
            new Dictionary<TrackSegment, Queue<TrackSegment>>();

        private float currentSpawnZ = 0.0f;
        private float startPlayerZ = 0.0f;
        private bool lastSegmentWasHeavy = false;
        private bool isInitialized = false;

        private void Start()
        {
            // Kiểm tra và đặt giá trị an toàn cho các thông số
            if (activeSegmentsCount <= 0) activeSegmentsCount = 7;
            if (initialSafeSegmentsCount < 0) initialSafeSegmentsCount = 0;
            if (despawnDistanceBehindPlayer <= 0f) despawnDistanceBehindPlayer = 15.0f;

            // Thiết lập container chứa các đoạn đường nếu chưa gán
            if (trackContainer == null)
            {
                GameObject container = new GameObject("TrackContainer");
                container.transform.SetParent(transform);
                trackContainer = container.transform;
            }

            // Nếu chưa được gọi qua GameManager.Init, tự động tìm Player trong scene để hỗ trợ test độc lập
            if (!isInitialized)
            {
                if (playerTransform == null)
                {
                    GameObject playerObj = GameObject.FindWithTag("Player");
                    if (playerObj != null)
                    {
                        playerTransform = playerObj.transform;
                    }
                }

                if (playerTransform != null)
                {
                    Init(playerTransform);
                }
                else
                {
                    Debug.LogWarning("[TrackSpawner] Chưa gán PlayerTransform và không tìm thấy đối tượng Tag 'Player'. Hệ thống sinh đường sẽ đợi GameManager gọi Init().");
                }
            }
        }

        private void Update()
        {
            if (playerTransform == null || activeSegments.Count == 0) return;

            // Kiểm tra xem đoạn đường đầu tiên đã bị nhân vật vượt qua một khoảng an toàn hay chưa
            TrackSegment firstSegment = activeSegments.Peek();
            if (firstSegment == null)
            {
                activeSegments.Dequeue();
                return;
            }

            float segmentLength = (firstSegment.SegmentLength > 0f) ? firstSegment.SegmentLength : 30.0f;
            float segmentEndZ = firstSegment.transform.position.z + segmentLength;

            if (playerTransform.position.z > segmentEndZ + despawnDistanceBehindPlayer)
            {
                // Thu hồi đoạn cũ về Object Pool
                activeSegments.Dequeue();
                ReturnToPool(firstSegment);

                // Sinh đoạn mới nối tiếp ở cuối đường
                SpawnNextSegment();
            }
        }

        #region Khởi tạo & Tái thiết lập (Reset)

        /// <summary>
        /// Khởi tạo hệ sinh đường với Transform của Player (được gọi từ GameManager).
        /// </summary>
        /// <param name="player">Transform của nhân vật chính</param>
        public void Init(Transform player)
        {
            playerTransform = player;
            startPlayerZ = (playerTransform != null) ? playerTransform.position.z : 0.0f;
            isInitialized = true;

            ResetTrack();
        }

        /// <summary>
        /// Tái lập toàn bộ đường chạy về trạng thái ban đầu để chơi lại.
        /// </summary>
        public void ResetTrack()
        {
            // 1. Thu hồi toàn bộ các đoạn đang hoạt động về Object Pool (có chặn giới hạn an toàn)
            int despawnSafety = activeSegments.Count + 10;
            while (activeSegments.Count > 0 && despawnSafety-- > 0)
            {
                TrackSegment segment = activeSegments.Dequeue();
                ReturnToPool(segment);
            }

            currentSpawnZ = 0.0f;
            lastSegmentWasHeavy = false;

            // Kiểm tra an toàn: nếu không có bất kỳ prefab nào được gán thì dừng ngay, log cảnh báo rõ ràng
            if (!HasAnyValidPrefabs())
            {
                Debug.LogError("[TrackSpawner] Tất cả danh sách Prefab đoạn đường đều TRỐNG! Hãy mở Menu 'Tools > Crystal Run > Build All' để tự động tạo và gán Prefab.");
                return;
            }

            // 2. Sinh các đoạn an toàn ban đầu (mặc định 3 đoạn đầu tiên)
            int safeCount = Mathf.Min(initialSafeSegmentsCount, activeSegmentsCount);
            for (int i = 0; i < safeCount; i++)
            {
                TrackSegment safePrefab = PickSafePrefab();
                if (safePrefab != null)
                {
                    SpawnSegment(safePrefab);
                }
            }

            // 3. Sinh các đoạn tiếp theo cho đủ số lượng activeSegmentsCount (giới hạn an toàn tối đa 20 vòng lặp để chống treo)
            int spawnSafety = MAX_LOOP_SAFETY_LIMIT;
            while (activeSegments.Count < activeSegmentsCount && spawnSafety > 0)
            {
                spawnSafety--;
                SpawnNextSegment();
            }

            if (spawnSafety <= 0 && activeSegments.Count < activeSegmentsCount)
            {
                Debug.LogWarning($"[TrackSpawner] Đã đạt giới hạn an toàn {MAX_LOOP_SAFETY_LIMIT} vòng lặp khi sinh đoạn đường (sinh được {activeSegments.Count}/{activeSegmentsCount} đoạn). Kiểm tra danh sách Prefab xem có bị null không.");
            }
        }

        #endregion

        #region Thuật toán Chọn Đoạn & Sinh Đường

        /// <summary>
        /// Sinh đoạn đường tiếp theo dựa trên giai đoạn độ khó và quy tắc không lặp 2 đoạn nặng.
        /// </summary>
        private void SpawnNextSegment()
        {
            TrackSegment prefabToSpawn = PickSegmentPrefab();
            if (prefabToSpawn == null)
            {
                prefabToSpawn = PickSafePrefab();
            }

            if (prefabToSpawn != null)
            {
                SpawnSegment(prefabToSpawn);
            }
        }

        /// <summary>
        /// Lấy một instance đoạn đường từ Object Pool và đặt vào vị trí nối tiếp cuối đường.
        /// </summary>
        private void SpawnSegment(TrackSegment prefab)
        {
            if (prefab == null) return;

            TrackSegment segment = GetFromPool(prefab);
            if (segment == null)
            {
                Debug.LogError($"[TrackSpawner] Không thể lấy đối tượng từ Pool cho prefab '{prefab.name}'!");
                return;
            }

            float length = segment.SegmentLength;
            if (length <= 0f)
            {
                Debug.LogWarning($"[TrackSpawner] Đoạn đường '{segment.name}' có SegmentLength = {length} <= 0! Tự động gán mặc định 30m.");
                length = 30.0f;
            }

            segment.transform.position = new Vector3(0f, 0f, currentSpawnZ);
            segment.transform.rotation = Quaternion.identity;

            currentSpawnZ += length;
            lastSegmentWasHeavy = segment.IsHeavy;

            activeSegments.Enqueue(segment);
        }

        /// <summary>
        /// Lựa chọn Prefab đoạn đường tiếp theo có trọng số dựa trên quãng đường người chơi đã đi.
        /// </summary>
        private TrackSegment PickSegmentPrefab()
        {
            float distance = GetPlayerTravelledDistance();
            int currentStage = (config != null) ? config.GetCurrentStage(distance) : 1;

            List<TrackSegment> candidateList = null;

            // Lựa chọn danh sách ứng viên theo giai đoạn (Stage)
            switch (currentStage)
            {
                case 1:
                    candidateList = stage1Prefabs;
                    break;

                case 2:
                    candidateList = (Random.value < 0.3f && stage1Prefabs != null && stage1Prefabs.Count > 0)
                        ? stage1Prefabs
                        : stage2Prefabs;
                    break;

                case 3:
                default:
                    float randVal = Random.value;
                    if (randVal < 0.1f && stage1Prefabs != null && stage1Prefabs.Count > 0)
                    {
                        candidateList = stage1Prefabs;
                    }
                    else if (randVal < 0.4f && stage2Prefabs != null && stage2Prefabs.Count > 0)
                    {
                        candidateList = stage2Prefabs;
                    }
                    else
                    {
                        candidateList = stage3Prefabs;
                    }
                    break;
            }

            // Nếu danh sách ứng viên rỗng, thử các danh sách dự phòng
            if (candidateList == null || candidateList.Count == 0)
            {
                candidateList = GetFallbackCandidateList();
            }

            if (candidateList == null || candidateList.Count == 0)
            {
                return PickSafePrefab();
            }

            // Quy tắc: Không cho hai đoạn "nặng" liên tiếp
            List<TrackSegment> validCandidates = new List<TrackSegment>();
            for (int i = 0; i < candidateList.Count; i++)
            {
                TrackSegment seg = candidateList[i];
                if (seg == null) continue;

                if (lastSegmentWasHeavy && seg.IsHeavy)
                {
                    continue; // Bỏ qua đoạn nặng nếu đoạn trước đó đã là đoạn nặng
                }
                validCandidates.Add(seg);
            }

            // Nếu lọc xong mà không còn ứng viên nào, chọn một đoạn an toàn hoặc fallback bất kỳ
            if (validCandidates.Count == 0)
            {
                TrackSegment safe = PickSafePrefab();
                if (safe != null) return safe;

                for (int i = 0; i < candidateList.Count; i++)
                {
                    if (candidateList[i] != null) validCandidates.Add(candidateList[i]);
                }
            }

            if (validCandidates.Count == 0)
            {
                return PickSafePrefab();
            }

            int randomIndex = Random.Range(0, validCandidates.Count);
            return validCandidates[randomIndex];
        }

        /// <summary>
        /// Chọn một đoạn đường an toàn (Safe Segment) hợp lệ.
        /// </summary>
        private TrackSegment PickSafePrefab()
        {
            TrackSegment picked = GetRandomValidFrom(safePrefabs);
            if (picked != null) return picked;

            picked = GetRandomValidFrom(stage1Prefabs);
            if (picked != null) return picked;

            picked = GetRandomValidFrom(stage2Prefabs);
            if (picked != null) return picked;

            picked = GetRandomValidFrom(stage3Prefabs);
            if (picked != null) return picked;

            return null;
        }

        private TrackSegment GetRandomValidFrom(List<TrackSegment> list)
        {
            if (list == null || list.Count == 0) return null;

            List<TrackSegment> valids = new List<TrackSegment>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) valids.Add(list[i]);
            }

            if (valids.Count == 0) return null;
            return valids[Random.Range(0, valids.Count)];
        }

        private bool HasAnyValidPrefabs()
        {
            return HasAnyNonNull(safePrefabs) ||
                   HasAnyNonNull(stage1Prefabs) ||
                   HasAnyNonNull(stage2Prefabs) ||
                   HasAnyNonNull(stage3Prefabs);
        }

        private bool HasAnyNonNull(List<TrackSegment> list)
        {
            if (list == null || list.Count == 0) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) return true;
            }
            return false;
        }

        private List<TrackSegment> GetFallbackCandidateList()
        {
            if (stage1Prefabs != null && stage1Prefabs.Count > 0) return stage1Prefabs;
            if (stage2Prefabs != null && stage2Prefabs.Count > 0) return stage2Prefabs;
            if (stage3Prefabs != null && stage3Prefabs.Count > 0) return stage3Prefabs;
            return safePrefabs;
        }

        private float GetPlayerTravelledDistance()
        {
            if (playerTransform == null) return 0f;
            return Mathf.Max(0f, playerTransform.position.z - startPlayerZ);
        }

        #endregion

        #region Hệ thống Object Pool

        /// <summary>
        /// Lấy đối tượng đoạn đường từ Pool; nếu Pool đang trống thì Instantiate mới.
        /// </summary>
        private TrackSegment GetFromPool(TrackSegment prefab)
        {
            if (!poolDictionary.TryGetValue(prefab, out Queue<TrackSegment> pool))
            {
                pool = new Queue<TrackSegment>();
                poolDictionary[prefab] = pool;
            }

            TrackSegment instance;
            if (pool.Count > 0)
            {
                instance = pool.Dequeue();
                instance.gameObject.SetActive(true);
                instance.ResetSegment();
            }
            else
            {
                Transform parent = (trackContainer != null) ? trackContainer : transform;
                instance = Instantiate(prefab, parent);
                instance.PrefabSource = prefab;
            }

            return instance;
        }

        /// <summary>
        /// Đưa đối tượng đoạn đường đã đi qua trở lại Pool để tái sử dụng.
        /// </summary>
        private void ReturnToPool(TrackSegment segment)
        {
            if (segment == null) return;

            segment.gameObject.SetActive(false);

            TrackSegment sourcePrefab = segment.PrefabSource;
            if (sourcePrefab != null)
            {
                if (!poolDictionary.TryGetValue(sourcePrefab, out Queue<TrackSegment> pool))
                {
                    pool = new Queue<TrackSegment>();
                    poolDictionary[sourcePrefab] = pool;
                }
                pool.Enqueue(segment);
            }
            else
            {
                // Phòng ngừa trường hợp instance không có source prefab
                Destroy(segment.gameObject);
            }
        }

        #endregion

        #region Public Configuration Helper

        /// <summary>
        /// Cấu hình danh sách prefab qua code (hữu ích cho Editor script khi tự động sinh prefab và scene).
        /// </summary>
        public void SetPrefabs(
            List<TrackSegment> safe,
            List<TrackSegment> stage1,
            List<TrackSegment> stage2,
            List<TrackSegment> stage3)
        {
            safePrefabs = safe ?? new List<TrackSegment>();
            stage1Prefabs = stage1 ?? new List<TrackSegment>();
            stage2Prefabs = stage2 ?? new List<TrackSegment>();
            stage3Prefabs = stage3 ?? new List<TrackSegment>();
        }

        /// <summary>
        /// Thiết lập GameConfig từ bên ngoài.
        /// </summary>
        public void SetConfig(GameConfig newConfig)
        {
            config = newConfig;
        }

        #endregion
    }
}

