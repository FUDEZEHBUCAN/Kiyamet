using System.Collections;
using DG.Tweening;
using UnityEngine;
using _Root.Scripts.Controllers;
using _Root.Scripts.Enums;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Interactable
{
    [RequireComponent(typeof(Collider))]
    public class HiddenDoorTrigger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform hiddenDoor;

        [Header("Countdown")]
        [SerializeField] private float countdownDuration = 3f;

        [Header("Door Move")]
        [SerializeField] private float moveRightDistance = 3f;
        [SerializeField] private float moveDuration = 1.2f;
        [SerializeField] private Ease moveEase = Ease.OutCubic;
        [SerializeField] private bool triggerOnlyOnce = true;
        [SerializeField] private bool lockDoorAtFinalLocalPosition = true;
        
        [Header("Door Move Events")]
        [SerializeField] private GameObject objectToActivateOnDoorMove;
        
        [Header("Camera Shake")]
        [SerializeField] private bool shakeCameraDuringDoorMove = true;
        [SerializeField] private float shakeInterval = 0.12f;

        private bool _countdownStarted;
        private bool _doorMoved;
        private bool _isDoorMoving;
        private Tween _activeDoorTween;
        private Vector3 _finalLocalPosition;
        private bool _hasFinalLocalPosition;
        private Coroutine _shakeRoutine;
        private bool _sequenceTriggered;

        private void Reset()
        {
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[HiddenDoorTrigger] OnTriggerEnter: {other.name}");
            
            if (_doorMoved && triggerOnlyOnce)
            {
                Debug.Log("[HiddenDoorTrigger] Door already moved and triggerOnlyOnce is true. Ignoring.");
                return;
            }
            
            if (_isDoorMoving)
            {
                Debug.Log("[HiddenDoorTrigger] Door is already moving. Ignoring.");
                return;
            }
            
            if (_sequenceTriggered && triggerOnlyOnce)
            {
                Debug.Log("[HiddenDoorTrigger] Sequence already triggered once. Ignoring.");
                return;
            }

            if (_countdownStarted)
            {
                Debug.Log("[HiddenDoorTrigger] Countdown already running. Ignoring.");
                return;
            }

            NetworkPlayer player = other.GetComponentInParent<NetworkPlayer>();
            if (player == null || !player.IsAlive)
            {
                Debug.Log("[HiddenDoorTrigger] Entered collider is not a live NetworkPlayer.");
                return;
            }

            Debug.Log($"[HiddenDoorTrigger] Valid player detected: {player.name}. Countdown starting.");
            _countdownStarted = true;
            _sequenceTriggered = true;
            StartCoroutine(CountdownAndMoveDoor());
        }

        private IEnumerator CountdownAndMoveDoor()
        {
            Debug.Log($"[HiddenDoorTrigger] Countdown started. Duration: {countdownDuration:F2}s");
            if (countdownDuration > 0f)
                yield return new WaitForSeconds(countdownDuration);

            Debug.Log("[HiddenDoorTrigger] Countdown finished. Moving hidden door.");
            MoveDoorRightInLocal();
            _countdownStarted = false;
        }

        private void MoveDoorRightInLocal()
        {
            if (hiddenDoor == null)
            {
                Debug.LogWarning("[HiddenDoorTrigger] Hidden door reference is missing.");
                return;
            }
            
            Rigidbody doorRb = hiddenDoor.GetComponent<Rigidbody>();
            if (doorRb != null && !doorRb.isKinematic)
            {
                doorRb.isKinematic = true;
                doorRb.velocity = Vector3.zero;
                doorRb.angularVelocity = Vector3.zero;
                Debug.Log("[HiddenDoorTrigger] Door rigidbody was dynamic. Switched to kinematic for tween.");
            }

            Vector3 localLeftDirection;
            if (hiddenDoor.parent != null)
            {
                localLeftDirection = hiddenDoor.parent.InverseTransformDirection(-hiddenDoor.right).normalized;
            }
            else
            {
                localLeftDirection = (-hiddenDoor.right).normalized;
            }
            
            Vector3 targetLocalPosition = hiddenDoor.localPosition + localLeftDirection * moveRightDistance;
            Debug.Log($"[HiddenDoorTrigger] Door move start. Current local: {hiddenDoor.localPosition}, Target local: {targetLocalPosition}, Duration: {moveDuration:F2}");
            
            if (objectToActivateOnDoorMove != null && !objectToActivateOnDoorMove.activeSelf)
            {
                objectToActivateOnDoorMove.SetActive(true);
                Debug.Log($"[HiddenDoorTrigger] Activated object on move start: {objectToActivateOnDoorMove.name}");
            }
            
            _isDoorMoving = true;
            StartDoorMoveShake();

            _activeDoorTween?.Kill();
            _activeDoorTween = hiddenDoor.DOLocalMove(targetLocalPosition, moveDuration)
                .SetEase(moveEase)
                .OnComplete(() =>
                {
                    hiddenDoor.localPosition = targetLocalPosition;
                    _finalLocalPosition = targetLocalPosition;
                    _hasFinalLocalPosition = true;
                    _doorMoved = true;
                    _isDoorMoving = false;
                    StopDoorMoveShake();
                    Debug.Log($"[HiddenDoorTrigger] Door move completed. Final local: {hiddenDoor.localPosition}");
                })
                .OnKill(() =>
                {
                    if (_isDoorMoving)
                    {
                        _isDoorMoving = false;
                        StopDoorMoveShake();
                    }
                });
        }
        
        private void LateUpdate()
        {
            if (!lockDoorAtFinalLocalPosition || !_doorMoved || !_hasFinalLocalPosition || hiddenDoor == null)
                return;
            
            if (hiddenDoor.localPosition != _finalLocalPosition)
            {
                hiddenDoor.localPosition = _finalLocalPosition;
            }
        }
        
        private void StartDoorMoveShake()
        {
            if (!shakeCameraDuringDoorMove)
                return;
            
            if (_shakeRoutine != null)
                StopCoroutine(_shakeRoutine);
            
            _shakeRoutine = StartCoroutine(DoorMoveShakeRoutine());
        }
        
        private void StopDoorMoveShake()
        {
            if (_shakeRoutine == null)
            {
                if (TpsCameraController.Instance != null)
                    TpsCameraController.Instance.StopCameraShake();
                return;
            }
            
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
            
            if (TpsCameraController.Instance != null)
                TpsCameraController.Instance.StopCameraShake();
        }
        
        private IEnumerator DoorMoveShakeRoutine()
        {
            float interval = Mathf.Max(0.05f, shakeInterval);
            
            while (_isDoorMoving)
            {
                if (TpsCameraController.Instance != null)
                {
                    TpsCameraController.Instance.ShakeCamera(CameraShakeType.DoorBreak);
                }
                
                yield return new WaitForSeconds(interval);
            }
            
            _shakeRoutine = null;
        }
        
        private void OnDisable()
        {
            _isDoorMoving = false;
            StopDoorMoveShake();
            _activeDoorTween?.Kill();
        }
    }
}
