using UnityEngine;

public class SettingsPanelManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject graphicsPanel;
    [SerializeField] private GameObject controlsPanel;

    private void OnEnable()
    {
        OpenAudio();
    }

    public void OpenAudio() => ShowPanel(audioPanel);
    public void OpenGraphics() => ShowPanel(graphicsPanel);
    public void OpenControls() => ShowPanel(controlsPanel);

    public void ShowPanel(GameObject target)
    {
        if (target == null)
            return;

        if (audioPanel != null)
            audioPanel.SetActive(false);
        if (graphicsPanel != null)
            graphicsPanel.SetActive(false);
        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        target.SetActive(true);
    }


    public void Toggle()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}