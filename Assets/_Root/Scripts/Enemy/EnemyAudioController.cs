using UnityEngine;

namespace _Root.Scripts.Enemy
{
    /// <summary>
    /// Enemy'ler için audio controller - saldırı, leap, hasar alma, ölüm sesleri
    /// </summary>
    public class EnemyAudioController : MonoBehaviour
    {
        [Header("Attack Sounds")]
        [SerializeField] private AudioClip[] attackSwingSounds;
        [SerializeField] private AudioClip[] attackHitSounds;

        [Header("Leap Attack")]
        [SerializeField] private AudioClip[] leapWindupSounds;
        [SerializeField] private AudioClip[] leapJumpSounds;
        [SerializeField] private AudioClip[] leapLandSounds;
        [SerializeField] private AudioClip[] leapHitSounds;
        [SerializeField] [Range(0f, 1f)] private float leapWindupVolume = 0.75f;
        [SerializeField] [Range(0f, 1f)] private float leapJumpVolume = 0.85f;
        [SerializeField] [Range(0f, 1f)] private float leapLandVolume = 0.9f;
        [SerializeField] [Range(0f, 1f)] private float leapHitVolume = 0.85f;

        [Header("Damage Sounds")]
        [SerializeField] private AudioClip[] takeDamageSounds;

        [Header("Death Sounds")]
        [SerializeField] private AudioClip[] deathSounds;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 28f;
        [SerializeField] private float minPitch = 0.92f;
        [SerializeField] private float maxPitch = 1.08f;
        [SerializeField] private float pitchVariance = 0.06f;

        public bool HasLeapWindupSounds => HasClips(leapWindupSounds);
        public bool HasLeapJumpSounds => HasClips(leapJumpSounds);
        public bool HasLeapLandSounds => HasClips(leapLandSounds);
        public bool HasLeapHitSounds => HasClips(leapHitSounds);

        private void Awake()
        {
            EnsureAudioSource();
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.dopplerLevel = 0.35f;
            audioSource.volume = volume;
        }

        public void PlayAttackSwing()
        {
            PlayRandom(attackSwingSounds, volume);
        }

        public void PlayAttackHit()
        {
            PlayRandom(attackHitSounds, volume);
        }

        public void PlayLeapWindup()
        {
            PlayRandom(leapWindupSounds, leapWindupVolume);
        }

        public void PlayLeapJump()
        {
            PlayRandom(leapJumpSounds, leapJumpVolume);
        }

        public void PlayLeapLand()
        {
            PlayRandom(leapLandSounds, leapLandVolume);
        }

        public void PlayLeapHit()
        {
            if (!PlayRandom(leapHitSounds, leapHitVolume))
                PlayAttackHit();
        }

        public void PlayTakeDamage()
        {
            PlayRandom(takeDamageSounds, volume);
        }

        public void PlayDeath()
        {
            PlayRandom(deathSounds, volume, pitchVarianceOverride: 0.04f);
        }

        private bool PlayRandom(AudioClip[] clips, float clipVolume, float pitchVarianceOverride = -1f)
        {
            if (audioSource == null || !HasClips(clips))
                return false;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
                return false;

            float spread = pitchVarianceOverride >= 0f ? pitchVarianceOverride : pitchVariance;
            audioSource.pitch = Random.Range(minPitch - spread, maxPitch + spread);
            audioSource.PlayOneShot(clip, Mathf.Clamp01(clipVolume));
            return true;
        }

        private static bool HasClips(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
                return false;

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                    return true;
            }

            return false;
        }
    }
}
