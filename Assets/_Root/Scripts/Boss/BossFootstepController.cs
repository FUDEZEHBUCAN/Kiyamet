using UnityEngine;

namespace _Root.Scripts.Boss
{
    /// <summary>
    /// Boss locomotion adım sesleri ve yakındaki oyuncular için hafif kamera sarsıntısı.
    /// Render tarafında mesafe biriktirerek adım tetikler.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossFootstepController : MonoBehaviour
    {
        [SerializeField] private BossAudioController bossAudio;
        [SerializeField] private Transform stepOrigin;
        [SerializeField] private float stepDistance = 2.15f;
        [SerializeField] private float rushStepDistanceMultiplier = 0.62f;
        [SerializeField] private float minMoveSpeed = 0.35f;

        private Vector3 _lastStepOrigin;
        private float _distanceSinceLastStep;
        private bool _hasLastStepOrigin;

        private void Awake()
        {
            if (bossAudio == null)
                bossAudio = GetComponent<BossAudioController>();

            if (bossAudio == null)
                bossAudio = GetComponentInChildren<BossAudioController>();

            if (stepOrigin == null)
                stepOrigin = transform;
        }

        public void Tick(bool enabled, bool isRushRun, BossCameraShakeSettings cameraShakeSettings)
        {
            if (!enabled)
            {
                ResetStepTracking();
                return;
            }

            Vector3 origin = stepOrigin != null ? stepOrigin.position : transform.position;
            if (!_hasLastStepOrigin)
            {
                _lastStepOrigin = origin;
                _hasLastStepOrigin = true;
                return;
            }

            Vector3 delta = origin - _lastStepOrigin;
            delta.y = 0f;
            _lastStepOrigin = origin;

            float frameDistance = delta.magnitude;
            if (frameDistance <= 0.0001f)
                return;

            float frameSpeed = Time.deltaTime > 0.0001f ? frameDistance / Time.deltaTime : 0f;
            if (frameSpeed < minMoveSpeed)
                return;

            float threshold = stepDistance * (isRushRun ? rushStepDistanceMultiplier : 1f);
            if (threshold <= 0.001f)
                return;

            _distanceSinceLastStep += frameDistance;
            while (_distanceSinceLastStep >= threshold)
            {
                _distanceSinceLastStep -= threshold;
                TriggerStep(origin, cameraShakeSettings);
            }
        }

        public void ResetStepTracking()
        {
            _distanceSinceLastStep = 0f;
            _hasLastStepOrigin = false;
        }

        private void TriggerStep(Vector3 worldPosition, BossCameraShakeSettings cameraShakeSettings)
        {
            bossAudio?.PlayFootstep();
            BossCameraShake.TryShakeLocalPlayer(BossCameraShakeType.Footstep, worldPosition, cameraShakeSettings);
        }
    }
}
