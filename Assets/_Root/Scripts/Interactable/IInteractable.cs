using UnityEngine;

namespace _Root.Scripts.Interactable
{
    /// <summary>
    /// Etkileşime girebilen objeler için interface
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Etkileşim başlatıldığında çağrılır
        /// </summary>
        /// <param name="interactor">Etkileşime geçen oyuncu</param>
        void OnInteractStart(Transform interactor);
        
        /// <summary>
        /// Etkileşim bittiğinde çağrılır
        /// </summary>
        /// <param name="interactor">Etkileşimi bırakan oyuncu</param>
        void OnInteractEnd(Transform interactor);
        
        /// <summary>
        /// Etkileşim sırasında her frame çağrılır
        /// </summary>
        /// <param name="interactor">Etkileşimde olan oyuncu</param>
        void OnInteractUpdate(Transform interactor);
        
        /// <summary>
        /// Etkileşime girebilir mi kontrolü
        /// </summary>
        bool CanInteract(Transform interactor);
    }
}

