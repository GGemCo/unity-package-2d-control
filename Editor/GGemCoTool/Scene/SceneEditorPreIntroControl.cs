using GGemCo2DCore;
using GGemCo2DCoreEditor;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DControlEditor
{
    /// <summary>
    /// 인트로 씬 설정 툴
    /// </summary>
    public class SceneEditorPreIntroControl : DefaultSceneEditorControl
    {
        private const string Title = "Pre 인트로 씬 셋팅하기";
        private GameObject _objGGemCoCore;
        
        [MenuItem(ConfigEditorControl.NameToolSettingScenePreIntro, false, (int)ConfigEditorControl.ToolOrdering.SettingScenePreIntro)]
        public static void ShowWindow()
        {
            GetWindow<SceneEditorPreIntroControl>(Title);
        }

        private void OnGUI()
        {
            if (!CheckCurrentLoadedScene(ConfigDefine.SceneNamePreIntro))
            {
                EditorGUILayout.HelpBox($"Pre 인트로 씬을 불러와 주세요.", MessageType.Error);
            }
            else
            {
                DrawRequiredSection();
            }
        }
        private void DrawRequiredSection()
        {
            Common.OnGUITitle("필수 항목");
            EditorGUILayout.HelpBox($"* GameLoaderManagerControl 오브젝트\n", MessageType.Info);
            if (GUILayout.Button("필수 항목 셋팅하기"))
            {
                SetupRequiredObjects();
            }
        }
        /// <summary>
        /// 필수 항목 셋팅
        /// </summary>
        private void SetupRequiredObjects()
        {
            _objGGemCoCore = GetOrCreateRootPackageGameObject();
            // GGemCo2DCore.ScenePreIntro GameObject 만들기
            GGemCo2DControl.GameLoaderManagerControl gameLoaderManagerControl =
                CreateOrAddComponent<GGemCo2DControl.GameLoaderManagerControl>(nameof(GGemCo2DControl.GameLoaderManagerControl));
            
            GGemCo2DCore.ScenePreIntro scene = CreateOrAddComponent<ScenePreIntro>(nameof(ScenePreIntro));
            // 반드시 SetDirty 처리해야 저장됨
            EditorUtility.SetDirty(scene);
        }
    }
}