using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// UI 위 클릭/터치 여부 판정 유틸리티입니다.
    /// </summary>
    internal static class UiPointerGuard
    {
        public static bool IsPointerOverUi()
        {
            if (EventSystem.current == null) return false;

            // 데스크톱(마우스)
            if (Mouse.current != null && EventSystem.current.IsPointerOverGameObject(Mouse.current.deviceId))
                return true;

            // 모바일(터치)
            if (Touchscreen.current != null)
            {
                foreach (var t in Touchscreen.current.touches)
                {
                    if (t.isInProgress && EventSystem.current.IsPointerOverGameObject(t.touchId.ReadValue()))
                        return true;
                }
            }

            // 최후의 보루(플랫폼에 따라 동작)
            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}
