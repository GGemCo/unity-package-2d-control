namespace GGemCo2DControl
{
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
