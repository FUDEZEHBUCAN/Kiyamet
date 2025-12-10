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
            if (Object.HasInputAuthority)
            {
                Local = this;
                Debug.Log("spawned local player");
            }
            else Debug.Log("spawned remote player");
        }
    }
}