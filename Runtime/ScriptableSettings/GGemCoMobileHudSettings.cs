using System;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 모바일 온스크린 HUD 설정입니다.
    /// </summary>
    [CreateAssetMenu(fileName = ConfigScriptableObjectControl.MobileHud.FileName, menuName = ConfigScriptableObjectControl.MobileHud.MenuName, order = ConfigScriptableObjectControl.MobileHud.Ordering)]
    public class GGemCoMobileHudSettings : ScriptableObject, ISettingsChangeNotifier
    {
        public event Action Changed;

#if UNITY_EDITOR
        /// <summary>
        /// 인스펙터 값 변경 시 범위를 보정하고 변경 이벤트를 전파합니다.
        /// </summary>
        private void OnValidate()
        {
            joystickMovementRange = Mathf.Max(1f, joystickMovementRange);
            joystickDeadZone = Mathf.Clamp01(joystickDeadZone);
            Changed?.Invoke();
        }
#endif

        /// <summary>
        /// 외부 코드에서 설정 변경 이벤트를 수동으로 발생시킵니다.
        /// </summary>
        public void RaiseChanged()
        {
            Changed?.Invoke();
        }

        [Header("활성화")]
        public bool enableMobileHud = true;
        public bool enableOnMobilePlatformOnly = true;
        public bool forceShowInEditor;
        public bool useSafeArea = true;
        public bool autoHideWhenNonTouchScheme;

        [Header("프리팹")]
        public GameObject hudPrefab;

        [Header("조이스틱")]
        public bool showJoystick = true;
        public float joystickMovementRange = 68f;
        [Range(0f, 1f)] public float joystickDeadZone = 0.1f;

        [Header("전투 터치")]
        public bool enableHalfScreenCombatInput;
    }
}
