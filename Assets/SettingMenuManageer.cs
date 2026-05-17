using UnityEngine;

public class SettingsPanelManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject graphicsPanel;
    [SerializeField] private GameObject controlsPanel;

    private void Start()
    {
        // Open a default panel on load, or close all
        OpenAudio();
    }

    public void OpenAudio() => ShowPanel(audioPanel);
    public void OpenGraphics() => ShowPanel(graphicsPanel);
    public void OpenControls() => ShowPanel(controlsPanel);

    public void ShowPanel(GameObject target)
    {
        audioPanel.SetActive(false);
        graphicsPanel.SetActive(false);
        controlsPanel.SetActive(false);

        target.SetActive(true);
    }
}