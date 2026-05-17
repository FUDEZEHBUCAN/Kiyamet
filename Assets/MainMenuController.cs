using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject optionsUI;

    public void OnSettingsButtonClicked()
    {
        optionsUI.SetActive(!optionsUI.activeSelf);
    }
}