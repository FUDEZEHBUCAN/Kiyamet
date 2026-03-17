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
        [SerializeField] private float pitchVariation = 0.1f;
        [SerializeField] private float minTimeBetweenSounds = 0.05f;
        
        private float _lastSoundTime;
        
        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
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
            
            // Pitch varyasyonu ekle (daha doğal ses için)
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(clip);
            
            _lastSoundTime = Time.time;
        }
        
        public void PlaySound(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || audioSource == null)
                return;
            
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(clip, volumeScale);
        }
    }
}
