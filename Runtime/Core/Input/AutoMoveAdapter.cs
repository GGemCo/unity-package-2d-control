using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// Core의 AutoMove 기능과 Control 입력/WallAction을 연결하는 어댑터입니다.
    /// - 이동 벡터 오버라이드
    /// - 버튼 입력 차단 여부(Provider 정책)
    /// - Wall Action 진행 중 AutoMove Suspend(Resume 가능) 처리
    /// </summary>
    internal sealed class AutoMoveAdapter
    {
        private readonly IAutoMoveVectorProvider _provider;
        private readonly IAutoMoveSuspendService _suspend;

        private AutoMoveSuspendToken _wallSuspendToken;
        private AutoMoveSuspendToken _guardSuspendToken;
        private AutoMoveSuspendToken _playerAttackRangeSuspendToken;
        private bool _isSuspendedByWall;
        private bool _isSuspendedByGuard;
        private bool _isSuspendedByPlayerAttackRange;

        public AutoMoveAdapter(IAutoMoveVectorProvider provider, IAutoMoveSuspendService suspend)
        {
            _provider = provider;
            _suspend = suspend;
            _wallSuspendToken = AutoMoveSuspendToken.None;
            _guardSuspendToken = AutoMoveSuspendToken.None;
            _playerAttackRangeSuspendToken = AutoMoveSuspendToken.None;
            _isSuspendedByWall = false;
            _isSuspendedByGuard = false;
            _isSuspendedByPlayerAttackRange = false;
        }

        public bool IsAutoMoveActive => _provider is { IsAutoMoveActive: true };

        /// <summary>
        /// AutoMove 활성 상태에서, 입력을 Provider에 통지하고 차단 여부를 반환합니다.
        /// </summary>
        public bool ShouldBlockInput(AutoMoveInputType inputType, Vector2 value)
        {
            if (_provider == null || !_provider.IsAutoMoveActive) return false;

            _provider.NotifyPlayerInput(inputType, value);
            return _provider.ShouldBlockInput(inputType);
        }

        /// <summary>
        /// Move 입력을 AutoMove 정책에 따라 오버라이드합니다.
        /// </summary>
        public Vector2 ResolveMove(Vector2 rawMove)
        {
            if (_provider == null || !_provider.IsAutoMoveActive) return rawMove;

            if (rawMove != Vector2.zero)
            {
                _provider.NotifyPlayerInput(AutoMoveInputType.Move, rawMove);
            }

            // 입력 통지 후에도 AutoMove가 유지될 수 있으므로 재확인
            return _provider.IsAutoMoveActive ? _provider.GetMoveVector() : rawMove;
        }

        /// <summary>
        /// Wall Action 진행 중에는 AutoMove를 Suspend(Resume 가능한 Pause)합니다.
        /// </summary>
        public void TickSuspendByWall(bool wallActive)
        {
            if (_suspend == null) return;

            if (wallActive)
            {
                if (!_isSuspendedByWall)
                {
                    _wallSuspendToken = _suspend.AcquireSuspend(AutoMoveSuspendReason.WallAction);
                    _isSuspendedByWall = _wallSuspendToken.IsValid;
                }
            }
            else
            {
                if (_isSuspendedByWall)
                {
                    _suspend.ReleaseSuspend(_wallSuspendToken);
                    _wallSuspendToken = AutoMoveSuspendToken.None;
                    _isSuspendedByWall = false;
                }
            }
        }
        public void TickSuspendByGuard(bool guardActive)
        {
            if (_suspend == null) return;

            if (guardActive)
            {
                if (!_isSuspendedByGuard)
                {
                    _guardSuspendToken = _suspend.AcquireSuspend(AutoMoveSuspendReason.GuardAction);
                    _isSuspendedByGuard = _guardSuspendToken.IsValid;
                }
            }
            else
            {
                if (_isSuspendedByGuard)
                {
                    _suspend.ReleaseSuspend(_guardSuspendToken);
                    _guardSuspendToken = AutoMoveSuspendToken.None;
                    _isSuspendedByGuard = false;
                }
            }
        }
        /// <summary>
        /// 플레이어 공격 거리에 몬스터가 있는 체크한 후 
        /// </summary>
        /// <param name="active"></param>
        public void TickSuspendByPlayerAttackRange(bool active)
        {
            if (_suspend == null) return;

            if (active)
            {
                if (!_isSuspendedByPlayerAttackRange)
                {
                    _playerAttackRangeSuspendToken = _suspend.AcquireSuspend(AutoMoveSuspendReason.PlayerAttackRange);
                    _isSuspendedByPlayerAttackRange = _playerAttackRangeSuspendToken.IsValid;
                }
            }
            else
            {
                if (_isSuspendedByPlayerAttackRange)
                {
                    _suspend.ReleaseSuspend(_playerAttackRangeSuspendToken);
                    _playerAttackRangeSuspendToken = AutoMoveSuspendToken.None;
                    _isSuspendedByPlayerAttackRange = false;
                }
            }
        }

        public void ReleaseAll()
        {
            if (_suspend == null) return;
            if (_wallSuspendToken.IsValid)
            {
                _suspend.ReleaseSuspend(_wallSuspendToken);
                _wallSuspendToken = AutoMoveSuspendToken.None;
            }

            if (_guardSuspendToken.IsValid)
            {
                _suspend.ReleaseSuspend(_guardSuspendToken);
                _guardSuspendToken = AutoMoveSuspendToken.None;
            }

            if (_playerAttackRangeSuspendToken.IsValid)
            {
                _suspend.ReleaseSuspend(_playerAttackRangeSuspendToken);
                _playerAttackRangeSuspendToken = AutoMoveSuspendToken.None;
            }

            _isSuspendedByWall = false;
            _isSuspendedByGuard = false;
            _isSuspendedByPlayerAttackRange = false;
        }
    }
}
