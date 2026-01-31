using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 벽 액션 Phase(상태) 구현체가 따라야 하는 계약(Contract)입니다.
    /// </summary>
    /// <remarks>
    /// - <see cref="Tick(ActionWall, Vector2)"/>: 프레임 단(입력/타이머/상태 전이 판단 등)
    /// - <see cref="FixedTick(ActionWall, Vector2)"/>: 물리 단(Rigidbody2D 이동/속도 적용 등)
    /// </remarks>
    internal interface IWallPhase
    {
        /// <summary>
        /// Phase 식별자입니다.
        /// </summary>
        WallPhaseId Id { get; }

        /// <summary>
        /// Phase 진입 시 1회 호출됩니다.
        /// </summary>
        /// <param name="ctx">벽 액션 컨텍스트입니다.</param>
        void Enter(ActionWall ctx);

        /// <summary>
        /// 매 프레임 호출되며, 입력 샘플링/타이머 누적/전이 판단 등을 수행합니다.
        /// </summary>
        /// <param name="ctx">벽 액션 컨텍스트입니다.</param>
        /// <param name="moveInput">이동 입력 벡터입니다.</param>
        void Tick(ActionWall ctx, Vector2 moveInput);

        /// <summary>
        /// FixedUpdate 타이밍으로 호출되며, Rigidbody2D 기반 물리 이동/속도 적용 등을 수행합니다.
        /// </summary>
        /// <param name="ctx">벽 액션 컨텍스트입니다.</param>
        /// <param name="moveInput">이동 입력 벡터입니다.</param>
        void FixedTick(ActionWall ctx, Vector2 moveInput);

        /// <summary>
        /// Phase 종료 시 1회 호출됩니다.
        /// </summary>
        /// <param name="ctx">벽 액션 컨텍스트입니다.</param>
        void Exit(ActionWall ctx);
    }
}