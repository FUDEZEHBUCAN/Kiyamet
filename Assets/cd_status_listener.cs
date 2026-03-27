using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class cd_status_listener : MonoBehaviour
{
    // Inspector'da atayýn veya otomatik bulunacak
    public cd_status cdSource;
    public Image overlayImage;

    void Start()
    {
        if (cdSource == null)
            cdSource = FindObjectOfType<cd_status>();


        if (cdSource != null)
        {
            cdSource.onCooldownUpdated.AddListener(OnCooldownUpdated);
            cdSource.onCooldownFinished.AddListener(OnCooldownFinished);

            // Baþlangýç durumu
            if (overlayImage != null)
                overlayImage.fillAmount = cdSource.current_percentage;
        }
    }

    void OnDestroy()
    {
        if (cdSource != null)
        {
            cdSource.onCooldownUpdated.RemoveListener(OnCooldownUpdated);
            cdSource.onCooldownFinished.RemoveListener(OnCooldownFinished);
        }
    }

    void OnCooldownUpdated(float perc)
    {
        if (overlayImage != null)
            overlayImage.fillAmount = perc;
    }

    void OnCooldownFinished()
    {
        if (overlayImage != null)
            overlayImage.fillAmount = 0f;
    }
}
