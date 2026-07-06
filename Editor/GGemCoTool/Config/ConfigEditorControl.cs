using GGemCo2DCore;
using GGemCo2DCoreEditor;

namespace GGemCo2DControlEditor
{
    /// <summary>
    /// Control 패키지 에디터 툴 메뉴 경로와 정렬 순서를 정의합니다.
    /// </summary>
    public static class ConfigEditorControl
    {
        public enum ToolOrdering
        {
            DefaultSetting = 1,
            SettingAddressable,
            SettingScenePreIntro,
            SettingSceneGame,
            Development = 100,
            Test = 200,
            Etc = 900,
        }
        /// <summary>
        /// Control 패키지 툴 메뉴의 루트 경로입니다.
        /// </summary>
        private const string NameToolGGemCo = GGemCoToolMenu.Control;
        // 기본 셋팅하기
        private const string NameToolSettings = NameToolGGemCo + GGemCoToolMenu.Settings;
        public const string NameToolSettingDefault = NameToolSettings + "기본 셋팅하기";
        public const string NameToolSettingAddressable = NameToolSettings + "Addressable 셋팅하기";
        public const string NameToolSettingScenePreIntro = NameToolSettings + "Pre 인트로 씬 셋팅하기";
        public const string NameToolSettingSceneGame = NameToolSettings + "게임 씬 셋팅하기";
        
        // 개발툴
        private const string NameToolDevelopment = NameToolGGemCo + GGemCoToolMenu.Development;
        
        // 테스트
        private const string NameToolTest = NameToolGGemCo + GGemCoToolMenu.Test;
        
        // etc
        private const string NameToolEtc = NameToolGGemCo + GGemCoToolMenu.Etc;
    }
}
