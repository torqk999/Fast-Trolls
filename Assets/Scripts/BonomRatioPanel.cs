using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BonomRatioPanel : MonoBehaviour
{
    public UIManager Manager;
    public Squad Squad;
    public BonomType Type;

    public Image TypeSprite;
    public TMP_Text Percent;
    public Slider Slider;
    public Button Lock;
    
    private float slider_cache = 0;

    public float Delta => Slider.value - slider_cache;

    public void Init(UIManager manager, BonomStats stats, Squad squad)
    {
        Manager = manager;
        Type = stats.Type;
        TypeSprite.sprite = stats.Sprite;
        Sync(squad);
        Lock.onClick.AddListener(() => Manager.RatioSliderLockToggle(this));
    }

    public void RollBack()
    {
        Sync(Squad);
    }

    public void Sync(Squad squad)
    {
        Squad = squad;

        if (Squad == null)
            return;

        Slider.value = Squad.Ratio;
        slider_cache = Squad.Ratio;
        Percent.text = (Squad.Ratio * 100).ToString("N0");
        Lock.gameObject.GetComponent<Image>().sprite = Squad.Locked ? Manager.Locked : Manager.Unlocked;
    }

    private void Update()
    {
        if (Slider != null && Slider.value != slider_cache)
        {
            Debug.Log($"Slider delta: {Delta}");
            Manager.RatioSliderUpdate(this);
            slider_cache = Slider.value;
        }
    }
}
