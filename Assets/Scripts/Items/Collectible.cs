using System;
using UnityEngine;

namespace CrystalRun
{
    /// <summary>
    /// Lớp cơ sở trừu tượng cho tất cả các vật phẩm thu thập được trong CrystalRun (Xu, Tinh thể, Power-Up):
    /// - Collider tự động đặt ở dạng Is Trigger.
    /// - Hiệu ứng hình ảnh nhấp nhô lên xuống (bobbing) và xoay tròn đều quanh trục Y.
    /// - Khi nhân vật chạm vào: gọi hàm trừu tượng OnCollected() rồi ẩn đối tượng (SetActive(false)), KHÔNG Destroy vì sử dụng Object Pool.
    /// - Hàm ResetItem() để khôi phục vị trí và kích hoạt lại khi đoạn đường được tái sử dụng.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class Collectible : MonoBehaviour
    {
        [Header("=== Cấu hình Chuyển động Hiển thị ===")]
        [Tooltip("Tốc độ tự xoay tròn quanh trục Y (độ/giây)")]
        [SerializeField] private float rotationSpeed = 90.0f;

        [Tooltip("Biên độ nhấp nhô lên xuống (mét)")]
        [SerializeField] private float bobAmplitude = 0.12f;

        [Tooltip("Tần số nhấp nhô lên xuống (chu kỳ/giây)")]
        [SerializeField] private float bobFrequency = 2.0f;

        #region Events

        /// <summary>Sự kiện kích hoạt khi vật phẩm này được nhặt</summary>
        public event Action OnItemCollected;

        #endregion

        protected Collider itemCollider;
        protected Vector3 initialLocalPosition;
        protected bool isCollected = false;

        private float bobRandomOffset;

        protected virtual void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        protected virtual void Awake()
        {
            itemCollider = GetComponent<Collider>();
            if (itemCollider != null)
            {
                itemCollider.isTrigger = true;
            }

            initialLocalPosition = transform.localPosition;
            // Tạo offset ngẫu nhiên để các vật phẩm gần nhau không bị nhấp nhô đồng loạt
            bobRandomOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        protected virtual void Update()
        {
            if (isCollected) return;

            AnimateVisual();
        }

        /// <summary>
        /// Thực thi hiệu ứng xoay tròn và nhấp nhô nhẹ.
        /// </summary>
        protected virtual void AnimateVisual()
        {
            // 1. Tự xoay quanh trục Y
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

            // 2. Nhấp nhô hình sin quanh vị trí Y ban đầu
            float newY = initialLocalPosition.y + Mathf.Sin((Time.time * bobFrequency) + bobRandomOffset) * bobAmplitude;
            transform.localPosition = new Vector3(initialLocalPosition.x, newY, initialLocalPosition.z);
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (isCollected) return;

            if (other.CompareTag("Player"))
            {
                isCollected = true;
                OnCollected(other.gameObject);
                OnItemCollected?.Invoke();

                // Ẩn đối tượng để chờ đoạn đường thu hồi vào Pool, không dùng Destroy
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Logic xử lý cộng điểm, hồi máu hoặc kích hoạt hiệu ứng khi người chơi chạm vào vật phẩm.
        /// </summary>
        /// <param name="player">GameObject của nhân vật chính</param>
        protected abstract void OnCollected(GameObject player);

        /// <summary>
        /// Khôi phục trạng thái và bật lại vật phẩm khi TrackSegment được tái sử dụng từ Object Pool.
        /// </summary>
        public virtual void ResetItem()
        {
            isCollected = false;
            transform.localPosition = initialLocalPosition;
            gameObject.SetActive(true);
        }
    }
}

