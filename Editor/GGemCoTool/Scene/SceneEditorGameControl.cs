using GGemCo2DCore;
using GGemCo2DCoreEditor;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DControlEditor
{
    /// <summary>
    /// 인트로 씬 설정 툴
    /// </summary>
    public class SceneEditorGameControl : DefaultSceneEditorControl
    {
        private const string Title = "Pre 인트로 씬 셋팅하기";
        private GameObject _objGGemCoCore;
        
        [MenuItem(ConfigEditorControl.NameToolSettingSceneGame, false, (int)ConfigEditorControl.ToolOrdering.SettingSceneGame)]
        public static void ShowWindow()
        {
            GetWindow<SceneEditorGameControl>(Title);
        }

        private void OnGUI()
        {
            if (!CheckCurrentLoadedScene(ConfigDefine.SceneNameGame))
            {
                EditorGUILayout.HelpBox($"게임 씬을 불러와 주세요.", MessageType.Error);
            }
            else
            {
                DrawRequiredSection();
            }
        }
        private void DrawRequiredSection()
        {
            Common.OnGUITitle("필수 항목");
            EditorGUILayout.HelpBox($"* ControlPackageManager 오브젝트\n", MessageType.Info);
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
            // GGemCo2DControl.ControlPackageManager GameObject 만들기
            GGemCo2DControl.ControlPackageManager controlPackageManager =
                CreateOrAddComponent<GGemCo2DControl.ControlPackageManager>(nameof(GGemCo2DControl.ControlPackageManager));
            
            // ControlPackageManager 은 싱글톤으로 활용하고 있어 root 로 이동
            controlPackageManager.gameObject.transform.SetParent(null);
            
            GGemCo2DCore.SceneGame scene = CreateOrAddComponent<SceneGame>(nameof(SceneGame));
            // 반드시 SetDirty 처리해야 저장됨
            EditorUtility.SetDirty(scene);
        }
    }
}