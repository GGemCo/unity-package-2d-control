using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모바일 HUD 조이스틱 뷰입니다.
    /// </summary>
    public sealed class MobileHudJoystickView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Image baseImage;
        [SerializeField] private RectTransform knobRectTransform;
        [SerializeField] private Image knobImage;
        [SerializeField] private OnScreenStick onScreenStick;
        [SerializeField] private MobileStickVisual stickVisual;

        public RectTransform RectTransform => rectTransform;
        public Image BaseImage => baseImage;
        public RectTransform KnobRectTransform => knobRectTransform;
        public Image KnobImage => knobImage;
        public OnScreenStick OnScreenStick => onScreenStick;

        public void EnsureReferences()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (baseImage == null) baseImage = GetComponent<Image>();
            if (onScreenStick == null) onScreenStick = GetComponent<OnScreenStick>();
            if (stickVisual == null) stickVisual = GetComponent<MobileStickVisual>();

            if (knobRectTransform == null || knobImage == null)
            {
                Transform knob = transform.Find("Knob");
                if (knob != null)
                {
                    knobRectTransform = knob.GetComponent<RectTransform>();
                    knobImage = knob.GetComponent<Image>();
                }
            }
        }

        public void ApplyVisual(float baseAlpha, float knobAlpha, Sprite baseSprite, Sprite knobSprite, float baseSize, float knobSize, float movementRange)
        {
            EnsureReferences();

            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(baseSize, baseSize);
            }

            if (baseImage != null)
            {
                if (baseSprite != null)
                {
                    baseImage.sprite = baseSprite;
                }

                Color color = baseImage.color;
                color.a = baseAlpha;
                baseImage.color = color;
            }

            if (knobRectTransform != null)
            {
                knobRectTransform.sizeDelta = new Vector2(knobSize, knobSize);
            }

            if (knobImage != null)
            {
                if (knobSprite != null)
                {
                    knobImage.sprite = knobSprite;
                }

                Color color = knobImage.color;
                color.a = knobAlpha;
                knobImage.color = color;
            }

            if (stickVisual == null)
            {
                stickVisual = gameObject.AddComponent<MobileStickVisual>();
            }

            stickVisual.Initialize(rectTransform, knobRectTransform, movementRange);

            if (onScreenStick != null)
            {
                onScreenStick.movementRange = movementRange;
            }
        }
    }
}
