using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// Safe Area를 기준으로 HUD 루트 영역을 갱신합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileInputHudSafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private RectTransform target;

        private Rect _lastSafeArea = Rect.zero;
        private Vector2 _padding;
        private bool _useSafeArea = true;

        public void Initialize(RectTransform targetRect, bool useSafeArea, Vector2 padding)
        {
            target = targetRect;
            _useSafeArea = useSafeArea;
            _padding = padding;
            Apply();
        }

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<RectTransform>();
            }

            Apply();
        }

        private void Update()
        {
            if (target == null)
            {
                return;
            }

            if (Screen.safeArea != _lastSafeArea)
            {
                Apply();
            }
        }

        public void Apply()
        {
            if (target == null)
            {
                return;
            }

            if (!_useSafeArea)
            {
                target.anchorMin = Vector2.zero;
                target.anchorMax = Vector2.one;
                target.offsetMin = new Vector2(_padding.x, _padding.y);
                target.offsetMax = new Vector2(-_padding.x, -_padding.y);
                _lastSafeArea = Screen.safeArea;
                return;
            }

            Rect safeArea = Screen.safeArea;
            Vector2 min = safeArea.position;
            Vector2 max = safeArea.position + safeArea.size;

            min.x /= Mathf.Max(1f, Screen.width);
            min.y /= Mathf.Max(1f, Screen.height);
            max.x /= Mathf.Max(1f, Screen.width);
            max.y /= Mathf.Max(1f, Screen.height);

            target.anchorMin = min;
            target.anchorMax = max;
            target.offsetMin = new Vector2(_padding.x, _padding.y);
            target.offsetMax = new Vector2(-_padding.x, -_padding.y);
            _lastSafeArea = safeArea;
        }
    }
}
