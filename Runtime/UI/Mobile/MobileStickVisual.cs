using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

namespace GGemCo2DControl
{
    internal sealed class MobileStickVisual : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private RectTransform _baseRect;
        private RectTransform _knobRect;
        private float _movementRange;

        public void Initialize(RectTransform baseRect, RectTransform knobRect, float movementRange)
        {
            _baseRect = baseRect;
            _knobRect = knobRect;
            _movementRange = Mathf.Max(1f, movementRange);
        }

        public void OnPointerDown(PointerEventData eventData) => UpdateKnobPosition(eventData);
        public void OnDrag(PointerEventData eventData) => UpdateKnobPosition(eventData);

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_knobRect != null)
            {
                _knobRect.anchoredPosition = Vector2.zero;
            }
        }

        private void UpdateKnobPosition(PointerEventData eventData)
        {
            if (_baseRect == null || _knobRect == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_baseRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                return;
            }

            Vector2 clamped = Vector2.ClampMagnitude(localPoint, _movementRange);
            _knobRect.anchoredPosition = clamped;
        }
    }

    internal static class MobileOnScreenUtility
    {
        public static void TryEnableIsolatedStickInput(OnScreenStick stick)
        {
            if (stick == null)
            {
                return;
            }

            const string propertyName = "useIsolatedInputActions";
            PropertyInfo property = typeof(OnScreenStick).GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property is { CanWrite: true } && property.PropertyType == typeof(bool))
            {
                property.SetValue(stick, true);
            }
        }
    }
}
