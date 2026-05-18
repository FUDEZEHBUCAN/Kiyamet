using UnityEngine;

namespace _Root.Scripts.Controllers
{
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
        
        [Header("Settings")]
        [Tooltip("Her çalışmada pitch = 1 ± bu değer (ör. 0.22 → yaklaşık 0.78–1.22).")]
        [SerializeField] [Range(0f, 0.45f)] private float pitchVariation = 0.22f;
        [SerializeField] private float minTimeBetweenSounds = 0.05f;

        [Header("3D ses")]
        [SerializeField] private bool use3DSound = true;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 18f;
        
        private float _lastSoundTime;
        
        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            audioSource.playOnAwake = false;
            ApplySpatialSettings();
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
            PlayRandomSound(meleeSwingSounds);
        }
        
        public void PlayMeleeHit()
        {
            PlayRandomSound(meleeHitSounds);
        }
        
        public void PlayTakeDamage()
        {
            PlayRandomSound(takeDamageSounds);
        }
        
        public void PlayDeath()
        {
            PlayRandomSound(deathSounds);
        }
        
        public void PlayBlock()
        {
            PlayRandomSound(blockSounds);
        }
        
        public void PlayDash()
        {
            PlayRandomSound(dashSounds);
        }
        
        public void PlayDashHit()
        {
            PlayRandomSound(dashHitSounds);
        }
        
        private void PlayRandomSound(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
                return;
            
            if (audioSource == null)
                return;
            
            // Çok sık ses çalmayı engelle
            if (Time.time - _lastSoundTime < minTimeBetweenSounds)
                return;
            
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            
            if (clip == null)
                return;
            
            audioSource.pitch = SampleRandomPitch();
            audioSource.PlayOneShot(clip);
            
            _lastSoundTime = Time.time;
        }
        
        public void PlaySound(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || audioSource == null)
                return;
            
            audioSource.pitch = SampleRandomPitch();
            audioSource.PlayOneShot(clip, volumeScale);
        }

        private float SampleRandomPitch()
        {
            if (pitchVariation <= 0.001f)
                return 1f;

            return Mathf.Clamp(1f + Random.Range(-pitchVariation, pitchVariation), 0.5f, 2f);
        }
    }
}
