using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모바일 HUD 조이스틱 뷰입니다.
    /// 입력용 참조와 노브 위치 동기화만 담당합니다.
    /// </summary>
    public sealed class MobileHudJoystickView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private RectTransform knobRectTransform;
        [SerializeField] private OnScreenStick onScreenStick;

        private float _movementRange = 68f;

        public RectTransform RectTransform => rectTransform;
        public RectTransform KnobRectTransform => knobRectTransform;
        public OnScreenStick OnScreenStick => onScreenStick;

        public void EnsureReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (onScreenStick == null)
            {
                onScreenStick = GetComponent<OnScreenStick>();
            }

            if (knobRectTransform == null)
            {
                Transform knob = transform.Find("Knob");
                if (knob != null)
                {
                    knobRectTransform = knob.GetComponent<RectTransform>();
                }
            }
        }

        public void SetMovementRange(float movementRange)
        {
            _movementRange = Mathf.Max(1f, movementRange);

            if (onScreenStick != null)
            {
                onScreenStick.movementRange = _movementRange;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateKnobPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateKnobPosition(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ResetKnob();
        }

        private void OnDisable()
        {
            ResetKnob();
        }

        private void UpdateKnobPosition(PointerEventData eventData)
        {
            EnsureReferences();
            if (rectTransform == null || knobRectTransform == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                return;
            }

            knobRectTransform.anchoredPosition = Vector2.ClampMagnitude(localPoint, _movementRange);
        }

        private void ResetKnob()
        {
            if (knobRectTransform != null)
            {
                knobRectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }
}
