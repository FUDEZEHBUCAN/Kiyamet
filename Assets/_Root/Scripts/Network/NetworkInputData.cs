using UnityEngine;
using Fusion;

namespace _Root.Scripts.Network
{
    public struct NetworkInputData : INetworkInput
    {
        public Vector2 MovementInput;
        public float RotationInput;
        public NetworkBool IsJumpPressed;
        public NetworkBool IsShootPressed;
        public NetworkBool IsMeleePressed;
        public NetworkBool IsBlockPressed;
        public NetworkBool IsDashPressed;
        public NetworkBool IsUltimatePressed;
        public NetworkBool IsInteractPressed;
        public NetworkBool IsRunning;
        public Vector3 AimPoint;
    }
}