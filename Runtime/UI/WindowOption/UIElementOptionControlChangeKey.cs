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
        [SerializeField] private TextMeshProUGUI actionName;
        [SerializeField] private TextMeshProUGUI bindingLabel;
        [SerializeField] private Button rebindButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button resetButton;

        private InputAction _action;
        private int _bindingIndex;
        private PlayerInput _playerInput;
        private UIPanelOptionControl _uiPanelOptionControl;
        private UIWindowOption _uiWindowOption;

        public void Bind(InputAction action, InputBinding binding, UIPanelOptionControl uiPanelOptionControl, UIWindowOption uiWindowOption)
        {
            _action = action;
            _bindingIndex = action.bindings.IndexOf(b => b.id == binding.id);
            _uiPanelOptionControl = uiPanelOptionControl;
            _uiWindowOption = uiWindowOption;
            
            actionName.text = action.name;
            RefreshLabel();

            rebindButton.onClick.AddListener(StartRebind);
            clearButton.onClick.AddListener(ClearBinding);
            resetButton.onClick.AddListener(ResetBinding);
        }

        public void RefreshLabel()
        {
            if (_action == null) return;
            var display = InputControlPath.ToHumanReadableString(
                _action.bindings[_bindingIndex].effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
            bindingLabel.text = string.IsNullOrEmpty(display) ? "-" : display;
        }

        private void OnCancel()
        {
            
        }
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
                Title = "키 변경하기", //슬롯 삭제
                Message = "변경하고 싶은 키를 눌러주세요.\n취소하고 싶은 경우 ESC키를 눌러주세요.",
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
            var scheme = _playerInput ? _playerInput.currentControlScheme : null;
            
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
                    o.Dispose();
                    if (wasEnabled) _action.Enable();
                    _uiPanelOptionControl.SetIsChange(true);
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

        private void ClearBinding()
        {
            if (_action == null) return;
            _action.ApplyBindingOverride(_bindingIndex, "");
            RefreshLabel();
        }

        private void ResetBinding()
        {
            if (_action == null) return;
            _action.RemoveBindingOverride(_bindingIndex);
            RefreshLabel();
        }
    }
}
