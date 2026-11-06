using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Đại diện cho một đoạn đường chạy (Module) trong hệ thống chạy vô tận.
    /// Gắn trực tiếp trên root của mỗi Prefab đoạn đường.
    /// Quản lý độ dài, độ khó, tính chất chướng ngại vật và tái kích hoạt toàn bộ vật phẩm/bẫy khi tái sử dụng từ Object Pool.
    /// </summary>
    public class TrackSegment : MonoBehaviour
    {
        [Header("=== Cấu hình Kích thước & Vị trí ===")]
        [Tooltip("Chiều dài dọc trục Z của đoạn đường này (mét, ví dụ 30m)")]
        [SerializeField] private float segmentLength = 30.0f;

        [Header("=== Cấu hình Độ khó ===")]
        [Tooltip("Giai đoạn độ khó mà đoạn đường này thuộc về: 1 (0-500m), 2 (500-1500m), 3 (>1500m)")]
        [Range(1, 3)]
        [SerializeField] private int difficultyLevel = 1;

        [Tooltip("Đoạn đường có chướng ngại vật nặng/dày đặc hay không (để tránh xuất hiện 2 đoạn nặng liên tiếp)")]
        [SerializeField] private bool isHeavy = false;

        [Tooltip("Đoạn đường an toàn, không có chướng ngại vật nguy hiểm (dùng cho khởi đầu game hoặc khoảng nghỉ)")]
        [SerializeField] private bool isSafe = false;

        #region Properties

        /// <summary>Chiều dài của đoạn đường tính theo mét</summary>
        public float SegmentLength => segmentLength;

        /// <summary>Cấp độ khó của đoạn đường (1, 2 hoặc 3)</summary>
        public int DifficultyLevel => difficultyLevel;

        /// <summary>Có phải là đoạn cạm bẫy nặng không</summary>
        public bool IsHeavy => isHeavy;

        /// <summary>Có phải là đoạn an toàn không</summary>
        public bool IsSafe => isSafe;

        /// <summary>Prefab gốc tạo ra đối tượng này (dùng để trả về đúng pool)</summary>
        public TrackSegment PrefabSource { get; set; }

        #endregion

        /// <summary>Sự kiện kích hoạt khi đoạn đường được tái sinh từ Object Pool</summary>
        public event Action OnResetSegment;

        // Lưu trữ trạng thái khởi đầu của tất cả các GameObject con (vật phẩm, chướng ngại) để khôi phục
        private struct ChildInitialState
        {
            public GameObject gameObject;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public bool initialActive;
        }

        private readonly List<ChildInitialState> childStates = new List<ChildInitialState>();
        private Collectible[] cachedCollectibles;
        private bool isInitialized = false;

        private void Awake()
        {
            CacheChildStates();
        }

        /// <summary>
        /// Ghi nhận toàn bộ vị trí, góc xoay và trạng thái active ban đầu của tất cả các đối tượng con.
        /// </summary>
        private void CacheChildStates()
        {
            if (isInitialized) return;

            childStates.Clear();
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            cachedCollectibles = GetComponentsInChildren<Collectible>(true);

            // Bỏ qua chính Transform gốc của đoạn đường (index 0)
            for (int i = 1; i < allChildren.Length; i++)
            {
                Transform child = allChildren[i];
                if (child != null)
                {
                    childStates.Add(new ChildInitialState
                    {
                        gameObject = child.gameObject,
                        localPosition = child.localPosition,
                        localRotation = child.localRotation,
                        initialActive = child.gameObject.activeSelf
                    });
                }
            }

            isInitialized = true;
        }

        /// <summary>
        /// Khôi phục toàn bộ vật phẩm (xu, tinh thể, power-up) và chướng ngại vật về vị trí và trạng thái ban đầu khi tái sử dụng từ Object Pool.
        /// </summary>
        public void ResetSegment()
        {
            if (!isInitialized)
            {
                CacheChildStates();
            }

            // Phục hồi trạng thái từng đối tượng con
            for (int i = 0; i < childStates.Count; i++)
            {
                ChildInitialState state = childStates[i];
                if (state.gameObject != null)
                {
                    state.gameObject.transform.localPosition = state.localPosition;
                    state.gameObject.transform.localRotation = state.localRotation;
                    state.gameObject.SetActive(state.initialActive);
                }
            }

            // Làm mới cờ thu thập của tất cả vật phẩm Collectible con
            if (cachedCollectibles != null)
            {
                for (int i = 0; i < cachedCollectibles.Length; i++)
                {
                    if (cachedCollectibles[i] != null)
                    {
                        cachedCollectibles[i].ResetItem();
                    }
                }
            }

            // Kích hoạt sự kiện cho các script gắn trên đối tượng con (MovingTrain, RollingBoulder) tự làm mới logic
            OnResetSegment?.Invoke();
        }

        #region Helper / Editor Methods

        /// <summary>
        /// Thiết lập cấu hình nhanh cho đoạn đường từ Editor hoặc script dựng cảnh.
        /// </summary>
        public void SetProperties(float length, int difficulty, bool heavy, bool safe)
        {
            segmentLength = length;
            difficultyLevel = Mathf.Clamp(difficulty, 1, 3);
            isHeavy = heavy;
            isSafe = safe;
        }

        #endregion
    }
}

