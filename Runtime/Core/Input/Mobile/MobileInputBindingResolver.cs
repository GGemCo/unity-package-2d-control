using System;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 기존 InputAction 바인딩에서 모바일 HUD가 사용할 control path를 해석합니다.
    /// </summary>
    public sealed class MobileInputBindingResolver
    {
        public string ResolveMoveControlPath(PlayerInput playerInput)
        {
            InputAction action = playerInput != null ? playerInput.actions?.FindAction(ConfigCommonControl.NameActionMove) : null;
            string path = TryGetFirstGamepadBindingPath(action);
            return string.IsNullOrWhiteSpace(path) ? "<Gamepad>/leftStick" : path;
        }

        public string ResolveButtonControlPath(PlayerInput playerInput, string actionName, string fallback)
        {
            InputAction action = playerInput != null ? playerInput.actions?.FindAction(actionName) : null;
            string path = TryGetFirstGamepadBindingPath(action);
            return string.IsNullOrWhiteSpace(path) ? fallback : path;
        }

        private static string TryGetFirstGamepadBindingPath(InputAction action)
        {
            if (action == null)
            {
                return null;
            }

            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                {
                    continue;
                }

                string path = binding.effectivePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = binding.path;
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (path.IndexOf("<Gamepad>", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return path;
                }
            }

            return null;
        }
    }
}
