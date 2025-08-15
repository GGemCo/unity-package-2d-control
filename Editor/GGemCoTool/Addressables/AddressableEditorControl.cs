using UnityEditor;
using UnityEngine;
using GGemCo2DCoreEditor;

namespace GGemCo2DControlEditor
{
    public class AddressableEditorControl : DefaultEditorWindow
    {
        private const string Title = "Addressable 셋팅하기";
        
        private SettingScriptableObjectControl _settingScriptableObjectControl;
        private SettingInputAction _settingInputAction;
        
        public float buttonWidth;
        public float buttonHeight;
        
        private Vector2 _scrollPosition;

        [MenuItem(ConfigEditorControl.NameToolSettingAddressable, false, (int)ConfigEditorControl.ToolOrdering.SettingAddressable)]
        public static void ShowWindow()
        {
            GetWindow<AddressableEditorControl>(Title);
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            buttonHeight = 40f;
            _settingScriptableObjectControl = new SettingScriptableObjectControl(this);
            _settingInputAction = new SettingInputAction(this);
        }
        private void OnGUI()
        {
            buttonWidth = position.width / 2f - 10f;
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            // EditorGUILayout.HelpBox("캐릭터 추가 후 맵을 추가해야 맵별 배치되어있는 캐릭터 정보가 반영됩니다.", MessageType.Error);
            
            EditorGUILayout.BeginHorizontal();
            _settingScriptableObjectControl.OnGUI();
            _settingInputAction.OnGUI();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(20);
            EditorGUILayout.EndScrollView();
        }
    }
}