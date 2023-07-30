using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelWrangler : MonoBehaviour
{
    [Header("Standard Section")]
    RectTransform myRectTransform;
    public Vector2 HideDelta;
    private Vector2 ActivePosition, HiddenPosition;
    public float LerpScale;
    public float MinSpeed;
    public float CutOffThreshHold;
    public bool InitialState;
    bool IsActive;

    [Header("Sauce Section")]
    public Image Target;
    public Sprite ActiveSprite, HiddenSprite;

    public void ToggleState()
    {
        SetState(!IsActive);
    }

    public void SetState(bool state = true)
    {
        IsActive = state;

        if (Target != null)
        {
            if (state && ActiveSprite != null)
                Target.sprite = ActiveSprite;

            if (!state && HiddenSprite != null)
                Target.sprite = HiddenSprite;
        }

        if (IsActive)
            gameObject.SetActive(true);
    }

    // Start is called before the first frame update
    void Start()
    {
        myRectTransform = ((RectTransform)transform);
        ActivePosition = myRectTransform.anchoredPosition;
        HiddenPosition = ActivePosition + HideDelta;
        IsActive = InitialState;

        if (!InitialState)
            myRectTransform.anchoredPosition = HiddenPosition;
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 targetPosition = IsActive ? ActivePosition : HiddenPosition;
        Vector2 delta = targetPosition - myRectTransform.anchoredPosition;
        float magSquared = (float)Fast.FastDistance(ref delta);

        float solution = (LerpScale * magSquared * MinSpeed)/magSquared;

        delta.x *= solution;
        delta.y *= solution;

        delta.x = float.IsNaN(delta.x) ? 0 : delta.x;
        delta.y = float.IsNaN(delta.y) ? 0 : delta.y;

        myRectTransform.anchoredPosition += delta;
        if (!IsActive &&
            Math.Abs(delta.x) < CutOffThreshHold &&
            Math.Abs(delta.y) < CutOffThreshHold)
            gameObject.SetActive(false);
    }
}
