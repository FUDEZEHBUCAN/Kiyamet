using UnityEngine;
using UnityEngine.SceneManagement;
using _Root.Scripts.UI;

namespace _Root.Scripts.Controllers
{
    public class SceneInitializer : MonoBehaviour
    {
        public void NewGameButton()
        {
            var flow = FindFirstObjectByType<MainMenuNewGameFlow>();
            if (flow != null)
            {
                flow.BeginNewGame();
                return;
            }

            SceneManager.LoadScene(1);
        }
    }
}