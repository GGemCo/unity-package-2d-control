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
        
        public void RaiseChanged() => Changed?.Invoke(); // 툴/코드에서 강제 호출 가능
        
        [Header("이동")]
        [Tooltip("세로(위/아래) 이동 허용 여부")]
        public bool canMoveVertical;

        [Header("점프")]
        [Tooltip("최고 점프 높이 (월드 유닛)")]
        public float jumpHeight;
        [Tooltip("지면에서 최고점까지 도달하는 시간 (초)")]
        public float jumpSpeed;

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
    }
}