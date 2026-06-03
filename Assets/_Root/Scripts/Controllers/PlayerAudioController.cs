using UnityEngine;

namespace _Root.Scripts.Controllers
{
    [System.Serializable]
    public sealed class MeleeQueueSoundGroup
    {
        public AudioClip[] clips;

        [Tooltip("PlayOneShot çarpanı (0 = sessiz, 1 = tam, >1 kuvvetlendirilmiş).")]
        [Range(0f, 2f)] public float volume = 1f;

        [Tooltip("Her çalışmada pitch = 1 ± bu değer (ör. 0.18 → yaklaşık 0.82–1.18).")]
        [Range(0f, 0.45f)] public float pitchVariation = 0.18f;
    }

    [RequireComponent(typeof(AudioSource))]
    public class PlayerAudioController : MonoBehaviour
    {
        [Header("Audio Source")]
        [SerializeField] private AudioSource audioSource;
        
        [Header("Attack Sounds")]
        [SerializeField] private AudioClip[] meleeSwingSounds;
        [SerializeField] private AudioClip[] meleeHitSounds;
        
        [Header("Damage Sounds")]
        [SerializeField] private AudioClip[] takeDamageSounds;
        [SerializeField] private AudioClip[] deathSounds;
        
        [Header("Block Sounds")]
        [SerializeField] private AudioClip[] blockSounds;
        
        [Header("Dash Sounds")]
        [SerializeField] private AudioClip[] dashSounds;
        [SerializeField] private AudioClip[] dashHitSounds;
        [SerializeField] private AudioClip[] shadowDashSounds;

        [Header("Ultimate Sounds")]
        [SerializeField] private AudioClip[] mirageStepActivateSounds;
        [SerializeField] private AudioClip[] mirageStepMoveSounds;

        [Header("Global (2D) ses")]
        [Tooltip("Mesafe bağımsız sesler için. Boş bırakılırsa otomatik oluşturulur.")]
        [SerializeField] private AudioSource globalAudioSource;

        [Header("Melee Queue (HUD)")]
        [SerializeField] private MeleeQueueSoundGroup meleeQueueWindowOpen = new();
        [SerializeField] private MeleeQueueSoundGroup meleeQueueAccepted = new();
        [SerializeField] private MeleeQueueSoundGroup meleeQueueChainStart = new();

        [Tooltip("Queue bildirimleri 2D global kaynak üzerinden çalınır (HUD geri bildirimi). Kapalıysa karakter AudioSource kullanılır.")]
        [SerializeField] private bool meleeQueueUseGlobalAudio = true;
        
        [Header("Settings")]
        [Tooltip("Her çalışmada pitch = 1 ± bu değer (ör. 0.22 → yaklaşık 0.78–1.22).")]
        [SerializeField] [Range(0f, 0.45f)] private float pitchVariation = 0.22f;
        [SerializeField] private float minTimeBetweenSounds = 0.05f;

        [Header("3D ses")]
        [SerializeField] private bool use3DSound = true;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 18f;
        
        private float _lastSwingSoundTime;
        private float _lastHitSoundTime;
        private float _lastGeneralSoundTime;
        private float _lastMirageMoveSoundTime;
        private float _lastQueueSoundTime;
        
        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            audioSource.playOnAwake = false;
            ApplySpatialSettings();
            EnsureGlobalAudioSource();
        }

        private void OnEnable()
        {
            ApplySpatialSettings();
        }

        private void ApplySpatialSettings()
        {
            if (audioSource == null)
                return;

            if (use3DSound)
                SpatialAudioUtility.ConfigureAs3D(audioSource, minDistance, maxDistance);
            else
                SpatialAudioUtility.ConfigureAs2D(audioSource);
        }
        
        public void PlayMeleeSwing()
        {
            PlayRandomSound(meleeSwingSounds, ref _lastSwingSoundTime);
        }
        
        public void PlayMeleeHit()
        {
            PlayRandomSound(meleeHitSounds, ref _lastHitSoundTime);
        }
        
