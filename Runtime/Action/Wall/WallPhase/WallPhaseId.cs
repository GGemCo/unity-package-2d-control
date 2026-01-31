namespace GGemCo2DControl
{
    /// <summary>
    /// 벽 액션(Wall Action) 상태 머신에서 사용하는 Phase 식별자입니다.
    /// </summary>
    /// <remarks>
    /// Phase 값은 전이 로직에서의 가독성과 확장성을 고려해 간격을 두고 정의됩니다.
    /// 새로운 벽 액션 Phase(예: LedgeGrab, CornerClimb)를 추가할 경우 이 열거형에 함께 확장합니다.
    /// </remarks>
    public enum WallPhaseId
    {
        /// <summary>
        /// 유효한 벽 Phase가 없는 초기 또는 비활성 상태입니다.
        /// </summary>
        None = 0,

        /// <summary>
        /// 벽에 매달려 정지해 있는 상태입니다.
        /// </summary>
        Hang = 10,

        /// <summary>
        /// 벽을 따라 아래로 미끄러지는 상태입니다.
        /// </summary>
        Slide = 20,

        /// <summary>
        /// Slide 도중 벽과의 접촉이 끊길 때 진입하는 종료 Phase입니다.
        /// </summary>
        /// <remarks>
        /// 벽에서 살짝 튕겨 나오듯 이탈하는 연출/물리를 처리하는 용도로 사용됩니다.
        /// </remarks>
        SlideEnd = 25,

        /// <summary>
        /// 벽을 기준으로 점프를 수행하는 상태입니다.
        /// </summary>
        Jump = 30,

        /// <summary>
        /// 반대편에 붙을 수 있는 벽이 없을 때의 종료 Phase입니다.
        /// </summary>
        /// <remarks>
        /// Wall Action을 종료하고, 일반 점프(ActionJump 등) 상태로
        /// 제어권을 인계하는 전이 지점으로 사용됩니다.
        /// </remarks>
        JumpEnd = 35,

        // Future 확장용 Phase
        // LedgeGrab = 40,
        // CornerClimb = 50,
    }
}