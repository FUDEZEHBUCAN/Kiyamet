using Fusion;
using UnityEngine;
using _Root.Scripts.Controllers;
using _Root.Scripts.Enums;

namespace _Root.Scripts.Interactable
{
    public enum HiddenDoorState : byte
    {
        Idle = 0,
        Countdown = 1,
        Moving = 2,
        Complete = 3
    }

    [RequireComponent(typeof(NetworkObject))]
    public class HiddenDoorTrigger : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform hiddenDoor;

        [Header("Countdown")]
        [SerializeField] private float countdownDuration = 3f;

        [Header("Door Move")]
        [SerializeField] private float moveRightDistance = 3f;
        [SerializeField] private float moveDuration = 1.2f;
        [SerializeField] private bool triggerOnlyOnce = true;
        [SerializeField] private bool lockDoorAtFinalLocalPosition = true;

        [Header("Door Move Events")]
        [SerializeField] private GameObject objectToActivateOnDoorMove;

        [Header("Camera Shake")]
        [SerializeField] private bool shakeCameraDuringDoorMove = true;
        [SerializeField] private float shakeInterval = 0.12f;

        [Networked] private HiddenDoorState DoorState { get; set; }
        [Networked] private TickTimer CountdownTimer { get; set; }
        [Networked] private float MoveStartTime { get; set; }
        [Networked] private Vector3 DoorStartLocalPosition { get; set; }
        [Networked] private Vector3 DoorTargetLocalPosition { get; set; }

        private HiddenDoorState _lastRenderedDoorState;
        private bool _moveEffectsStarted;
        private float _nextShakeTime;

        public void TryTriggerDoorSequence()
        {
            if (Object != null && Object.IsValid && !Object.HasStateAuthority)
                return;

            if (DoorState == HiddenDoorState.Complete && triggerOnlyOnce)
                return;

            if (DoorState == HiddenDoorState.Moving)
                return;

            if (DoorState == HiddenDoorState.Countdown)
                return;

            if (DoorState != HiddenDoorState.Idle && triggerOnlyOnce)
                return;

            DoorState = HiddenDoorState.Countdown;
            CountdownTimer = TickTimer.CreateFromSeconds(Runner, countdownDuration);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            if (DoorState == HiddenDoorState.Countdown && CountdownTimer.Expired(Runner))
            {
                BeginDoorMoveAuthority();
            }

            if (DoorState == HiddenDoorState.Moving)
            {
                float elapsed = Runner.SimulationTime - MoveStartTime;
                if (elapsed >= moveDuration)
                    CompleteDoorMoveAuthority();
            }
        }

        public override void Render()
        {
            ApplyDoorVisuals();
        }

        private void BeginDoorMoveAuthority()
        {
            if (hiddenDoor == null)
            {
                Debug.LogWarning("[HiddenDoorTrigger] Hidden door reference is missing.");
                return;
            }

            PrepareDoorRigidbodyForTween();

            DoorStartLocalPosition = hiddenDoor.localPosition;
            DoorTargetLocalPosition = DoorStartLocalPosition + GetDoorLocalMoveDelta();
            MoveStartTime = Runner.SimulationTime;
            DoorState = HiddenDoorState.Moving;
        }

        private void CompleteDoorMoveAuthority()
        {
            if (hiddenDoor != null)
                hiddenDoor.localPosition = DoorTargetLocalPosition;

            DoorState = HiddenDoorState.Complete;
        }

        private void ApplyDoorVisuals()
        {
            if (hiddenDoor == null)
                return;

            switch (DoorState)
            {
                case HiddenDoorState.Moving:
                    float t = moveDuration > 0.001f
                        ? Mathf.Clamp01((Runner.SimulationTime - MoveStartTime) / moveDuration)
                        : 1f;
                    hiddenDoor.localPosition = Vector3.Lerp(
                        DoorStartLocalPosition,
                        DoorTargetLocalPosition,
                        EaseOutCubic(t));
                    break;

                case HiddenDoorState.Complete:
                    if (lockDoorAtFinalLocalPosition)
                        hiddenDoor.localPosition = DoorTargetLocalPosition;
                    break;
            }

            if (DoorState == HiddenDoorState.Moving && _lastRenderedDoorState != HiddenDoorState.Moving)
                BeginMoveEffectsClient();

            if (DoorState != HiddenDoorState.Moving)
            {
                _moveEffectsStarted = false;
                if (_lastRenderedDoorState == HiddenDoorState.Moving)
                    StopDoorMoveShake();
            }
            else if (_moveEffectsStarted && shakeCameraDuringDoorMove)
            {
                UpdateDoorMoveShake();
            }

            _lastRenderedDoorState = DoorState;
        }

        private void BeginMoveEffectsClient()
        {
            _moveEffectsStarted = true;
            _nextShakeTime = Time.unscaledTime;

            if (objectToActivateOnDoorMove != null && !objectToActivateOnDoorMove.activeSelf)
                objectToActivateOnDoorMove.SetActive(true);

            if (shakeCameraDuringDoorMove && TpsCameraController.Instance != null)
                TpsCameraController.Instance.ShakeCamera(CameraShakeType.DoorBreak);
        }

        private void UpdateDoorMoveShake()
        {
            float interval = Mathf.Max(0.05f, shakeInterval);
            if (Time.unscaledTime < _nextShakeTime)
                return;

            _nextShakeTime = Time.unscaledTime + interval;
            if (TpsCameraController.Instance != null)
                TpsCameraController.Instance.ShakeCamera(CameraShakeType.DoorBreak);
        }

        private void StopDoorMoveShake()
        {
            if (TpsCameraController.Instance != null)
                TpsCameraController.Instance.StopCameraShake();
        }

        private Vector3 GetDoorLocalMoveDelta()
        {
            Vector3 localLeftDirection;
            if (hiddenDoor.parent != null)
                localLeftDirection = hiddenDoor.parent.InverseTransformDirection(-hiddenDoor.right).normalized;
            else
                localLeftDirection = (-hiddenDoor.right).normalized;

            return localLeftDirection * moveRightDistance;
        }

        private void PrepareDoorRigidbodyForTween()
        {
            Rigidbody doorRb = hiddenDoor.GetComponent<Rigidbody>();
            if (doorRb == null || doorRb.isKinematic)
                return;

            doorRb.isKinematic = true;
            doorRb.velocity = Vector3.zero;
            doorRb.angularVelocity = Vector3.zero;
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        private void OnDisable()
        {
            StopDoorMoveShake();
        }
    }
}
