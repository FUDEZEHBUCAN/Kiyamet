using UnityEngine;

namespace _Root.Scripts.Interactable
{
    /// <summary>
    /// Varsayılan etkileşim menzilinden farklı bir menzil kullanılır.
    /// </summary>
    public interface IInteractableRangeOverride
    {
        float GetInteractionRange(Transform interactor);
    }
}
