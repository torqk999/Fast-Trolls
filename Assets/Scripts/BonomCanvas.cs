using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BonomCanvas : MonoBehaviour
{
    public TMP_Text NamePanel;
    public Slider HealthSlider;
    public Slider AttackSlider; // << Needs implementation...
    private long LastCanvasRefresh;
    private Bonom myBonom;

    public void Init(Bonom bonom)
    {
        if (NamePanel == null ||
            HealthSlider == null ||
            AttackSlider == null)
            return;

        gameObject.SetActive(true);
        myBonom = bonom;
        NamePanel.text = bonom.Stats.Type;
    }

    private void Update()
    {
        transform.position = Camera.main.WorldToScreenPoint(myBonom.transform.position + (Vector3.up * Game.Instance.HalfBonomHeight), Camera.MonoOrStereoscopicEye.Mono);
        if (Game.GameTime + Game.Instance.CanvasDelayTicks < LastCanvasRefresh)
            return;

        LastCanvasRefresh = Game.GameTime;
        HealthSlider.value = myBonom.HealthPercent;
    }
}
