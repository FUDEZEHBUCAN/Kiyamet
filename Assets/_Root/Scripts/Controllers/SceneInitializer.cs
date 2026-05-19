using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Root.Scripts.Controllers
{
    public class SceneInitializer : MonoBehaviour
    {
        public void NewGameButton()
        {
            SceneManager.LoadScene(1);
        }
    }
}