using System;
using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DControl
{
    /// <summary>
    /// Control 패키지 ScriptableObject 메뉴 설정 정의
    /// </summary>
    public static class ConfigScriptableObjectControl
    {
        /// <summary>
        /// Control 패키지 Settings 식별 키
        /// </summary>
        public enum ControlSettingsKey
        {
            AttackCombo,
            PlayerAction,
            PlayerGuard,
            MobileHud,
        }

        /// <summary>
        /// Control 패키지 내부 메뉴 정렬 순서
        /// </summary>
        public enum ControlLocalOrder
        {
            AttackCombo,
            PlayerAction,
            PlayerGuard,
            MobileHud,
        }

        public const string BasePath = ConfigDefine.NameSDK + "/Settings/";
        public const string BaseName = ConfigDefine.NameSDK;

        public static class AttackCombo
        {
            public const string FileName = BaseName + "AttackComboSettings";
            public const string MenuName = BasePath + FileName;
            public const int Ordering =
                (int)ConfigScriptableObjectCommon.PackageOrder.Control +
                (int)ControlLocalOrder.AttackCombo;
        }

        public static class PlayerAction
        {
            public const string FileName = BaseName + "PlayerActionSettings";
            public const string MenuName = BasePath + FileName;
            public const int Ordering =
                (int)ConfigScriptableObjectCommon.PackageOrder.Control +
                (int)ControlLocalOrder.PlayerAction;
        }

        public static class PlayerGuard
        {
            public const string FileName = BaseName + "PlayerGuardSettings";
            public const string MenuName = BasePath + FileName;
            public const int Ordering =
                (int)ConfigScriptableObjectCommon.PackageOrder.Control +
                (int)ControlLocalOrder.PlayerGuard;
        }

        public static class MobileHud
        {
            public const string FileName = BaseName + "MobileHudSettings";
            public const string MenuName = BasePath + FileName;
            public const int Ordering =
                (int)ConfigScriptableObjectCommon.PackageOrder.Control +
                (int)ControlLocalOrder.MobileHud;
        }

        /// <summary>
        /// Control 패키지 전체 메뉴 메타데이터
        /// </summary>
        public static readonly IReadOnlyDictionary<ControlSettingsKey, ConfigScriptableObjectCommon.MenuInfo> Infos =
            new Dictionary<ControlSettingsKey, ConfigScriptableObjectCommon.MenuInfo>
            {
                {
                    ControlSettingsKey.AttackCombo,
                    new ConfigScriptableObjectCommon.MenuInfo(
                        AttackCombo.FileName,
                        AttackCombo.MenuName,
                        AttackCombo.Ordering,
                        typeof(GGemCoAttackComboSettings))
                },
                {
                    ControlSettingsKey.PlayerAction,
                    new ConfigScriptableObjectCommon.MenuInfo(
                        PlayerAction.FileName,
                        PlayerAction.MenuName,
                        PlayerAction.Ordering,
                        typeof(GGemCoPlayerActionSettings))
                },
                {
                    ControlSettingsKey.PlayerGuard,
                    new ConfigScriptableObjectCommon.MenuInfo(
                        PlayerGuard.FileName,
                        PlayerGuard.MenuName,
                        PlayerGuard.Ordering,
                        typeof(GGemCoPlayerGuardSettings))
                },
                {
                    ControlSettingsKey.MobileHud,
                    new ConfigScriptableObjectCommon.MenuInfo(
                        MobileHud.FileName,
                        MobileHud.MenuName,
                        MobileHud.Ordering,
                        typeof(GGemCoMobileHudSettings))
                },
            };

        /// <summary>
        /// 파일명 기준으로 타입을 조회하기 위한 매핑
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Type> SettingsTypes =
            new Dictionary<string, Type>
            {
                { AttackCombo.FileName, typeof(GGemCoAttackComboSettings) },
                { PlayerAction.FileName, typeof(GGemCoPlayerActionSettings) },
                { PlayerGuard.FileName, typeof(GGemCoPlayerGuardSettings) },
                { MobileHud.FileName, typeof(GGemCoMobileHudSettings) },
            };

        public static ConfigScriptableObjectCommon.MenuInfo GetInfo(ControlSettingsKey key)
        {
            return Infos[key];
        }

        public static bool TryGetSettingsType(string fileName, out Type settingsType)
        {
            return SettingsTypes.TryGetValue(fileName, out settingsType);
        }
    }
}
