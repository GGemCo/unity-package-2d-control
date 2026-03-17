using System.Reflection;
using UnityEngine.InputSystem.OnScreen;

namespace GGemCo2DControl
{
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