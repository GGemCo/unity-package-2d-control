using System;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using Object = UnityEngine.Object;

namespace GGemCo2DControl
{
    /// <summary>
    /// HUD 뷰 인스턴스와 입력 바인딩을 적용하는 프레젠터입니다.
    /// </summary>
    public sealed class MobileInputHudPresenter
    {
        /// <summary>
        /// EventSystem 구성을 점검합니다.
        /// </summary>
        public void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                {
                    GcLogger.LogError("EventSystem already exists but InputSystemUIInputModule is missing.");
                }

                return;
            }
        }

        /// <summary>
        /// HUD 프리팹을 인스턴스화하고 루트 뷰를 찾아 반환합니다.
        /// </summary>
        /// <param name="settings">모바일 HUD 설정입니다.</param>
        /// <returns>생성된 루트 뷰, 생성 실패 시 null 입니다.</returns>
        public MobileHudRootView CreateOrInstantiateView(GGemCoMobileHudSettings settings)
        {
            if (settings == null || settings.hudPrefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(settings.hudPrefab);
            instance.name = settings.hudPrefab.name;

            MobileHudRootView prefabView = instance.GetComponent<MobileHudRootView>();
            if (prefabView == null)
            {
                prefabView = instance.GetComponentInChildren<MobileHudRootView>(true);
            }

            if (prefabView != null)
            {
                prefabView.EnsureReferences();
                return prefabView;
            }

            Object.Destroy(instance);
            return null;
        }

        /// <summary>
        /// HUD 뷰 전체에 입력 경로와 표시 상태를 적용합니다.
        /// </summary>
        /// <param name="rootView">대상 HUD 루트 뷰입니다.</param>
        /// <param name="settings">모바일 HUD 설정입니다.</param>
        /// <param name="playerInput">플레이어 입력 컴포넌트입니다.</param>
        /// <param name="bindingResolver">액션-컨트롤 경로 해석기입니다.</param>
        public void Apply(MobileHudRootView rootView, GGemCoMobileHudSettings settings, PlayerInput playerInput, MobileInputBindingResolver bindingResolver)
        {
            if (rootView == null || settings == null)
            {
                return;
            }

            rootView.EnsureReferences();
            rootView.EnsureCombatZoneViews();

            if (rootView.RootCanvasGroup != null)
            {
                rootView.RootCanvasGroup.interactable = true;
                rootView.RootCanvasGroup.blocksRaycasts = true;
            }

            rootView.SafeAreaFitter?.Initialize(rootView.SafeAreaRoot, settings.useSafeArea);

            bool useHalfScreenCombatInput = settings.enableHalfScreenCombatInput;
            ApplyJoystickVisibility(rootView, settings, useHalfScreenCombatInput);

            if (rootView.JoystickView != null && rootView.JoystickView.gameObject.activeSelf)
            {
                ApplyJoystick(rootView.JoystickView, settings, playerInput, bindingResolver);
            }

            ApplyButtons(rootView, playerInput, bindingResolver);
            ApplyCombatInputMode(rootView, playerInput, bindingResolver, useHalfScreenCombatInput);
        }

        /// <summary>
        /// 조이스틱 표시 여부를 반영합니다.
        /// </summary>
        /// <param name="rootView">HUD 루트 뷰입니다.</param>
        /// <param name="settings">모바일 HUD 설정입니다.</param>
        /// <param name="forceHide">반분할 전투 모드 등으로 강제 숨김이 필요한지 여부입니다.</param>
        private static void ApplyJoystickVisibility(MobileHudRootView rootView, GGemCoMobileHudSettings settings, bool forceHide)
        {
            bool showJoystick = settings.showJoystick && !forceHide;
            SetViewActive(rootView.JoystickView, showJoystick);
        }

        /// <summary>
        /// 반분할 전투 입력 모드를 적용합니다.
        /// </summary>
        /// <param name="rootView">HUD 루트 뷰입니다.</param>
        /// <param name="playerInput">플레이어 입력 컴포넌트입니다.</param>
        /// <param name="bindingResolver">액션-컨트롤 경로 해석기입니다.</param>
        /// <param name="useHalfScreenCombatInput">반분할 전투 입력 사용 여부입니다.</param>
        private static void ApplyCombatInputMode(MobileHudRootView rootView, PlayerInput playerInput, MobileInputBindingResolver bindingResolver, bool useHalfScreenCombatInput)
        {
            SetViewActive(rootView.LeftCombatZoneView, useHalfScreenCombatInput);
            SetViewActive(rootView.RightCombatZoneView, useHalfScreenCombatInput);

            if (useHalfScreenCombatInput)
            {
                // 반분할 모드에서는 좌/우 터치 의미를 고정하기 위해 일반 액션 버튼 입력을 끕니다.
                SetStandardActionButtonsActive(rootView, false);

                ApplyButton(rootView.LeftCombatZoneView,
                    bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionGuard, "<Gamepad>/leftShoulder"));
                ApplyButton(rootView.RightCombatZoneView,
                    bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionAttack, "<Gamepad>/buttonWest"));
                return;
            }

            SetStandardActionButtonsActive(rootView, true);
        }

        /// <summary>
        /// 기존 액션 버튼(공격/점프/대시/가드/상호작용)의 표시를 일괄 전환합니다.
        /// </summary>
        /// <param name="rootView">HUD 루트 뷰입니다.</param>
        /// <param name="isActive">활성화 여부입니다.</param>
        private static void SetStandardActionButtonsActive(MobileHudRootView rootView, bool isActive)
        {
            SetViewActive(rootView.AttackButtonView, isActive);
            SetViewActive(rootView.JumpButtonView, isActive);
            SetViewActive(rootView.DashButtonView, isActive);
            SetViewActive(rootView.GuardButtonView, isActive);
            SetViewActive(rootView.InteractionButtonView, isActive);
        }

        /// <summary>
        /// 뷰가 존재할 때 GameObject 활성 상태를 전환합니다.
        /// </summary>
        /// <param name="component">활성 상태를 바꿀 컴포넌트입니다.</param>
        /// <param name="isActive">활성화 여부입니다.</param>
        private static void SetViewActive(Component component, bool isActive)
        {
            if (component == null)
            {
                return;
            }

            if (component.gameObject.activeSelf == isActive)
            {
                return;
            }

            component.gameObject.SetActive(isActive);
        }

        /// <summary>
        /// 조이스틱의 입력 경로와 이동 범위를 적용합니다.
        /// </summary>
        /// <param name="joystickView">조이스틱 뷰입니다.</param>
        /// <param name="settings">모바일 HUD 설정입니다.</param>
        /// <param name="playerInput">플레이어 입력 컴포넌트입니다.</param>
        /// <param name="bindingResolver">액션-컨트롤 경로 해석기입니다.</param>
        private static void ApplyJoystick(MobileHudJoystickView joystickView, GGemCoMobileHudSettings settings, PlayerInput playerInput, MobileInputBindingResolver bindingResolver)
        {
            if (joystickView == null)
            {
                return;
            }

            joystickView.EnsureReferences();
            joystickView.SetMovementRange(settings.joystickMovementRange);

            OnScreenStick stick = joystickView.OnScreenStick;
            if (stick == null)
            {
                return;
            }

            string targetPath = bindingResolver.ResolveMoveControlPath(playerInput);
            if (!string.Equals(stick.controlPath, targetPath, StringComparison.Ordinal))
            {
                if (!stick.isActiveAndEnabled || stick.control == null)
                {
                    stick.controlPath = targetPath;
                }
            }

            stick.movementRange = settings.joystickMovementRange;
            MobileOnScreenUtility.TryEnableIsolatedStickInput(stick);
        }

        /// <summary>
        /// 기본 HUD 버튼들의 입력 경로를 적용합니다.
        /// </summary>
        /// <param name="rootView">HUD 루트 뷰입니다.</param>
        /// <param name="playerInput">플레이어 입력 컴포넌트입니다.</param>
        /// <param name="bindingResolver">액션-컨트롤 경로 해석기입니다.</param>
        private static void ApplyButtons(MobileHudRootView rootView, PlayerInput playerInput, MobileInputBindingResolver bindingResolver)
        {
            ApplyButton(rootView.AttackButtonView,
                bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionAttack, "<Gamepad>/buttonWest"));
            ApplyButton(rootView.JumpButtonView,
                bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionJump, "<Gamepad>/buttonSouth"));
            ApplyButton(rootView.DashButtonView,
                bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionDash, "<Gamepad>/buttonEast"));
            ApplyButton(rootView.GuardButtonView,
                bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionGuard, "<Gamepad>/leftShoulder"));
            ApplyButton(rootView.InteractionButtonView,
                bindingResolver.ResolveButtonControlPath(playerInput, ConfigCommonControl.NameActionInteraction, "<Gamepad>/rightShoulder"));
        }

        /// <summary>
        /// 개별 버튼의 입력 경로를 적용합니다.
        /// </summary>
        /// <param name="buttonView">대상 버튼 뷰입니다.</param>
        /// <param name="controlPath">적용할 컨트롤 경로입니다.</param>
        private static void ApplyButton(MobileHudButtonView buttonView, string controlPath)
        {
            if (buttonView == null)
            {
                return;
            }

            buttonView.EnsureReferences();
            if (buttonView.OnScreenButton == null)
            {
                return;
            }

            if (string.Equals(buttonView.OnScreenButton.controlPath, controlPath, StringComparison.Ordinal))
            {
                return;
            }

            if (!buttonView.OnScreenButton.isActiveAndEnabled || buttonView.OnScreenButton.control == null)
            {
                buttonView.OnScreenButton.controlPath = controlPath;
            }
        }
    }
}
