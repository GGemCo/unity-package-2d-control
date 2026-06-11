using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 입력 허용/차단 및 상충 상태 정리(캔슬/취소 요청) 정책을 한 곳으로 모은 클래스.
    /// 
    /// 목적:
    /// - InputManager에 흩어진 "상태별 if"를 단일 정책으로 집약
    /// - Action 간 상충(대시 중 공격/점프, 점프 중 대시 등) 처리의 일관성 확보
    /// </summary>
    internal sealed class PlayerInputPolicy
    {
        private readonly CharacterBase _character;
        private readonly ActionAttack _attack;
        private readonly ActionDash _dash;
        private readonly ActionJump _jump;
        private readonly ActionClimb _climb;
        private readonly ActionPushPull _pushPull;
        private readonly ActionWall _wall;

        private readonly System.Func<ISkillCancelableDriver> _getSkillCancelable;
        private readonly System.Func<Vector2> _getMoveInput;

        // Settings snapshot(ApplySettings에서 갱신)
        public bool CanAttackPlayDashing { get; set; }
        public bool CanJumpPlayDashing { get; set; }
        public bool CanDashPlayJumping { get; set; }
        public bool CanClimbingPlayJumping { get; set; }
        public bool CanJumpPlayClimbing { get; set; }
        public bool CanDashPlayClimbing { get; set; }
        public bool CanJumpUseSkill { get; set; }
        public bool CanDashUseSkill { get; set; }
        public bool CanAttackPlayJump { get; set; }
        public AttackGuardCancelPolicy AttackGuardCancelPolicy { get; set; } = AttackGuardCancelPolicy.AttackAndComboWait;

        public PlayerInputPolicy(
            CharacterBase character,
            ActionAttack attack,
            ActionDash dash,
            ActionJump jump,
            ActionClimb climb,
            ActionPushPull pushPull,
            ActionWall wall,
            System.Func<ISkillCancelableDriver> getSkillCancelable,
            System.Func<Vector2> getMoveInput)
        {
            _character = character;
            _attack = attack;
            _dash = dash;
            _jump = jump;
            _climb = climb;
            _pushPull = pushPull;
            _wall = wall;
            _getSkillCancelable = getSkillCancelable;
            _getMoveInput = getMoveInput;
        }

        public bool TryPrepareAttack(out string denyLog)
        {
            denyLog = null;
            if (_character.IsStatusDead()) return false;

            // 벽 상태 중 공격 정책(현재는 금지)
            if (_wall is { IsWallLocked: true })
            {
                denyLog = "벽 매달림/미끄러짐 중 공격은 불가능 합니다.";
                return false;
            }

            if (_character.IsStatusDash() && _dash.IsDashing)
            {
                if (CanAttackPlayDashing)
                {
                    _dash.CancelDash(true);
                }
                else
                {
                    denyLog = "PlayerAction 셋팅에 canAttackPlayDashing 값이 false 입니다.";
                    return false;
                }
            }
            else if (_character.IsStatusJump() && _jump.IsJumping)
            {
                if (CanAttackPlayJump)
                {
                    _jump.CancelJump(true);
                }
                else
                {
                    denyLog = "PlayerAction 셋팅에 CanAttackPlayJump 값이 false 입니다.";
                    return false;
                }
            }
            else if (_character.IsStatusClimb() && _climb.IsClimbing)
            {
                denyLog = "등반 중 공격은 불가능 합니다.";
                return false;
            }
            else if (_character.IsStatusPush() && _pushPull.IsPushing)
            {
                denyLog = "밀기 중 공격은 불가능 합니다.";
                return false;
            }

            return true;
        }

        

        public bool TryPrepareGuard(out string denyLog)
        {
            denyLog = null;
            if (_character.IsStatusDead()) return false;

            // 벽 상태 중 가드 정책(현재는 금지)
            if (_wall is { IsWallLocked: true })
            {
                denyLog = "벽 매달림/미끄러짐 중 가드는 불가능 합니다.";
                return false;
            }

            // 대시 중 가드: 기본은 금지(필요 시 설정값 추가 고려)
            if (_character.IsStatusDash() && _dash.IsDashing)
            {
                denyLog = "대시 중 가드는 불가능 합니다.";
                return false;
            }

            // 점프 중 가드: 기본은 금지(필요 시 설정값 추가 고려)
            if (_character.IsStatusJump() && _jump.IsJumping)
            {
                denyLog = "점프 중 가드는 불가능 합니다.";
                return false;
            }

            if (_character.IsStatusClimb() && _climb.IsClimbing)
            {
                denyLog = "등반 중 가드는 불가능 합니다.";
                return false;
            }

            if (_character.IsStatusPush() && _pushPull.IsPushing)
            {
                denyLog = "밀기 중 가드는 불가능 합니다.";
                return false;
            }

            if (!TryCancelAttackForGuard(out denyLog))
            {
                return false;
            }

            // 스킬 사용 중 가드: 정책 미정이면 보수적으로 허용(원하면 차단 조건 추가)
            // var skill = _getSkillCancelable?.Invoke();
            // if (skill != null && skill.IsSkillRunning) { ... }

            return true;
        }

        /// <summary>
        /// 현재 공격 상태가 가드 입력으로 취소 가능한지 확인하고, 가능하면 공격 예약 작업을 정리합니다.
        /// </summary>
        /// <param name="denyLog">가드 입력을 거부할 때 출력할 로그입니다.</param>
        /// <returns>가드 입력을 계속 진행할 수 있으면 <see langword="true"/>입니다.</returns>
        private bool TryCancelAttackForGuard(out string denyLog)
        {
            denyLog = null;

            if (!_character.IsStatusAttack() && !_character.IsStatusAttackComboWait())
            {
                return true;
            }

            switch (AttackGuardCancelPolicy)
            {
                case AttackGuardCancelPolicy.None:
                    denyLog = "공격 중 가드는 불가능 합니다.";
                    return false;

                case AttackGuardCancelPolicy.AttackComboWaitOnly:
                    if (!_character.IsStatusAttackComboWait())
                    {
                        denyLog = "공격 애니메이션 중 가드는 불가능 합니다.";
                        return false;
                    }
                    break;

                case AttackGuardCancelPolicy.AttackAndComboWait:
                    break;

                default:
                    denyLog = "알 수 없는 공격 중 가드 캔슬 정책입니다.";
                    return false;
            }

            _attack?.CancelAttackByActionInterrupt();
            return true;
        }

        public bool TryPrepareJump(out string denyLog)
        {
            denyLog = null;
            if (_character.IsStatusDead()) return false;

            // 벽 매달림/미끄러짐 중에는 handler에서 라우팅

            if (_character.IsStatusDash() && _dash.IsDashing)
            {
                if (CanJumpPlayDashing)
                {
                    _dash.CancelDash(true);
                }
                else
                {
                    denyLog = "PlayerAction 셋팅에 canJumpPlayDashing 값이 false 입니다.";
                    return false;
                }
            }
            else if (_character.IsStatusClimb() && _climb.IsClimbing)
            {
                if (CanJumpPlayClimbing)
                {
                    _climb.CancelClimb();
                }
                else
                {
                    denyLog = "PlayerAction 셋팅에 canJumpPlayClimbing 값이 false 입니다.";
                    return false;
                }
            }
            else if (_character.IsStatusPush() && _pushPull.IsPushing)
            {
                denyLog = "밀기 중 점프는 불가능 합니다.";
                return false;
            }
            else if (_character.IsStatusCastingSkill() || _character.IsStatusUseSkill())
            {
                if (CanJumpUseSkill)
                {
                    var cancel = _getSkillCancelable?.Invoke();
                    cancel?.RequestCancelSkill(SkillCancelReason.UserInput);
                }
                else
                {
                    denyLog = "PlayerAction 셋팅에 canJumpUseSkill 값이 false 입니다.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 현재 캐릭터 상태에서 대시 입력을 시작할 수 있도록 상충 상태를 정리합니다.
        /// </summary>
        /// <param name="denyLog">대시 입력을 거부할 때 출력할 로그입니다.</param>
        /// <param name="allowSkillStateByExternalRule">
        /// 상위 계층의 전용 규칙이 승인한 대시일 때 스킬 사용 상태의 <c>CanDashUseSkill</c> 검사를 우회할지 여부입니다.
        /// </param>
        /// <returns>대시 준비가 완료되었으면 <see langword="true"/>입니다.</returns>
        public bool TryPrepareDash(out string denyLog, bool allowSkillStateByExternalRule = false)
        {
            denyLog = null;
            if (_character.IsStatusDead()) return false;

            if (_wall is { IsWallLocked: true })
            {
                denyLog = "벽 매달림/미끄러짐 중 대시는 불가능 합니다.";
                return false;
            }

            if (_character.IsStatusJump())
            {
                if (CanDashPlayJumping)
                {
                    if (_jump.IsJumping)
                        _jump.CancelJump(skipLandAnimation: true, restoreGravity: false);
                }
                else
                {
                    denyLog = "PlayerAction 셋팅에 canDashPlayJumping 값이 false 입니다.";
                    return false;
                }
            }
            else if (_character.IsStatusClimb() && _climb.IsClimbing)
            {
                if (CanDashPlayClimbing)
                {
                    var dir = _getMoveInput?.Invoke() ?? Vector2.zero;
                    _character.SetFacing(dir);
                    _climb.CancelClimb();
                }
                else
                {
                    denyLog = "PlayerAction 셋팅에 canDashPlayClimbing 값이 false 입니다.";
                    return false;
                }
            }
            else if (_character.IsStatusPush() && _pushPull.IsPushing)
            {
                denyLog = "밀기 중 대시는 불가능 합니다.";
                return false;
            }
            else if (_character.IsStatusCastingSkill() || _character.IsStatusUseSkill())
            {
                if (allowSkillStateByExternalRule)
                {
                    // 프로젝트 전용 콤보 대시처럼 상위 규칙이 이미 허용한 경우에는
                    // 일반 대시 설정인 canDashUseSkill보다 외부 규칙을 우선합니다.
                    return true;
                }

                if (CanDashUseSkill)
                {
                    var cancel = _getSkillCancelable?.Invoke();
                    cancel?.RequestCancelSkill(SkillCancelReason.UserInput);
                }
                else
                {
                    denyLog = "PlayerAction 셋팅에 canDashUseSkill 값이 false 입니다.";
                    return false;
                }
            }

            return true;
        }
    }
}
