using UnityEngine;
using _Root.Scripts.Controllers;

namespace _Root.Scripts.Boss
{
    /// <summary>
    /// Tepegöz boss sesleri — ambient growl, saldırı kararı, özel saldırılar, taşlaşma vb.
    /// </summary>
    public class BossAudioController : MonoBehaviour
    {
        [Header("Ambient")]
        [SerializeField] private AudioClip[] ambientGrowlSounds;
        [SerializeField] private float growlMinInterval = 9f;
        [SerializeField] private float growlMaxInterval = 20f;
        [SerializeField] [Range(0f, 1f)] private float ambientVolume = 0.55f;

        [Header("Aggro / Karar")]
        [SerializeField] private AudioClip[] aggroRoarSounds;
        [SerializeField] private AudioClip[] attackDecideSounds;
        [SerializeField] [Range(0f, 1f)] private float aggroVolume = 0.85f;

        [Header("Melee")]
        [SerializeField] private AudioClip[] normalAttackSounds;
        [SerializeField] private AudioClip[] heavyAttackSounds;
        [SerializeField] private AudioClip[] attackHitSounds;
        [SerializeField] [Range(0f, 1f)] private float meleeVolume = 0.9f;

        [Header("Hasar / Ölüm")]
        [SerializeField] private AudioClip[] takeDamageSounds;
        [SerializeField] private AudioClip[] deathSounds;
        [SerializeField] [Range(0f, 1f)] private float painVolume = 0.75f;

        [Header("Taşlaşma")]
        [SerializeField] private AudioClip[] petrifySounds;
        [SerializeField] [Range(0f, 1f)] private float petrifyVolume = 0.95f;

        [Header("Uyku")]
        [SerializeField] private AudioClip sleepingSound;
        [SerializeField] [Range(0f, 1f)] private float sleepingVolume = 0.45f;
        [SerializeField] private bool sleepingLoop = true;
        [SerializeField] private float sleepingMinDistance = 4f;
        [SerializeField] private float sleepingMaxDistance = 16f;

        [Header("Uyanış")]
        [SerializeField] private AudioClip[] wakeUpSounds;
        [SerializeField] [Range(0f, 1f)] private float wakeUpVolume = 0.9f;
        [SerializeField] private AudioClip[] wakeLightStartSounds;
        [SerializeField] [Range(0f, 1f)] private float wakeLightStartVolume = 0.75f;

        [Header("Jump Attack")]
        [SerializeField] private AudioClip[] jumpWindupSounds;
        [SerializeField] private AudioClip[] jumpLeapSounds;
        [SerializeField] private AudioClip[] jumpLandSounds;
        [SerializeField] [Range(0f, 1f)] private float jumpVolume = 0.9f;

        [Header("Laser")]
        [SerializeField] private AudioClip[] laserChargeSounds;
        [SerializeField] private AudioClip[] laserBeamSounds;
        [SerializeField] private AudioClip[] laserHitSounds;
        [SerializeField] [Range(0f, 1f)] private float laserVolume = 0.88f;

        [Header("Rush")]
        [SerializeField] private AudioClip[] rushWindupSounds;
        [SerializeField] private AudioClip[] rushRunSounds;
        [SerializeField] private AudioClip[] rushStrikeSounds;
        [SerializeField] private AudioClip[] rushHitSounds;
        [SerializeField] [Range(0f, 1f)] private float rushVolume = 0.9f;

        [Header("Adım")]
        [SerializeField] private AudioClip[] footstepSounds;
        [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.82f;

        [Header("Sources")]
        [SerializeField] private AudioSource primarySource;
        [SerializeField] private AudioSource secondarySource;
        [SerializeField] private AudioSource sleepingSource;
        [SerializeField] private float minPitch = 0.92f;
        [SerializeField] private float maxPitch = 1.08f;
        [SerializeField] private float minDistance = 6f;
        [SerializeField] private float maxDistance = 55f;

        private float _growlCooldown;
        private bool _sleepingSoundActive;

        private void Awake()
        {
            EnsureAudioSources();
        }

        private void EnsureAudioSources()
        {
            if (primarySource == null)
            {
                primarySource = GetComponent<AudioSource>();
                if (primarySource == null)
                    primarySource = gameObject.AddComponent<AudioSource>();
            }

            ConfigureSource(primarySource);

            if (secondarySource == null)
            {
                secondarySource = gameObject.AddComponent<AudioSource>();
                ConfigureSource(secondarySource);
            }
        }

        private void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0.35f;
        }

