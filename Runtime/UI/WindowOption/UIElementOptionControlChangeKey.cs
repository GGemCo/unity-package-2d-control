using System;
using System.Globalization;
using GGemCo2DCore;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace GGemCo2DControl
{
    /// <summary>
    /// 키 변경하기 element 
    /// </summary>
    public class UIElementOptionControlChangeKey : MonoBehaviour
    {
        [SerializeField, Tooltip("바인딩 대상 InputAction의 표시 이름입니다. Composite 파트인 경우 ‘(Up/Down/Left/Right)’처럼 세부 파트명이 함께 표기됩니다.")]
        private TextMeshProUGUI actionName;
        [SerializeField, Tooltip("현재 키/버튼 바인딩의 사람 친화적 표시 문자열입니다. 장치별 버튼명이 반영됩니다.")]
        private TextMeshProUGUI bindingLabel;
        [SerializeField, Tooltip("키 재지정을 시작합니다. 입력 대기 상태로 전환되며 ESC로 취소할 수 있습니다.")]
        private Button rebindButton;
        [SerializeField, Tooltip("현재 바인딩을 제거합니다. 미할당 상태가 될 수 있으니 주의하세요.")]
        private Button clearButton;
        [SerializeField, Tooltip("현재 바인딩을 프로젝트 기본값으로 복원합니다.")]
        private Button resetButton;
        [SerializeField] private InputBinding.DisplayStringOptions displayStringOptions;

        private InputAction _action;
        private int _bindingIndex;
        private PlayerInput _playerInput;
        private UIPanelOptionControl _uiPanelOptionControl;
        private UIWindowOption _uiWindowOption;

        public void Bind(InputAction action, InputBinding binding)
        {
            _action = action;
            _bindingIndex = action.bindings.IndexOf(b => b.id == binding.id);

            // 1) 액션명 + (Composite Part) 표시
            actionName.text = GetActionLabel(action, _bindingIndex);

            RefreshLabel();

            rebindButton.onClick.AddListener(StartRebind);
            clearButton.onClick.AddListener(ClearBinding);
            resetButton.onClick.AddListener(ResetBinding);
        }
        /// <summary>
        /// 액션명에 Composite Part 표시(예: "Move (Up)")
        /// Composite가 아닐 경우 액션명만 반환
        /// </summary>
        private static string GetActionLabel(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return string.Empty;

            var b = action.bindings[bindingIndex];
            if (b.isPartOfComposite)
            {
                // binding.name 예: "up", "down" -> 보기 좋게 가공
                var part = Nicify(b.name);
                return $"{action.name} ({part})";
            }
            return action.name;
        }

        /// <summary>
        /// "up", "arrow_left" 등을 "Up", "Arrow Left"로 가공
        /// </summary>
        private static string Nicify(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var spaced = raw.Replace('_', ' ').Replace('-', ' ');
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(spaced);
        }

        public void RefreshLabel()
        {
            if (_action == null) return;

            // 2) 표시 문자열은 GetBindingDisplayString 권장
            //    (장치별 실제 버튼명, 지역화 옵션, 장치명 포함/제외 등 제어 가능)
            var display = _action.GetBindingDisplayString(
                _bindingIndex,
                options: displayStringOptions
            );

            bindingLabel.text = string.IsNullOrEmpty(display) ? "-" : display;

            // (선택) Composite의 경우 공백 문자열이 나올 수 있어 후처리
            if (string.IsNullOrEmpty(bindingLabel.text))
                bindingLabel.text = "-";
        }
        /// <summary>
        /// 키 바꾸기 시작
        /// </summary>
        private void StartRebind()
        {
            if (_action == null) return;

            // 리바인딩은 대상 액션이 Disable일 때 시작 (완료/취소 후 복구)
            bool wasEnabled = _action.enabled;
            if (wasEnabled) _action.Disable();
            
            PopupMetadata popupMetadata = new PopupMetadata
            {
                PopupType = PopupManager.Type.Default,
                MessageColor = Color.red,
                Title = "Popup_Title_Change_Key", //슬롯 삭제
                Message = "Popup_Message_Change_Key",
                ShowConfirmButton = false,
                ShowCancelButton = false
            };
            _uiWindowOption.popupManager.ShowPopup(popupMetadata);
            
            var op = _action.PerformInteractiveRebinding(_bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f);

            if (_playerInput == null && SceneGame.Instance != null) 
            {
                _playerInput = SceneGame.Instance.player.GetComponent<PlayerInput>();
            }
            var scheme = _playerInput ? _playerInput.currentControlScheme : _uiPanelOptionControl.GetScheme();
            
            // (핵심) 현재 스킴에 맞는 컨트롤만 허용하는 필터
            if (scheme == ConfigCommonControl.NameControlSchemeGamepad)
            {
                // 게임패드 입력만 후보로 인정
                op.WithControlsHavingToMatchPath("<Gamepad>/*")
                    .WithExpectedControlType<AxisControl>(); // 필요에 따라 StickControl/ButtonControl 등
            }
            else // Keyboard&Mouse
            {
                // 마우스 포인터/델타 같은 노이즈 제외
                op.WithControlsHavingToMatchPath("<Keyboard>/*")
                    .WithControlsHavingToMatchPath("<Mouse>/*")
                    .WithControlsExcluding("<Mouse>/position")
                    .WithControlsExcluding("<Mouse>/delta");
            }

            op.OnComplete(o =>
                {
                    var ctrl = o.selectedControl;   // 이번에 선택된 실제 컨트롤
                    o.Dispose();
                    if (wasEnabled) _action.Enable();

                    ClearDuplicateBindingsAcrossAsset(ctrl, scheme);
                    _uiPanelOptionControl.MarkDirty(true);
                    RefreshLabel();
                    _uiWindowOption.popupManager.Cancel();
                })
                .OnCancel(o =>
                {
                    o.Dispose();
                    if (wasEnabled) _action.Enable();
                    RefreshLabel();
                    _uiWindowOption.popupManager.Cancel();
                })
                .Start();
        }
        /// <summary>
        /// 현재 지정된 키를 지워주기
        /// </summary>
        private void ClearBinding()
        {
            if (_action == null) return;
            _action.ApplyBindingOverride(_bindingIndex, "");
            _uiPanelOptionControl.MarkDirty(true);
            RefreshLabel();
        }
        /// <summary>
        /// asset에 지정된 디폴트 값으로 되돌리기
        /// </summary>
        private void ResetBinding()
        {
            if (_action == null) return;
            _action.RemoveBindingOverride(_bindingIndex);
            _uiPanelOptionControl.MarkDirty(true);
            RefreshLabel();
        }
        /// <summary>
        /// 윈도우, 패널 주입하기
        /// </summary>
        /// <param name="uiWindowOption"></param>
        /// <param name="uiPanelOptionControl"></param>
        public void SetUIWindowOption(UIWindowOption uiWindowOption, UIPanelOptionControl uiPanelOptionControl)
        {
            _uiWindowOption = uiWindowOption;
            _uiPanelOptionControl = uiPanelOptionControl;
        }
        
        /// <summary>
        /// 그룹 문자열 포함 여부 유틸
        /// </summary>
        /// <param name="groups"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        private static bool GroupsContains(string groups, string group)
        {
            if (string.IsNullOrEmpty(group)) return true;          // 스킴 미결정이면 전부 대상
            if (string.IsNullOrEmpty(groups)) return false;
            foreach (var g in groups.Split(';'))
                if (string.Equals(g.Trim(), group, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
        /// <summary>
        /// 중복 바인딩 클리어
        /// </summary>
        /// <param name="newControl"></param>
        /// <param name="bindingGroup"></param>
        private void ClearDuplicateBindingsAcrossAsset(InputControl newControl, string bindingGroup)
        {
            var asset = _action?.actionMap?.asset;
            if (asset == null || newControl == null) return;

            bool change = false;
            foreach (var map in asset.actionMaps)
            foreach (var act in map.actions)
            {
                for (int i = 0; i < act.bindings.Count; i++)
                {
                    if (act == _action && i == _bindingIndex) continue;

                    var b = act.bindings[i];
                    if (!GroupsContains(b.groups, bindingGroup)) continue;

                    var eff = b.effectivePath; // overridePath 없으면 path
                    if (string.IsNullOrEmpty(eff)) continue;

                    // 문자열 equals 대신 Matches 사용
                    if (InputControlPath.Matches(eff, newControl))
                    {
                        act.ApplyBindingOverride(i, ""); // 중복 해제(클리어)
                        change = true;
                    }
                }
            }
            if (change)
                _uiPanelOptionControl.RefreshAllLabels();
        }
    }
}
