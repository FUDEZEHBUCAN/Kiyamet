using UnityEngine;
using Fusion;

namespace _Root.Scripts.Interactable
{
    [RequireComponent(typeof(Rigidbody))]
    public class PushableRock : NetworkBehaviour, IInteractable
    {
        [Header("Rock Settings")]
        [SerializeField] private float pushForce = 500f; // Kaya ittirme kuvveti
        [SerializeField] private float maxInteractionDistance = 4f; // Maksimum etkileşim mesafesi (bu mesafeden uzaklaşırsa etkileşim biter)
        
        [Header("Trigger Settings")]
        [SerializeField] private float triggerForce = 300f; // Trigger'da uygulanacak sabit force
        
        [Header("Event Settings")]
        [SerializeField] private Transform targetPosition; // Kaya buraya getirildiğinde event tetiklenecek
        [SerializeField] private float targetDistanceThreshold = 1f; // Hedef mesafesi
        
        private Rigidbody _rigidbody;
        private Transform _currentInteractor;
        private bool _isBeingPushed;
        private bool _hasInteracted; // En az bir kere etkileşime geçti mi
        private Vector3 _initialInteractionPosition; // Etkileşim başladığında player'ın pozisyonu
        
        // Trigger için
        private Transform _activeTrigger; // Aktif trigger objesi
        private bool _isInTrigger; // Trigger içinde mi
        
        // Event için
        public System.Action OnRockPlaced; // Kaya hedef konuma getirildiğinde tetiklenecek
        
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = false;
            // FreezeRotation None olarak kalacak - tüm eksenlerde dönebilir
        }
        
        public void OnInteractStart(Transform interactor)
        {
            if (!Object.HasStateAuthority)
                return;
                
            _currentInteractor = interactor;
            _isBeingPushed = true;
            _hasInteracted = true; // En az bir kere etkileşime geçti
            _initialInteractionPosition = interactor.position;
        }
        
        public void OnInteractEnd(Transform interactor)
        {
            if (!Object.HasStateAuthority)
                return;
                
            _currentInteractor = null;
            _isBeingPushed = false;
            
            // Kaya mevcut momentumuyla devam edecek, yavaşça duracak (FixedUpdateNetwork'te handle ediliyor)
        }
        
        public void OnInteractUpdate(Transform interactor)
        {
            if (!Object.HasStateAuthority || !_isBeingPushed || _currentInteractor == null)
                return;
            
            // Player'ın baktığı yönü al (forward direction)
            Vector3 pushDirection = interactor.forward;
            pushDirection.y = 0f; // Sadece yatay yön
            pushDirection.Normalize();
            
            // Kaya'ya force uygula (player'ın baktığı yöne)
            Vector3 force = pushDirection * pushForce * Runner.DeltaTime;
            _rigidbody.AddForce(force, ForceMode.Force);
            
            // Rotasyon doğal fizik motoruna bırakıldı - manuel rotasyon yok
            
            // Hedef konuma ulaşıldı mı kontrol et
            if (targetPosition != null)
            {
                float distance = Vector3.Distance(transform.position, targetPosition.position);
                if (distance <= targetDistanceThreshold)
                {
                    OnRockPlaced?.Invoke();
                }
            }
        }
        
        /// <summary>
        /// Player kayadan çok uzaklaştı mı kontrol et
        /// </summary>
        public bool ShouldEndInteraction(Transform interactor)
        {
            if (interactor == null)
                return true;
            
            float distance = Vector3.Distance(interactor.position, transform.position);
            return distance > maxInteractionDistance;
        }
        
        public bool CanInteract(Transform interactor)
        {
            // Her zaman etkileşime girebilir (başka bir oyuncu ittiriyorsa false dönebilir)
            return !_isBeingPushed || _currentInteractor == interactor;
        }
        
        public override void FixedUpdateNetwork()
        {
            // Networked update - sadece server
            if (!Object.HasStateAuthority)
                return;
            
            // Trigger içindeyse ve etkileşim halindeyse veya daha önce etkileşime geçtiyse
            if (_isInTrigger && _activeTrigger != null && (_isBeingPushed || _hasInteracted))
            {
                // Trigger objesinin forward yönünde sabit force uygula
                Vector3 triggerDirection = _activeTrigger.forward;
                triggerDirection.y = 0f; // Sadece yatay yön
                triggerDirection.Normalize();
                
                Vector3 force = triggerDirection * triggerForce * Runner.DeltaTime;
                _rigidbody.AddForce(force, ForceMode.Force);
            }
            
            // Kaya ittirilmiyorsa ve hareket ediyorsa, yavaşça durdur
            if (!_isBeingPushed && !_isInTrigger && _rigidbody.velocity.magnitude > 0.1f)
            {
                _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, Vector3.zero, Runner.DeltaTime * 2f);
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!Object.HasStateAuthority)
                return;
            
            // BreakableDoor kontrolü - kapıyı kır
            BreakableDoor door = other.GetComponent<BreakableDoor>();
            if (door != null && (_isBeingPushed || _hasInteracted))
            {
                door.BreakDoor();
            }
            
            // "RockTrigger" adlı trigger'a girdi mi kontrol et
            if (other.CompareTag("RockTrigger") || other.name.Contains("RockTrigger"))
            {
                _isInTrigger = true;
                _activeTrigger = other.transform;
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (!Object.HasStateAuthority)
                return;
            
            // "RockTrigger" adlı trigger'dan çıktı mı kontrol et
            if (other.CompareTag("RockTrigger") || other.name.Contains("RockTrigger"))
            {
                _isInTrigger = false;
                _activeTrigger = null;
            }
        }
    }
}

