using UnityEngine;
using UnityEngine.EventSystems;

namespace GGemCo2DControl
{
    
    internal sealed class MobileButtonVisual : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _rectTransform;
        private Vector3 _defaultScale = Vector3.one;
        private float _pressedScale = 0.92f;

        public void Initialize(RectTransform rectTransform, float pressedScale)
        {
            _rectTransform = rectTransform;
            _defaultScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
            _pressedScale = Mathf.Clamp(pressedScale, 0.5f, 1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_rectTransform != null)
            {
                _rectTransform.localScale = _defaultScale * _pressedScale;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_rectTransform != null)
            {
                _rectTransform.localScale = _defaultScale;
            }
        }
    }

}