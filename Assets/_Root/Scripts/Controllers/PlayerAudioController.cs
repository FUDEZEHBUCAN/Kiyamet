using UnityEngine;

namespace _Root.Scripts.Controllers
{
    [RequireComponent(typeof(AudioSource))]
    public class PlayerAudioController : MonoBehaviour
    {
        [Header("Audio Source")]
        [SerializeField] private AudioSource audioSource;
        
        [Header("Attack Sounds")]
        [Tooltip("Melee saldırı başlangıç sesi (swing)")]
        [SerializeField] private AudioClip[] meleeSwingSounds;
        [Tooltip("Melee saldırı hasar verdiğinde")]
        [SerializeField] private AudioClip[] meleeHitSounds;
        
        [Header("Damage Sounds")]
        [Tooltip("Hasar aldığında")]
        [SerializeField] private AudioClip[] takeDamageSounds;
        [Tooltip("Öldüğünde")]
        [SerializeField] private AudioClip[] deathSounds;
        
        [Header("Block Sounds")]
        [Tooltip("Kalkanla saldırı blokladığında")]
        [SerializeField] private AudioClip[] blockSounds;
        
        [Header("Settings")]
        [SerializeField] private float pitchVariation = 0.1f;
        [SerializeField] private float minTimeBetweenSounds = 0.05f;
        
        private float _lastSoundTime;
        
        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }
        
        /// <summary>
        /// Melee saldırı başlangıç sesi (swing)
        /// </summary>
        public void PlayMeleeSwing()
        {
            PlayRandomSound(meleeSwingSounds);
        }
        
        /// <summary>
        /// Melee saldırı hasar verdiğinde
        /// </summary>
        public void PlayMeleeHit()
        {
            PlayRandomSound(meleeHitSounds);
        }
        
        /// <summary>
        /// Hasar aldığında
        /// </summary>
        public void PlayTakeDamage()
        {
            PlayRandomSound(takeDamageSounds);
        }
        
        /// <summary>
        /// Öldüğünde
        /// </summary>
        public void PlayDeath()
        {
            PlayRandomSound(deathSounds);
        }
        
        /// <summary>
        /// Kalkanla saldırı blokladığında
        /// </summary>
        public void PlayBlock()
        {
            PlayRandomSound(blockSounds);
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
            
            // Pitch varyasyonu ekle (daha doğal ses için)
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(clip);
            
            _lastSoundTime = Time.time;
        }
        
        /// <summary>
        /// Belirli bir ses çal (özel durumlar için)
        /// </summary>
        public void PlaySound(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || audioSource == null)
                return;
            
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(clip, volumeScale);
        }
    }
}
