namespace GGemCo2DControl
{
    /// <summary>
    /// 물리적인 공격 버튼 입력을 어떤 논리 입력으로 처리할지 나타냅니다.
    /// </summary>
    public enum PlayerAttackInputRoute
    {
        /// <summary>
        /// 공격 버튼을 기본 공격 입력으로 처리합니다.
        /// </summary>
        Attack = 0,

        /// <summary>
        /// 공격 버튼을 점프 입력으로 처리합니다.
        /// </summary>
        Jump = 1,
    }

    /// <summary>
    /// 상위 게임 계층이 현재 문맥에 따라 공격 버튼의 논리 입력을 결정할 수 있도록 제공하는 포트입니다.
    /// </summary>
    /// <remarks>
    /// Control 패키지는 전투 여부 같은 게임 전용 규칙을 알지 않으며,
    /// 활성화된 구현체가 경로를 제공하지 않으면 기본 공격 입력을 사용합니다.
    /// </remarks>
    public interface IPlayerAttackInputRouteProvider
    {
        /// <summary>
        /// 현재 공격 버튼 입력에 적용할 논리 입력 경로를 반환합니다.
        /// </summary>
        /// <param name="route">공격 버튼에 적용할 논리 입력 경로입니다.</param>
        /// <returns>현재 구현체가 입력 경로를 결정했으면 <see langword="true"/>입니다.</returns>
        bool TryResolveAttackInputRoute(out PlayerAttackInputRoute route);
    }
}
