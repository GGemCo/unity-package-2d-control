using GGemCo2DCore;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 시뮬레이션 툴 입력 처리 핸들러
    /// - 입력 시점에서 즉시 실행하지 않고, 다음 프레임에 UI 위 여부를 판정 후 실행합니다.
    /// </summary>
    internal sealed class SimulationToolInputHandler
    {
        private readonly InputManager _owner;
        private readonly CharacterBase _characterBase;
        private readonly ActionDash _actionDash;
        private readonly ActionJump _actionJump;
        private readonly ActionClimb _actionClimb;
        private readonly ActionPushPull _actionPushPull;
        private readonly System.Func<bool> _isWallLocked;

        private IToolAction _toolAction;
        private bool _pending;
        private InputAction.CallbackContext _pendingCtx;

        public SimulationToolInputHandler(
            InputManager owner,
            CharacterBase characterBase,
            ActionDash actionDash,
            ActionJump actionJump,
            ActionClimb actionClimb,
            ActionPushPull actionPushPull,
            System.Func<bool> isWallLocked)
        {
            _owner = owner;
            _characterBase = characterBase;
            _actionDash = actionDash;
            _actionJump = actionJump;
            _actionClimb = actionClimb;
            _actionPushPull = actionPushPull;
            _isWallLocked = isWallLocked;
        }

        public void SetToolAction(IToolAction toolAction)
        {
            _toolAction = toolAction;
        }

        public void Tick()
        {
            if (!_pending) return;
            _pending = false;

            if (UiPointerGuard.IsPointerOverUi()) return;
            if (_toolAction == null) return;

            _toolAction.UseTool(_pendingCtx);
        }

        /// <summary>
        /// 릴리즈(실제/가상) 확정 시점에 호출됩니다.
        /// - 가상 릴리즈일 경우, Press 시점 컨텍스트를 전달하는 것을 권장합니다.
        /// </summary>
        public void HandleResolved(InputAction.CallbackContext ctx)
        {
            if (_toolAction == null)
            {
                GcLogger.Log("Simulation ToolAction 이 주입되지 않았습니다. (SimulationActionInstaller 확인)");
                return;
            }

            if (_characterBase.IsStatusDead()) return;

            if (_isWallLocked != null && _isWallLocked())
            {
                GcLogger.Log("벽 상태 중 시뮬레이션 툴사용은 불가능 합니다.");
                return;
            }

            if (_characterBase.IsStatusDash() && _actionDash.IsDashing)
            {
                GcLogger.Log("대시 중 시뮬레이션 툴사용은 불가능 합니다.");
                return;
            }
            if (_characterBase.IsStatusJump() && _actionJump.IsJumping)
            {
                GcLogger.Log("점프 중 시뮬레이션 툴사용은 불가능 합니다.");
                return;
            }
            if (_characterBase.IsStatusClimb() && _actionClimb.IsClimbing)
            {
                GcLogger.Log("등반 중 시뮬레이션 툴사용은 불가능 합니다.");
                return;
            }
            if (_characterBase.IsStatusPush() && _actionPushPull.IsPushing)
            {
                GcLogger.Log("밀기 중 시뮬레이션 툴사용은 불가능 합니다.");
                return;
            }

            // 다음 프레임에서 UI 위 여부 확인 후 실행
            _pendingCtx = ctx;
            _pending = true;
        }

        // (레거시 호환) 기존 호출 지점을 위해 남겨둠
        public void OnSimulationTool(InputAction.CallbackContext ctx)
        {
            HandleResolved(ctx);
        }
    }
}
