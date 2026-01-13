using UnityEngine;
using Fusion;
using _Root.Scripts.Controllers;
using _Root.Scripts.Enums;
using NetworkPlayer = _Root.Scripts.Network.NetworkPlayer;

namespace _Root.Scripts.Interactable
{
    public class BreakableDoor : NetworkBehaviour
    {
        [Header("Door Objects")]
        [SerializeField] private GameObject intactDoor; // Sağlam kapı objesi
        [SerializeField] private GameObject brokenDoor; // Yıkılmış kapı objesi
        
        [Header("Particle Effect")]
        [SerializeField] private GameObject breakParticlePrefab; // Kırılma particle efekti
        [SerializeField] private Transform particleSpawnPoint; // Particle spawn noktası (boşsa kapının merkezi kullanılır)
        
        [Header("Audio Effect")]
        [SerializeField] private AudioClip[] breakSounds; // Kırılma ses efektleri
        [SerializeField] private AudioSource audioSource; // AudioSource (boşsa otomatik eklenir)
        [SerializeField] private float volume = 1f; // Ses seviyesi
        
        [Header("Trigger Settings")]
        [SerializeField] private bool useTrigger = true; // Trigger kullanılsın mı?
        [SerializeField] private string triggerTag = "RockTrigger"; // Trigger tag'i
        
        private bool _isBroken = false;
        
        private void Awake()
        {
            // Başlangıç durumunu ayarla
            if (intactDoor != null)
                intactDoor.SetActive(true);
            
            if (brokenDoor != null)
                brokenDoor.SetActive(false);
            
            // AudioSource ayarla
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
        /// Kapıyı kır (PushableRock'tan çağrılabilir)
        /// </summary>
        public void BreakDoor()
        {
            if (!Object.HasStateAuthority || _isBroken)
                return;
            
            _isBroken = true;
            
            // Sağlam kapıyı kapat
            if (intactDoor != null)
            {
                intactDoor.SetActive(false);
            }
            
            // Yıkılmış kapıyı aç
            if (brokenDoor != null)
            {
                brokenDoor.SetActive(true);
            }
            
            // Particle spawn et
            SpawnBreakParticle();
            
            // Ses efekti çal
            PlayBreakSound();
            
            // Tüm oyunculara kamera shake ver
            TriggerCameraShakeForAllPlayers();
        }
        
        /// <summary>
        /// Kırılma particle efektini spawn et
        /// </summary>
        private void SpawnBreakParticle()
        {
            if (breakParticlePrefab == null)
                return;
            
            Vector3 spawnPosition = particleSpawnPoint != null 
                ? particleSpawnPoint.position 
                : transform.position;
            
            Quaternion spawnRotation = particleSpawnPoint != null 
                ? particleSpawnPoint.rotation 
                : Quaternion.identity;
            
            GameObject particle = Instantiate(breakParticlePrefab, spawnPosition, spawnRotation);
            
            // Particle'ı belirli bir süre sonra yok et (particle sistemi bitince)
            ParticleSystem ps = particle.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                Destroy(particle, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            else
            {
                // ParticleSystem yoksa varsayılan süre
                Destroy(particle, 5f);
            }
        }
        
        /// <summary>
        /// Kırılma ses efektini çal
        /// </summary>
        private void PlayBreakSound()
        {
            if (audioSource == null || breakSounds == null || breakSounds.Length == 0)
                return;
            
            // Rastgele bir ses seç
            AudioClip clip = breakSounds[Random.Range(0, breakSounds.Length)];
            if (clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }
        
        /// <summary>
        /// Tüm oyunculara kamera shake ver
        /// </summary>
        private void TriggerCameraShakeForAllPlayers()
        {
            // Tüm NetworkPlayer'ları bul
            NetworkPlayer[] allPlayers = FindObjectsOfType<NetworkPlayer>();
            
            foreach (var player in allPlayers)
            {
                // Her oyuncunun local camera controller'ına shake gönder
                if (player.Object != null && player.Object.HasInputAuthority && TpsCameraController.Instance != null)
                {
                    TpsCameraController.Instance.ShakeCamera(CameraShakeType.DoorBreak);
                }
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!Object.HasStateAuthority || !useTrigger || _isBroken)
                return;
            
            // PushableRock kontrolü
            PushableRock rock = other.GetComponent<PushableRock>();
            if (rock != null)
            {
                // Kaya trigger'a girdi, kapıyı kır
                BreakDoor();
                return;
            }
            
            // Tag kontrolü (alternatif yöntem)
            if (!string.IsNullOrEmpty(triggerTag) && other.CompareTag(triggerTag))
            {
                BreakDoor();
            }
        }
        
        /// <summary>
        /// Kapı kırıldı mı?
        /// </summary>
        public bool IsBroken => _isBroken;
    }
}

