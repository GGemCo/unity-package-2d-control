using UnityEngine;
using UnityEngine.InputSystem.OnScreen;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모바일 HUD 버튼 뷰입니다.
    /// 입력 바인딩에 필요한 참조만 관리합니다.
    /// </summary>
    public sealed class MobileHudButtonView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private OnScreenButton onScreenButton;

        public RectTransform RectTransform => rectTransform;
        public OnScreenButton OnScreenButton => onScreenButton;

        public void EnsureReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (onScreenButton == null)
            {
                onScreenButton = GetComponent<OnScreenButton>();
            }
        }
    }
}
