using GGemCo2DCore;

namespace GGemCo2DControl
{
    /// <summary>
    /// 플레이어 입력 콜백 단계에서 프로젝트 전용 조건으로 특정 입력을 차단할 수 있도록 제공하는 포트입니다.
    /// </summary>
    /// <remarks>
    /// Control 패키지는 입력 종류만 전달하고, 실제 차단 조건은 상위 계층 컴포넌트가 구현합니다.
    /// </remarks>
    public interface IPlayerInputBlockProvider
    {
        /// <summary>
        /// 지정한 입력 타입을 현재 프레임에서 차단해야 하는지 확인합니다.
        /// </summary>
        /// <param name="inputType">검사할 플레이어 입력 타입입니다.</param>
        /// <returns>입력을 차단하면 true입니다.</returns>
        bool ShouldBlockInput(AutoMoveInputType inputType);
    }
}