        public void PlayTakeDamage()
        {
            PlayRandomSound(takeDamageSounds, ref _lastGeneralSoundTime);
        }
        
        public void PlayDeath()
        {
            PlayRandomSound(deathSounds, ref _lastGeneralSoundTime);
        }
        
        public void PlayBlock()
        {
            PlayRandomSound(blockSounds, ref _lastGeneralSoundTime);
        }
        
        public void PlayDash()
        {
            PlayRandomSound(dashSounds, ref _lastGeneralSoundTime);
        }

        public void PlayShadowDash()
        {
            if (shadowDashSounds != null && shadowDashSounds.Length > 0)
                PlayRandomSound(shadowDashSounds, ref _lastGeneralSoundTime);
            else
                PlayDash();
        }

        public void PlayDashHit()
        {
            PlayRandomSound(dashHitSounds, ref _lastGeneralSoundTime);
        }

        /// <summary>Mirage Step ulti başlangıcı — 2D, haritadaki tüm oyuncular duyar.</summary>
        public void PlayMirageStepActivateGlobal()
        {
            EnsureGlobalAudioSource();
            PlayRandomSound(mirageStepActivateSounds, ref _lastGeneralSoundTime, globalAudioSource);
        }

        /// <summary>Mirage Step hedefe sıçrama — 3D karakter kaynağı.</summary>
        public void PlayMirageStepMove()
        {
            PlayRandomSound(mirageStepMoveSounds, ref _lastMirageMoveSoundTime);
        }

        public void PlayMeleeQueueWindowOpen() => PlayQueueSound(meleeQueueWindowOpen);

        public void PlayMeleeQueueAccepted() => PlayQueueSound(meleeQueueAccepted);

        public void PlayMeleeQueueChainStart() => PlayQueueSound(meleeQueueChainStart);
        
        private void PlayRandomSound(AudioClip[] clips, ref float lastPlayedTime, AudioSource source = null)
        {
            if (clips == null || clips.Length == 0)
                return;

            source ??= audioSource;
            if (source == null)
                return;
            
            if (Time.time - lastPlayedTime < minTimeBetweenSounds)
                return;
            
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            
            if (clip == null)
                return;
            
            source.pitch = SampleRandomPitch(pitchVariation);
            source.PlayOneShot(clip);
            
            lastPlayedTime = Time.time;
        }

        private void PlayQueueSound(MeleeQueueSoundGroup group)
        {
            if (group?.clips == null || group.clips.Length == 0 || group.volume <= 0.001f)
                return;

            AudioSource source = ResolveQueueAudioSource();
            if (source == null)
                return;

            if (Time.time - _lastQueueSoundTime < minTimeBetweenSounds)
                return;

            AudioClip clip = group.clips[Random.Range(0, group.clips.Length)];
            if (clip == null)
                return;

            source.pitch = SampleRandomPitch(group.pitchVariation);
            source.PlayOneShot(clip, group.volume);
            _lastQueueSoundTime = Time.time;
        }

        private AudioSource ResolveQueueAudioSource()
        {
            if (meleeQueueUseGlobalAudio)
            {
                EnsureGlobalAudioSource();
                return globalAudioSource;
            }

            return audioSource;
        }
        
        public void PlaySound(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || audioSource == null)
                return;
            
            audioSource.pitch = SampleRandomPitch(pitchVariation);
            audioSource.PlayOneShot(clip, volumeScale);
        }

        private static float SampleRandomPitch(float variation)
        {
            if (variation <= 0.001f)
                return 1f;

            return Mathf.Clamp(1f + Random.Range(-variation, variation), 0.5f, 2f);
        }

        private void EnsureGlobalAudioSource()
        {
            if (globalAudioSource != null)
            {
                SpatialAudioUtility.ConfigureAs2D(globalAudioSource);
                return;
            }

            globalAudioSource = gameObject.AddComponent<AudioSource>();
            globalAudioSource.playOnAwake = false;
            SpatialAudioUtility.ConfigureAs2D(globalAudioSource);
        }
    }
}
