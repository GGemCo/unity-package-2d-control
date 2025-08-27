#if UNITY_EDITOR
using System.IO;
using GGemCo2DControl;
using GGemCo2DCoreEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities; // (선택) 일부 확장에 필요

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
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            // 존재 시 덮어쓰기 확인
            if (File.Exists(path))
            {
                bool result = EditorUtility.DisplayDialog("덮어쓰기", "이미 생성된 Input Action 파일이 있습니다.\n덮어 씌우시겠습니까?", "네", "아니요");
                if (!result) return;
            }

            // 1) Asset 및 Control Schemes 구성
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();

            // Control Schemes (InputActionAsset 레벨에 정의)
            asset.AddControlScheme(new InputControlScheme("Keyboard&Mouse")
                .WithRequiredDevice("<Keyboard>")
                .WithRequiredDevice("<Mouse>"));

            asset.AddControlScheme(new InputControlScheme("Gamepad")
                .WithRequiredDevice("<Gamepad>"));

            // 2) Action Map & Actions
            var map = new InputActionMap("Player");

            var move   = map.AddAction("Move",   InputActionType.Value, expectedControlLayout: "Vector2");
            var attack = map.AddAction("Attack", InputActionType.Button);
            var jump   = map.AddAction("Jump",   InputActionType.Button);

            // --- Keyboard&Mouse 바인딩 ---
            // Move: 2DVector(WASD) 컴포지트
            int startIndex = move.bindings.Count; // 이후 추가될 컴포지트 구간 인덱스 추적
            move.AddCompositeBinding("2DVector") // 컴포지트 자체
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            int endIndex = move.bindings.Count;
            // 방금 추가한 컴포지트(+파츠) 구간에 그룹 지정: "Keyboard&Mouse"
            for (int i = startIndex; i < endIndex; i++)
                move.ChangeBinding(i).WithGroup("Keyboard&Mouse"); // 공식 확장 API로 groups 지정 :contentReference[oaicite:4]{index=4}

            // Attack / Jump 키보드 바인딩
            attack.AddBinding("<Keyboard>/space", groups: "Keyboard&Mouse");
            jump.AddBinding("<Keyboard>/f",       groups: "Keyboard&Mouse");

            // --- Gamepad 바인딩 ---
            // Move: 왼쪽 스틱 전체(단순 바인딩)
            move.AddBinding("<Gamepad>/leftStick", groups: "Gamepad");
            // 필요 시 아날로그 모드의 2DVector로도 가능:
            // move.AddCompositeBinding("2DVector(mode=2)")
            //     .With("Up",    "<Gamepad>/leftStick/up")
            //     .With("Down",  "<Gamepad>/leftStick/down")
            //     .With("Left",  "<Gamepad>/leftStick/left")
            //     .With("Right", "<Gamepad>/leftStick/right");
            // 위처럼 컴포지트 사용 시에도 ChangeBinding(i).WithGroup("Gamepad")로 그룹 지정 가능 :contentReference[oaicite:5]{index=5}

            // Attack / Jump 게임패드 바인딩
            attack.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad"); // A / Cross
            jump.AddBinding("<Gamepad>/buttonEast",    groups: "Gamepad"); // B / Circle

            asset.AddActionMap(map);

            // 3) 저장 (JSON) 및 임포트
            var json = asset.ToJson(); // .inputactions 포맷(JSON) 추출 :contentReference[oaicite:6]{index=6}
            File.WriteAllText(path, json);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Debug.Log($"Created JSON InputActionAsset with Control Schemes: {path}");
        }
    }
}
#endif
