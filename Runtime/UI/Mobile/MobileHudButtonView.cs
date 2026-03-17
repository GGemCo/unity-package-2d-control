using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모바일 HUD 버튼 뷰입니다.
    /// </summary>
    public sealed class MobileHudButtonView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private OnScreenButton onScreenButton;
        [SerializeField] private MobileButtonVisual buttonVisual;

        public RectTransform RectTransform => rectTransform;
        public Image BackgroundImage => backgroundImage;
        public Image IconImage => iconImage;
        public OnScreenButton OnScreenButton => onScreenButton;

        public void EnsureReferences()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            if (onScreenButton == null) onScreenButton = GetComponent<OnScreenButton>();
            if (buttonVisual == null) buttonVisual = GetComponent<MobileButtonVisual>();

            if (iconImage == null)
            {
                Transform icon = transform.Find("Icon");
                if (icon != null)
                {
                    iconImage = icon.GetComponent<Image>();
                }
            }
        }

        public void ApplyVisual(float alpha, Sprite backgroundSprite, Sprite iconSprite, Vector2 size, float pressedScale)
        {
            EnsureReferences();

            if (rectTransform != null)
            {
                rectTransform.sizeDelta = size;
            }

            if (backgroundImage != null)
            {
                if (backgroundSprite != null)
                {
                    backgroundImage.sprite = backgroundSprite;
                }

                Color color = backgroundImage.color;
                color.a = alpha;
                backgroundImage.color = color;
            }

            if (iconImage != null)
            {
                iconImage.sprite = iconSprite;
                iconImage.enabled = iconSprite != null;
            }

            if (buttonVisual == null && rectTransform != null)
            {
                buttonVisual = gameObject.AddComponent<MobileButtonVisual>();
            }

            buttonVisual?.Initialize(rectTransform, pressedScale);
        }
    }
}
