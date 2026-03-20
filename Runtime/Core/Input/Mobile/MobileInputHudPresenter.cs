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
    /// HUD 프리팹 인스턴스화와 입력 바인딩 적용을 담당합니다.
    /// </summary>
    public sealed class MobileInputHudPresenter
    {
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

        public void Apply(MobileHudRootView rootView, GGemCoMobileHudSettings settings, PlayerInput playerInput, MobileInputBindingResolver bindingResolver)
        {
            if (rootView == null || settings == null)
            {
                return;
            }

            rootView.EnsureReferences();

            if (rootView.RootCanvasGroup != null)
            {
                rootView.RootCanvasGroup.interactable = true;
                rootView.RootCanvasGroup.blocksRaycasts = true;
            }

            rootView.SafeAreaFitter?.Initialize(rootView.SafeAreaRoot, settings.useSafeArea);

            ApplyJoystick(rootView.JoystickView, settings, playerInput, bindingResolver);
            ApplyButtons(rootView, playerInput, bindingResolver);
        }

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
