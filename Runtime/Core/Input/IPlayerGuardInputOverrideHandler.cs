namespace GGemCo2DControl
{
    /// <summary>
    /// 조작 잠금 검사와 일반 가드 처리보다 먼저 프로젝트 전용 가드 입력을 선처리하는 포트입니다.
    /// </summary>
    /// <remarks>
    /// 피격 또는 Crowd Control 중에도 사용할 수 있는 긴급 행동처럼 명시적인 선처리가 필요한 기능만 구현합니다.
    /// 구현체는 실제 행동이 시작된 경우에만 입력을 소비해야 하며, 실행할 수 없으면 일반 가드 흐름으로 반환해야 합니다.
    /// </remarks>
    public interface IPlayerGuardInputPreprocessor
    {
        /// <summary>
        /// 조작 잠금 검사 전에 가드 버튼 입력을 프로젝트 전용 규칙으로 처리합니다.
        /// </summary>
        /// <returns>
        /// 프로젝트 전용 행동이 실제로 시작되어 일반 가드를 건너뛰어야 하면 <see langword="true"/>,
        /// 기존 가드 처리를 계속해야 하면 <see langword="false"/>입니다.
        /// </returns>
        bool TryHandleGuardInputBeforeControlLock();
    }

    /// <summary>
    /// 기본 가드 입력을 실행하기 전에 프로젝트 전용 입력 규칙이 가드 입력을 선점할 수 있도록 제공하는 포트입니다.
    /// </summary>
    /// <remarks>
    /// Control 패키지는 이 인터페이스만 호출하고, 실제 스킬/프로젝트 전용 처리는 상위 계층 컴포넌트에서 구현합니다.
    /// </remarks>
    public interface IPlayerGuardInputOverrideHandler
    {
        /// <summary>
        /// 가드 버튼 입력을 프로젝트 전용 규칙으로 처리합니다.
        /// </summary>
        /// <returns>true이면 입력을 소비하여 기본 가드 처리를 건너뛰고, false이면 기존 가드 입력 처리를 계속 진행합니다.</returns>
        bool TryHandleGuardInput();
    }
}
