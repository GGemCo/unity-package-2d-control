using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 가드 입력 준비 단계에서 확정한 후속 처리 정보를 전달합니다.
    /// </summary>
    internal readonly struct GuardPreparationContext
    {
        /// <summary>
        /// 별도의 후속 처리가 필요하지 않은 기본 컨텍스트입니다.
        /// </summary>
        public static GuardPreparationContext None => default;

        /// <summary>
        /// 가드 시작이 확정된 시점에 활성 HitStop을 종료해야 하는지 여부입니다.
        /// </summary>
        public bool InterruptHitStopOnStart { get; }

        /// <summary>
        /// 가드 시작이 확정된 시점에 기본 공격의 전진 이동을 취소해야 하는지 여부입니다.
        /// </summary>
        public bool CancelAttackMoveForceOnStart { get; }

        /// <summary>
        /// 가드 입력 준비 결과를 생성합니다.
        /// </summary>
        /// <param name="interruptHitStopOnStart">가드 시작 시 활성 HitStop을 종료할지 여부입니다.</param>
        /// <param name="cancelAttackMoveForceOnStart">가드 시작 시 기본 공격의 전진 이동을 취소할지 여부입니다.</param>
        public GuardPreparationContext(
            bool interruptHitStopOnStart,
            bool cancelAttackMoveForceOnStart)
        {
            InterruptHitStopOnStart = interruptHitStopOnStart;
            CancelAttackMoveForceOnStart = cancelAttackMoveForceOnStart;
        }
    }

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
        public GuardDuringHitStopPolicy GuardDuringHitStopPolicy { get; set; } = GuardDuringHitStopPolicy.Block;

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

        

        /// <summary>
        /// 현재 캐릭터 상태에서 가드 입력을 시작할 수 있는지 확인하고 필요한 선행 상태를 정리합니다.
        /// </summary>
        /// <param name="denyLog">가드 입력이 거부된 경우 출력할 로그입니다.</param>
        /// <param name="preparationContext">가드 시작이 확정된 뒤 적용할 후속 처리 정보입니다.</param>
        /// <returns>가드 입력을 계속 처리할 수 있으면 <see langword="true"/>를 반환합니다.</returns>
        public bool TryPrepareGuard(out string denyLog, out GuardPreparationContext preparationContext)
        {
            denyLog = null;
            preparationContext = GuardPreparationContext.None;
            if (_character.IsStatusDead()) return false;

            bool interruptHitStopOnStart = false;
            if (_character.IsDontControl())
            {
                if (!CanStartGuardByInterruptingHitStop())
                {
                    return false;
                }

                // 이 시점에는 HitStop만 조작을 차단하고 있으므로, 스테미나 지불 성공 후 안전하게 종료하도록 예약합니다.
                interruptHitStopOnStart = true;
            }

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

            if (!TryCancelAttackForGuard(out denyLog, out bool cancelAttackMoveForceOnStart))
            {
                return false;
            }

            // 스킬 사용 중 가드: 정책 미정이면 보수적으로 허용(원하면 차단 조건 추가)
            // var skill = _getSkillCancelable?.Invoke();
            // if (skill != null && skill.IsSkillRunning) { ... }

            preparationContext = new GuardPreparationContext(
                interruptHitStopOnStart,
                cancelAttackMoveForceOnStart);
            return true;
        }

        /// <summary>
        /// 현재 조작 불가 상태가 가드 정책으로 중단할 수 있는 HitStop 단독 상태인지 확인합니다.
        /// </summary>
        /// <remarks>
        /// Crowd Control 또는 외부 전체 조작 잠금이 함께 활성화된 경우에는 HitStop 예외 정책으로 우회하지 않습니다.
        /// 실제 지면 충돌을 확인하여 HitStop 도중 공중 가드가 시작되는 것도 방지합니다.
        /// </remarks>
        /// <returns>HitStop을 종료하고 가드를 시도할 수 있으면 <see langword="true"/>를 반환합니다.</returns>
        public bool CanStartGuardByInterruptingHitStop()
        {
            if (GuardDuringHitStopPolicy != GuardDuringHitStopPolicy.InterruptHitStopAndStartGuard)
                return false;
            if (!_character.IsHitStopped)
                return false;
            if (_character.HasActiveOrQueuedCrowdControl())
                return false;
            if (_character.IsControlLocked())
                return false;

            return _jump != null && _jump.IsGroundedByCollision();
        }

        /// <summary>
        /// 현재 공격 상태가 가드 입력으로 취소 가능한지 확인하고, 가능하면 공격 예약 작업을 정리합니다.
        /// </summary>
        /// <param name="denyLog">가드 입력을 거부할 때 출력할 로그입니다.</param>
        /// <param name="cancelAttackMoveForceOnStart">
        /// 가드 시작이 확정된 뒤 기본 공격의 전진 이동을 취소해야 하면 <see langword="true"/>입니다.
        /// </param>
        /// <returns>가드 입력을 계속 진행할 수 있으면 <see langword="true"/>입니다.</returns>
        private bool TryCancelAttackForGuard(
            out string denyLog,
            out bool cancelAttackMoveForceOnStart)
        {
            denyLog = null;
            cancelAttackMoveForceOnStart = false;

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
            // 공격 예약은 준비 단계에서 정리하되, 이동 힘은 스테미나 지불이 성공한 뒤에만 무효화합니다.
            cancelAttackMoveForceOnStart = true;
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
