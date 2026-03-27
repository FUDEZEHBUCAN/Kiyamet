using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class cd_status : MonoBehaviour
{
    public float max_cd;
    public float current_cd;
    public float current_percentage;

    // Inspector üzerinden baðlanabilir: UI güncellemeleri için
    public UnityEvent<float> onCooldownUpdated;
    public UnityEvent onCooldownFinished;

    bool wasOnCooldown;

    void Start()
    {
        current_percentage = (max_cd > 0f) ? Mathf.Clamp01(current_cd / max_cd) : 0f;
        wasOnCooldown = current_cd > 0f;
    }

    void Update()
    {
        if (current_cd > 0f)
        {
            current_cd -= Time.deltaTime;
            if (current_cd <= 0f)
            {
                current_cd = 0f;
                current_percentage = 0f;
                if (wasOnCooldown)
                {
                    onCooldownUpdated?.Invoke(current_percentage);
                    onCooldownFinished?.Invoke();
                    wasOnCooldown = false;
                }
            }
            else
            {
                current_percentage = (max_cd > 0f) ? Mathf.Clamp01(current_cd / max_cd) : 0f;
                onCooldownUpdated?.Invoke(current_percentage);
                wasOnCooldown = true;
            }
        }
    }

    // Cooldown'u baþlatmak için kolay fonksiyon
    public void StartCooldown()
    {
        current_cd = max_cd;
        current_percentage = (max_cd > 0f) ? 1f : 0f;
        onCooldownUpdated?.Invoke(current_percentage);
        wasOnCooldown = true;
    }

    public bool IsOnCooldown()
    {
        return current_cd > 0f;
    }
}
