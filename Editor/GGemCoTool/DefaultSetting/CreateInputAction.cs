#if UNITY_EDITOR
using System.IO;
using GGemCo2DControl;
using GGemCo2DCoreEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControlEditor
{
    public class CreateInputAction
    {
        private const string Title = "디폴트 Input Action 파일 생성하기";
        private readonly DefaultSettingsToolControl _defaultSettingsToolControl;
        public CreateInputAction(DefaultSettingsToolControl defaultSettingsToolControl)
        {
            _defaultSettingsToolControl = defaultSettingsToolControl;
        }
        public void OnGUI()
        {
            // Common.OnGUITitle(Title);

            if (GUILayout.Button(Title, GUILayout.Width(_defaultSettingsToolControl.buttonWidth), GUILayout.Height(_defaultSettingsToolControl.buttonHeight)))
            {
                Setup();
            }
        }

        private static void Setup()
        {
            string path = ConfigAddressableControl.InputAction.Path;

            // 폴더가 없으면 생성
            string directory = Path.GetDirectoryName(path);
            if (directory != null)
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }
            }

            if (File.Exists(path))
            {
                bool result = EditorUtility.DisplayDialog("덮어쓰기", "이미 생성된 Input Action 파일이 있습니다.\n덮어 씌우시겠습니까?", "네", "아니요");
                if (!result) return;
            }
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();

            var map = new InputActionMap("Player");

            var move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            var comp = move.AddCompositeBinding("2DVector");
            comp.With("Up", "<Keyboard>/w");
            comp.With("Down", "<Keyboard>/s");
            comp.With("Left", "<Keyboard>/a");
            comp.With("Right", "<Keyboard>/d");

            var attack = map.AddAction("Attack", InputActionType.Button, "<Keyboard>/space");

            asset.AddActionMap(map);

            // 핵심: ToJson() → 텍스트 파일로 저장
            var json = asset.ToJson();
            File.WriteAllText(path, json);

            // 임포터로 재가져오기
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Debug.Log($"Created JSON InputActionAsset: {path}");
        }
    }
}
#endif