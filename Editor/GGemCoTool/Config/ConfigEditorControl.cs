using GGemCo2DCore;

namespace GGemCo2DControlEditor
{
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
        private const string NameToolGGemCo = ConfigDefine.NameSDK+"ToolControl/";
        // 기본 셋팅하기
        private const string NameToolSettings = NameToolGGemCo + "설정하기/";
        public const string NameToolSettingDefault = NameToolSettings + "기본 셋팅하기";
        public const string NameToolSettingAddressable = NameToolSettings + "Addressable 셋팅하기";
        public const string NameToolSettingScenePreIntro = NameToolSettings + "Pre 인트로 씬 셋팅하기";
        public const string NameToolSettingSceneGame = NameToolSettings + "게임 씬 셋팅하기";
        
        // 개발툴
        private const string NameToolDevelopment = NameToolGGemCo + "개발툴/";
        
        // 테스트
        private const string NameToolTest = NameToolGGemCo + "테스트툴/";
        
        // etc
        private const string NameToolEtc = NameToolGGemCo + "기타/";
    }
}