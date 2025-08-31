using System;
using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DControl
{
    /// <summary>
    /// ScriptableObject 관련 설정 정의
    /// </summary>
    public static class ConfigScriptableObjectControl
    {
        public static class AttackCombo
        {
            public const string FileName = ConfigScriptableObject.BaseName + "AttackComboSettings";
            public const string MenuName = ConfigScriptableObject.BasePath + FileName;
            public const int Ordering = (int)ConfigScriptableObject.MenuOrdering.AttackComboSettings;
        }

        public static readonly Dictionary<string, Type> SettingsTypes = new()
        {
            { AttackCombo.FileName, typeof(GGemCoAttackComboSettings) },
        };
    }
}