        public void PlayEvent(BossAudioEventType eventType)
        {
            if (eventType == BossAudioEventType.None)
                return;

            EnsureAudioSources();

            switch (eventType)
            {
                case BossAudioEventType.AggroRoar:
                    PlayRandom(aggroRoarSounds, primarySource, aggroVolume);
                    break;
                case BossAudioEventType.AttackDecide:
                    PlayRandom(attackDecideSounds, primarySource, aggroVolume);
                    break;
                case BossAudioEventType.NormalAttack:
                    PlayRandom(normalAttackSounds, primarySource, meleeVolume);
                    break;
                case BossAudioEventType.HeavyAttack:
                    PlayRandom(heavyAttackSounds, primarySource, meleeVolume * 1.05f);
                    break;
                case BossAudioEventType.AttackHit:
                    PlayRandom(attackHitSounds, secondarySource, meleeVolume);
                    break;
                case BossAudioEventType.TakeDamage:
                    PlayRandom(takeDamageSounds, primarySource, painVolume);
                    break;
                case BossAudioEventType.Death:
                    PlayRandom(deathSounds, primarySource, 1f, pitchVariance: 0.04f);
                    break;
                case BossAudioEventType.Petrify:
                    PlayAll(petrifySounds, primarySource, petrifyVolume, pitchVariance: 0.03f);
                    break;
                case BossAudioEventType.WakeUp:
                    PlayAll(wakeUpSounds, primarySource, wakeUpVolume, pitchVariance: 0.04f);
                    break;
                case BossAudioEventType.WakeLightStart:
                    PlayAll(wakeLightStartSounds, secondarySource, wakeLightStartVolume, pitchVariance: 0.03f);
                    break;
                case BossAudioEventType.JumpWindup:
                    PlayRandom(jumpWindupSounds, primarySource, jumpVolume * 0.85f);
                    break;
                case BossAudioEventType.JumpLeap:
                    PlayRandom(jumpLeapSounds, primarySource, jumpVolume);
                    break;
                case BossAudioEventType.JumpLand:
                    PlayRandom(jumpLandSounds, secondarySource, jumpVolume * 1.1f);
                    break;
                case BossAudioEventType.LaserCharge:
                    PlayRandom(laserChargeSounds, primarySource, laserVolume * 0.8f);
                    break;
                case BossAudioEventType.LaserBeam:
                    PlayRandom(laserBeamSounds, primarySource, laserVolume);
                    break;
                case BossAudioEventType.LaserHit:
                    PlayRandom(laserHitSounds, secondarySource, laserVolume * 0.75f);
                    break;
                case BossAudioEventType.RushWindup:
                    PlayRandom(rushWindupSounds, primarySource, rushVolume);
                    break;
                case BossAudioEventType.RushRun:
                    PlayRandom(rushRunSounds, primarySource, rushVolume * 0.7f);
                    break;
                case BossAudioEventType.RushStrike:
                    PlayRandom(rushStrikeSounds, primarySource, rushVolume);
                    break;
                case BossAudioEventType.RushHit:
                    PlayRandom(rushHitSounds, secondarySource, rushVolume);
                    break;
            }
        }

        /// <summary>Yerel ambient growl — tüm client'larda Render'da çağrılır.</summary>
        public void TickAmbientGrowl(bool enabled)
        {
            if (!enabled || ambientGrowlSounds == null || ambientGrowlSounds.Length == 0)
                return;

            EnsureAudioSources();
            _growlCooldown -= Time.deltaTime;
            if (_growlCooldown > 0f)
                return;

            PlayRandom(ambientGrowlSounds, primarySource, ambientVolume, pitchVariance: 0.08f);
            _growlCooldown = Random.Range(growlMinInterval, growlMaxInterval);
        }

        public void ResetAmbientGrowlTimer()
        {
            _growlCooldown = Random.Range(growlMinInterval * 0.5f, growlMaxInterval * 0.65f);
        }

        public void PlayFootstep()
        {
            EnsureAudioSources();
            PlayRandom(footstepSounds, secondarySource, footstepVolume, pitchVariance: 0.1f);
        }

        /// <summary>Uyku ambient — tüm client'larda Render'da senkronize edilir.</summary>
        public void SetSleepingSoundActive(bool active)
        {
            if (sleepingSound == null)
                return;

            EnsureAudioSources();
            EnsureSleepingSource();

            if (active == _sleepingSoundActive)
                return;

            _sleepingSoundActive = active;
            if (active)
            {
                ConfigureSleepingSource();
                sleepingSource.clip = sleepingSound;
                sleepingSource.volume = sleepingVolume;
                sleepingSource.loop = sleepingLoop;
                sleepingSource.pitch = Random.Range(minPitch, maxPitch);
                sleepingSource.Play();
                return;
            }

            sleepingSource.Stop();
            sleepingSource.clip = null;
        }

        private void EnsureSleepingSource()
        {
            if (sleepingSource == null)
                sleepingSource = gameObject.AddComponent<AudioSource>();

            ConfigureSleepingSource();
        }

        private void ConfigureSleepingSource()
        {
            if (sleepingSource == null)
                return;

            sleepingSource.playOnAwake = false;
            SpatialAudioUtility.ConfigureAs3D(
                sleepingSource,
                sleepingMinDistance,
                sleepingMaxDistance);
            sleepingSource.dopplerLevel = 0f;
        }

        private void PlayRandom(AudioClip[] clips, AudioSource source, float volume, float pitchVariance = 0.06f)
        {
            if (source == null || clips == null || clips.Length == 0)
                return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
                return;

            float pitchSpread = Mathf.Max(0f, pitchVariance);
            source.pitch = Random.Range(minPitch - pitchSpread, maxPitch + pitchSpread);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void PlayAll(AudioClip[] clips, AudioSource source, float volume, float pitchVariance = 0.06f)
        {
            if (source == null || clips == null || clips.Length == 0)
                return;

            float clampedVolume = Mathf.Clamp01(volume);
            float pitchSpread = Mathf.Max(0f, pitchVariance);

            foreach (AudioClip clip in clips)
            {
                if (clip == null)
                    continue;

                source.pitch = Random.Range(minPitch - pitchSpread, maxPitch + pitchSpread);
                source.PlayOneShot(clip, clampedVolume);
            }
        }
    }
}
