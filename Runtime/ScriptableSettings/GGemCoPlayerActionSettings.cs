using System;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 플레이어 action 설정
    /// </summary>
    [CreateAssetMenu(fileName = ConfigScriptableObjectControl.PlayerAction.FileName, menuName = ConfigScriptableObjectControl.PlayerAction.MenuName, order = ConfigScriptableObjectControl.PlayerAction.Ordering)]
    public class GGemCoPlayerActionSettings : ScriptableObject, ISettingsChangeNotifier
    {
        public enum HangAnimationAssetFacing
        {
            Left = -1,
            Right = 1,
        }
        // 에디터/플레이모드에서만 쓰일 런타임 이벤트 (직렬화 방지)
        public event Action Changed;

#if UNITY_EDITOR
        // 인스펙터 값 변경 시 호출(에디터 전용)
        private void OnValidate()
        {
            // 값 클램핑/정규화도 여기서 처리하면 편함
            // if (jumpHeight < 0f) jumpHeight = 0f;
            // if (dashDuration < 0.01f) dashDuration = 0.01f;

            Changed?.Invoke();
        }
#endif

        public void RaiseChanged()
        {
            
        }
        
        [Header("이동")]
        [Tooltip("세로(위/아래) 이동 허용 여부")]
        public bool canMoveVertical;

        [Header("입력")]
        [Tooltip("버튼 Press 후 릴리즈로 확정하기까지의 최대 대기시간(ms)\n- 모든 액션은 릴리즈(실제/가상) 시점에만 실행됩니다.\n- 대기 시간 동안 다른 버튼 Press가 들어오면 동시 입력(Chord)으로 판단합니다.")]
        [Range(1f, 500f)]
        public float pressToReleaseMaxWaitMs = 80f;

        [Header("점프")]
        [Tooltip("점프 애니메이션 prefix (예: jump)")]
        public string prefixJumpAnimation = "jump";
        [Tooltip("최고 점프 높이 (월드 유닛)")]
        public float jumpHeight;
        [Tooltip("지면에서 최고점까지 도달하는 시간 (초)")]
        public float jumpSpeed;
        [Tooltip("스킬 사용 중 점프 가능 여부")]
        public bool canJumpUseSkill;
        [Tooltip("점프 중 공격 가능 여부")]
        public bool canAttackPlayJump;

        [Header("대시")]
        [Tooltip("대시 거리 (월드 유닛)")]
        public float dashDistance;
        [Tooltip("대시 지속 시간 (초)")]
        public float dashDuration;
        [Tooltip("대시 이동 보간 방식 (Easing)")]
        public Easing.EaseType dashEasing;
        [Tooltip("대시 중 공격 가능 여부")]
        public bool canAttackPlayDashing;
        // [Tooltip("대시 중 이동 가능 여부")]
        // public bool canMovePlayDashing;
        [Tooltip("대시 중 점프 가능 여부")]
        public bool canJumpPlayDashing;
        [Tooltip("점프 중 대시 가능 여부")]
        public bool canDashPlayJumping;
        [Tooltip("스킬 사용 중 대시 가능 여부")]
        public bool canDashUseSkill;

        [Header("등반")]
        [Tooltip("기본 등반 속도 비율 (플레이어 이동 속도 기준)\n예: 1=100%, 0.5=50%\n※ 오브젝트 값이 0보다 크면 그 값을 우선 사용")]
        public float climbSpeed;
        [Tooltip("점프 중 등반 시작 가능 여부")]
        public bool canClimbingPlayJumping;
        [Tooltip("등반 중 점프 가능 여부")]
        public bool canJumpPlayClimbing;
        [Tooltip("등반 중 대시 가능 여부")]
        public bool canDashPlayClimbing;
        [Tooltip("등반 중 좌/우 이동 가능 여부")]
        public bool canMoveSidePlayClimbing;

        [Header("밀기 / 당기기")]
        [Tooltip("기본 밀기 속도 비율 (플레이어 이동 속도 기준)\n예: 1=100%, 0.5=50%\n※ 오브젝트 값이 0보다 크면 그 값을 우선 사용")]
        public float pushMoveSpeed;
        [Tooltip("기본 당기기 속도 비율 (플레이어 이동 속도 기준)\n예: 1=100%, 0.5=50%\n※ 오브젝트 값이 0보다 크면 그 값을 우선 사용")]
        public float pullMoveSpeed;

        // ============================================================
        // Wall Action
        // ============================================================

        [Header("벽 액션 - 공통")]
        [Tooltip("벽 매달림/미끄러짐/벽 점프 기능 활성화")]
        public bool enableWallAction = true;

        [Tooltip("벽 액션 디버그 Gizmo(레이 캐스트 등) 표시 여부")]
        public bool enableWallDebugGizmos;

        [Tooltip("벽으로 취급할 레이어 마스크 (미설정 시 Ground 레이어를 사용)")]
        public LayerMask wallMask;

        [Tooltip("벽 감지 거리 (월드 유닛). 캐릭터 콜라이더 가장자리에서 측면으로 이 거리만큼 검사합니다.")]
        public float wallCheckDistance = 0.08f;

        [Header("벽 액션 - Hang")]
        [Tooltip("벽 매달림 애니메이션 리소스의 기본 방향(Flip 없음 기준).\n" +
                 "예) Right(오른쪽) 리소스라면, 벽이 플레이어 오른쪽에 있을 때 Flip이 필요합니다.")]
        public HangAnimationAssetFacing wallHangAssetFacing = HangAnimationAssetFacing.Right;

        [Tooltip("벽 매달림 → 미끄러짐으로 전환되는 대기 시간(초). 입력이 없을 때만 카운트됩니다.")]
        public float wallHangToSlideDelay = 0.35f;

        [Tooltip("Kinematic 벽 매달림 시 벽 안쪽으로 밀어넣는 X 오프셋(월드 유닛). 0.02~0.08 권장")]
        public float wallHangInsetX = 0.04f;

        [Tooltip("벽 매달림 애니메이션 prefix (예: wall_hang)")]
        public string prefixWallHangAnimation = "wall_hang";

        [Header("벽 액션 - Slide")]
        [Tooltip("Kinematic 벽 미끄러짐 하강 속도(월드 유닛/초). wallSlideSpeed를 사용하지 않고 이 값을 사용합니다.")]
        public float wallSlideDownSpeed = 2.5f;

        [Tooltip("벽 미끄러짐 애니메이션 prefix (예: wall_slide)")]
        public string prefixWallSlideAnimation = "wall_slide";
        [Tooltip("Slide 도중 벽 접촉이 끊길 때, 현재 바라보는 방향으로 적용할 X 속도(월드 유닛/초).\n" +
                 "0이면 수직 점프만 적용됩니다.")]
        public float wallSlideEndExitJumpX = 2.25f;

        [Tooltip("Slide 도중 벽 접촉이 끊길 때, 적용할 Y 속도(월드 유닛/초).\n" +
                 "값이 너무 크면 체공이 길어질 수 있으니 2~5 범위를 권장합니다.")]
        public float wallSlideEndExitJumpY = 3.5f;

        [Header("벽 액션 - Jump")]
        [Tooltip("Kinematic 벽 점프 발사 각도(도). 0=수평, 90=수직. 반대편 방향(dirX)에 대해 적용됩니다.")]
        [Range(0f, 89f)]
        public float wallJumpAngleDeg = 55f;

        [Tooltip("반대편 벽이 '예측 각도' 방향에 존재할 때 사용하는 벽 점프 초기 속도(월드 유닛/초).")]
        public float wallJumpSpeedWithOppositeWall = 10.0f;

        [Tooltip("반대편 벽 존재 여부를 예측하기 위한 레이캐스트 거리(월드 유닛).")]
        public float wallJumpPredictDistance = 3.5f;

        [Tooltip("Kinematic 벽 점프 최대 지속 시간(초). 이 시간이 지나면 Dynamic으로 복귀하여 기존 점프 시스템에 맡깁니다. (안전장치)")]
        public float wallJumpMaxDuration = 0.75f;

        [Tooltip("벽 점프 직후 재부착 방지 시간(초)")]
        public float wallReattachCooldown = 0.15f;

        [Tooltip("벽 점프 애니메이션 prefix (예: wall_jump)")]
        public string prefixWallJumpAnimation = "wall_jump";

        [Header("벽 액션 - JumpEnd")]
        [Tooltip("반대편 벽이 없을 때, Kinematic 상태로 얼마나 이동할 것인지")]
        public float wallJumpEndDistance = 8.0f;
        [Tooltip("반대편 벽이 없을 때, 현재 바라보는 방향으로 적용할 X 속도(월드 유닛/초).\n" +
                 "0이면 수직 점프만 적용됩니다.")]
        public float wallJumpEndExitJumpX = 2.25f;

        [Tooltip("반대편 벽이 없을 때, 적용할 Y 속도(월드 유닛/초).\n" +
                 "값이 너무 크면 체공이 길어질 수 있으니 2~5 범위를 권장합니다.")]
        public float wallJumpEndExitJumpY = 3.5f;

        [Header("방어")]
        [Tooltip("방어 애니메이션 prefix (예: guard)")]
        public string prefixGuardAnimation;

        // ============================================================

        private void Reset()
        {
            canMoveVertical = true;
        }
    }
}
