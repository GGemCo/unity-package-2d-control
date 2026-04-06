using System;

namespace GGemCo2DControl
{
    /// <summary>
    /// 플레이어 탈진 상태를 외부 시스템(UI 등)에 노출하는 포트입니다.
    /// </summary>
    public interface IPlayerExhaustionStateSource
    {
        /// <summary>
        /// 현재 탈진 상태인지 여부입니다.
        /// </summary>
        bool IsExhausting { get; }

        /// <summary>
        /// 탈진 상태가 시작/종료될 때 호출됩니다.
        /// - true: 탈진 시작
        /// - false: 탈진 종료
        /// </summary>
        event Action<bool> ExhaustionStateChanged;
    }
}
