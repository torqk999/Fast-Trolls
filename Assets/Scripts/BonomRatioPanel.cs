using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BonomRatioPanel : MonoBehaviour
{
    public Squad Squad;

    public TMP_Text Percent;
    public Slider Slider;
    public Button Lock;

    public string Type;
    public Image ThumbNail;

    private float slider_cache = 0;

    public float Delta => Slider.value - slider_cache;

    public void Init(BonomStats stats)
    {
        Type = stats.Type;
        ThumbNail.sprite = stats.Sprite;
        //Sync(squad);
        Lock.onClick.AddListener(() => Game.UIManager.RatioSliderLockToggle(this));
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
        Lock.gameObject.GetComponent<Image>().sprite = Squad.Locked ? Game.UIManager.Locked : Game.UIManager.Unlocked;
    }

    private void Update()
    {
        if (Slider != null && Slider.value != slider_cache)
        {
            Debug.Log($"Slider delta: {Delta}");
            Game.UIManager.RatioSliderUpdate(this);
            slider_cache = Slider.value;
        }
    }
}
