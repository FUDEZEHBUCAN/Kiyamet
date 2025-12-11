using Fusion;
using UnityEngine;

namespace _Root.Scripts.Network
{
    public class NetworkPlayer : NetworkBehaviour,IPlayerLeft
    { 
        public static NetworkPlayer Local { get; set; }
        
        
        public void PlayerLeft(PlayerRef player)
        {
            if (player == Object.InputAuthority)
            {
                Runner.Despawn(Object);
            }
        }

        public override void Spawned()
        {
            Debug.Log($"[NetworkPlayer] Spawned - InputAuthority: {Object.InputAuthority}, HasInputAuthority: {Object.HasInputAuthority}, HasStateAuthority: {Object.HasStateAuthority}, ObjectId: {Object.Id}");
            
            if (Object.HasInputAuthority)
            {
                Local = this;
                Debug.Log($"[NetworkPlayer] ✓ This is LOCAL player (I control it)");
            }
            else 
            {
                Debug.Log($"[NetworkPlayer] ✓ This is REMOTE player (other client controls it)");
            }
        }
    }
}