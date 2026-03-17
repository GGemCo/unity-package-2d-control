using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// HUD 표시/숨김 정책을 reason 기반으로 관리합니다.
    /// </summary>
    public sealed class MobileInputHudVisibilityService
    {
        private readonly HashSet<string> _suppressedReasons = new HashSet<string>();
        private CanvasGroup _canvasGroup;
        private bool _visibleByPlatform = true;

        public void Bind(CanvasGroup canvasGroup)
        {
            _canvasGroup = canvasGroup;
            Apply();
        }

        public void SetVisibleByPlatform(bool visible)
        {
            _visibleByPlatform = visible;
            Apply();
        }

        public void SetSuppressed(string reason, bool suppressed)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "Unknown";
            }

            if (suppressed)
            {
                _suppressedReasons.Add(reason);
            }
            else
            {
                _suppressedReasons.Remove(reason);
            }

            Apply();
        }

        public void ClearAllSuppressedReasons()
        {
            _suppressedReasons.Clear();
            Apply();
        }

        private void Apply()
        {
            if (_canvasGroup == null)
            {
                return;
            }

            bool visible = _visibleByPlatform && _suppressedReasons.Count == 0;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}
