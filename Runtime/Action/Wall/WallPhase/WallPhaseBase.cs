using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 벽(Wall) 상태 머신에서 각 Phase(단계)의 공통 동작을 정의하는 추상 기본 클래스입니다.
    /// </summary>
    /// <remarks>
    /// 파생 클래스는 특정 벽 동작 단계(예: 진입, 유지, 이탈 등)를 구현하며,
    /// 필요에 따라 Enter/Tick/FixedTick/Exit 메서드를 재정의합니다.
    /// </remarks>
    internal abstract class WallPhaseBase : IWallPhase
    {
        private const float MoveInputThreshold = 0.05f;

        /// <summary>
        /// 현재 Wall Phase를 식별하는 고유 ID입니다.
        /// </summary>
        public abstract WallPhaseId Id { get; }

        /// <summary>
        /// 해당 Phase에 진입할 때 한 번 호출됩니다.
        /// </summary>
        /// <param name="ctx">현재 벽 동작의 컨텍스트 정보를 담고 있는 객체입니다.</param>
        public virtual void Enter(ActionWall ctx) { }

        /// <summary>
        /// 매 프레임 호출되며, 입력에 따른 논리 처리를 수행합니다.
        /// </summary>
        /// <param name="ctx">현재 벽 동작의 컨텍스트 정보를 담고 있는 객체입니다.</param>
        /// <param name="moveInput">플레이어의 이동 입력 값입니다.</param>
        public virtual void Tick(ActionWall ctx, Vector2 moveInput) { }

        /// <summary>
        /// FixedUpdate 주기에 맞춰 호출되며, 물리 연산과 관련된 처리를 수행합니다.
        /// </summary>
        /// <param name="ctx">현재 벽 동작의 컨텍스트 정보를 담고 있는 객체입니다.</param>
        /// <param name="moveInput">플레이어의 이동 입력 값입니다.</param>
        public virtual void FixedTick(ActionWall ctx, Vector2 moveInput) { }

        /// <summary>
        /// 해당 Phase에서 벗어날 때 한 번 호출됩니다.
        /// </summary>
        /// <param name="ctx">현재 벽 동작의 컨텍스트 정보를 담고 있는 객체입니다.</param>
        public virtual void Exit(ActionWall ctx) { }

        /// <summary>
        /// 이동 입력이 거의 없는 상태인지 판별합니다.
        /// </summary>
        /// <param name="moveInput">플레이어의 이동 입력 값입니다.</param>
        /// <returns>
        /// X, Y 축 입력 값이 모두 임계값 미만이면 true를 반환합니다.
        /// </returns>
        protected static bool HasNoMoveInput(Vector2 moveInput)
            => Mathf.Abs(moveInput.x) < MoveInputThreshold && Mathf.Abs(moveInput.y) < MoveInputThreshold;
    }
}