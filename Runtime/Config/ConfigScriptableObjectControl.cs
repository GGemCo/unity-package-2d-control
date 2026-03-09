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
            PlayerAction
        }

        /// <summary>
        /// Control 패키지 내부 메뉴 정렬 순서
        /// </summary>
        public enum ControlLocalOrder
        {
            AttackCombo,
            PlayerAction,
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
            };

        /// <summary>
        /// 파일명 기준으로 타입을 조회하기 위한 매핑
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Type> SettingsTypes =
            new Dictionary<string, Type>
            {
                { AttackCombo.FileName, typeof(GGemCoAttackComboSettings) },
                { PlayerAction.FileName, typeof(GGemCoPlayerActionSettings) },
            };

        /// <summary>
        /// 설정 키로 메뉴 정보를 조회한다.
        /// </summary>
        public static ConfigScriptableObjectCommon.MenuInfo GetInfo(ControlSettingsKey key)
        {
            return Infos[key];
        }

        /// <summary>
        /// 파일명으로 설정 타입을 조회한다.
        /// </summary>
        public static bool TryGetSettingsType(string fileName, out Type settingsType)
        {
            return SettingsTypes.TryGetValue(fileName, out settingsType);
        }
    }
}