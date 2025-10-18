using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 시뮬레이션 툴(건설/농사/타일조작 등) 입력 트리거용 최소 계약
    /// </summary>
    public interface IToolAction : IPlayerAction
    {
        /// <summary>InputManager가 UI 위 클릭 등 사전 필터링 후 호출</summary>
        void UseTool(InputAction.CallbackContext ctx);

        /// <summary>툴이 지속 상태인지(프리뷰/드래그 등)</summary>
        bool IsActive { get; }

        /// <summary>사망/장면전환/상태전이 시 강제 종료</summary>
        void Cancel();
    }
}