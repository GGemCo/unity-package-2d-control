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

        public PlayerInputPolicy(
            CharacterBase character,
            ActionDash dash,
            ActionJump jump,
            ActionClimb climb,
            ActionPushPull pushPull,
            ActionWall wall,
            System.Func<ISkillCancelableDriver> getSkillCancelable,
            System.Func<Vector2> getMoveInput)
        {
            _character = character;
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

        public bool TryPrepareDash(out string denyLog)
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
