using UnityEngine;
using Fusion;

namespace _Root.Scripts.Interactable
{
    [RequireComponent(typeof(Rigidbody))]
    public class PushableRock : NetworkBehaviour, IInteractable
    {
        [Header("Rock Settings")]
        [SerializeField] private float pushForce = 500f; // Kaya ittirme kuvveti
        [SerializeField] private float rotationSpeed = 2f; // Kayanın dönme hızı
        [SerializeField] private float maxInteractionDistance = 4f; // Maksimum etkileşim mesafesi (bu mesafeden uzaklaşırsa etkileşim biter)
        
        [Header("Event Settings")]
        [SerializeField] private Transform targetPosition; // Kaya buraya getirildiğinde event tetiklenecek
        [SerializeField] private float targetDistanceThreshold = 1f; // Hedef mesafesi
        
        private Rigidbody _rigidbody;
        private Transform _currentInteractor;
        private bool _isBeingPushed;
        private Vector3 _initialInteractionPosition; // Etkileşim başladığında player'ın pozisyonu
        
        // Event için
        public System.Action OnRockPlaced; // Kaya hedef konuma getirildiğinde tetiklenecek
        
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = false;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; // Sadece Y ekseni etrafında dönebilir
        }
        
        public void OnInteractStart(Transform interactor)
        {
            if (!Object.HasStateAuthority)
                return;
                
            _currentInteractor = interactor;
            _isBeingPushed = true;
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
            
            // Kaya player'a doğru dönsün
            Vector3 lookDirection = (interactor.position - transform.position);
            lookDirection.y = 0f;
            if (lookDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Runner.DeltaTime);
            }
            
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
            
            // Kaya ittirilmiyorsa ve hareket ediyorsa, yavaşça durdur
            if (!_isBeingPushed && _rigidbody.velocity.magnitude > 0.1f)
            {
                _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, Vector3.zero, Runner.DeltaTime * 2f);
            }
        }
    }
}

