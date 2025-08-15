using GGemCo2DCoreEditor;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DControlEditor
{
    public class DefaultSettingsToolControl : DefaultEditorWindow
    {
        public float buttonWidth;
        public float buttonHeight;
        
        private Vector2 _scrollPosition;
        
        private SettingGGemCoControl _settingGGemCoControl;
        private CreateInputAction _createInputAction;

        [MenuItem(ConfigEditorControl.NameToolSettingDefault, false, (int)ConfigEditorControl.ToolOrdering.DefaultSetting)]
        public static void ShowWindow()
        {
            GetWindow<DefaultSettingsToolControl>("기본 셋팅하기");
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            buttonHeight = 40f;
            _settingGGemCoControl = new SettingGGemCoControl(this);
            _createInputAction = new CreateInputAction(this);
        }

        private void OnGUI()
        {
            buttonWidth = position.width / 2f - 10f;
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            EditorGUILayout.BeginHorizontal();
            _settingGGemCoControl.OnGUI();
            _createInputAction.OnGUI();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(20);
            EditorGUILayout.EndScrollView();
        }
    }
}