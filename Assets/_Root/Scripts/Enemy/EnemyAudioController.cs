using UnityEngine;

namespace _Root.Scripts.Enemy
{
    /// <summary>
    /// Enemy'ler için audio controller - saldırı, hasar alma, ölüm sesleri
    /// </summary>
    public class EnemyAudioController : MonoBehaviour
    {
        [Header("Attack Sounds")]
        [SerializeField] private AudioClip[] attackSwingSounds; // Saldırı başlangıç sesleri
        [SerializeField] private AudioClip[] attackHitSounds; // Hasar vurma sesleri
        
        [Header("Damage Sounds")]
        [SerializeField] private AudioClip[] takeDamageSounds; // Hasar alma sesleri
        
        [Header("Death Sounds")]
        [SerializeField] private AudioClip[] deathSounds; // Ölüm sesleri
        
        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float volume = 1f;
        
        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.volume = volume;
        }
        
        /// <summary>
        /// Saldırı başlangıç sesi (swing)
        /// </summary>
        public void PlayAttackSwing()
        {
            if (audioSource != null && attackSwingSounds != null && attackSwingSounds.Length > 0)
            {
                AudioClip clip = attackSwingSounds[Random.Range(0, attackSwingSounds.Length)];
                if (clip != null)
                {
                    audioSource.PlayOneShot(clip);
                }
            }
        }
        
        /// <summary>
        /// Saldırı vuruş sesi (hasar vurduğunda)
        /// </summary>
        public void PlayAttackHit()
        {
            if (audioSource != null && attackHitSounds != null && attackHitSounds.Length > 0)
            {
                AudioClip clip = attackHitSounds[Random.Range(0, attackHitSounds.Length)];
                if (clip != null)
                {
                    audioSource.PlayOneShot(clip);
                }
            }
        }
        
        /// <summary>
        /// Hasar alma sesi
        /// </summary>
        public void PlayTakeDamage()
        {
            if (audioSource != null && takeDamageSounds != null && takeDamageSounds.Length > 0)
            {
                AudioClip clip = takeDamageSounds[Random.Range(0, takeDamageSounds.Length)];
                if (clip != null)
                {
                    audioSource.PlayOneShot(clip);
                }
            }
        }
        
        /// <summary>
        /// Ölüm sesi
        /// </summary>
        public void PlayDeath()
        {
            if (audioSource != null && deathSounds != null && deathSounds.Length > 0)
            {
                AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
                if (clip != null)
                {
                    audioSource.PlayOneShot(clip);
                }
            }
        }
    }
}
