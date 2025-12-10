using UnityEngine;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;
namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// Tamamen local çalışan, üçüncü şahıs kamera takip scripti.
    /// Network component gerektirmez; sadece local oyuncuyu takip eder.
    /// </summary>
    public class TpsCameraController : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;                 // Takip edilecek oyuncu (NetworkPlayer)

        [Header("Offset")]
        public float distance = 4f;              // Hedeften geri mesafe
        public float height = 2f;                // Hedeften yukarı mesafe

        [Header("Mouse")]
        // Yatay (yaw) karakter tarafından kontrol ediliyor; kamera sadece dikey ekseni (pitch) mouse ile kontrol eder
        public float mouseYSensitivity = 2f;
        public Vector2 pitchLimits = new Vector2(-40f, 80f);

        private float _yaw;   // Yatay açı (sağa-sola)
        private float _pitch; // Dikey açı (yukarı-aşağı)

        private void Start()
        {
            // Eğer target atanmadıysa local player'dan al
            if (target == null && NetworkPlayer.Local != null)
            {
                target = NetworkPlayer.Local.transform;
            }

            var angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            // Local player spawn olduysa target'ı at
            if (target == null && NetworkPlayer.Local != null)
            {
                target = NetworkPlayer.Local.transform;
            }
            if (target == null) 
                return;

            // Mouse input: sadece dikey (pitch) kamera tarafından kontrol ediliyor
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y") * mouseYSensitivity;

            _pitch -= mouseY;
            _pitch  = Mathf.Clamp(_pitch, pitchLimits.x, pitchLimits.y);

            // Yatay açı her zaman karakterin Y rotasyonundan alınır; kamera ve karakter senkron kalır
            _yaw = target.eulerAngles.y;

            // Kamera rotasyonu
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.rotation = rotation;

            // Hedef etrafında konum (ekstra smoothing yok, direkt takip)
            Vector3 desiredOffset = new Vector3(0f, height, -distance);
            Vector3 desiredPos = target.position + rotation * desiredOffset;
            transform.position = desiredPos;
        }
    }
}


