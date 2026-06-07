using UnityEngine;

namespace _Root.Scripts.Interactable
{
    /// <summary>
    /// Yakınlık / bakış hesabında collider yerine sabit bir nokta kullanılır.
    /// </summary>
    public interface IInteractableProximityTarget
    {
        Vector3 GetProximitySamplePoint(Transform interactor);
    }
}
