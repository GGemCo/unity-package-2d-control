using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 점프 액션 (설정: 높이, 정점까지 시간)
    /// - jump_start: 점프 시작 ~ 정점 도달까지 재생
    /// - jump_wait : 정점에서 잠시 재생 (짧은 대기)
    /// - jump_end  : 정점 이후 하강 시 재생
    /// </summary>
    public class ActionJump
    {
        // --- 외부 참조 ---
        private InputManager _inputManager;
        private CharacterBase _characterBase;
        private CharacterBaseController _characterBaseController;

        // --- 캐시 ---
        private Rigidbody2D _rb;
        private Collider2D _col;
        // private Animator _anim;

        // --- 디자이너 파라미터 (요구사항: 2개만 노출) ---
        // 목표 점프 높이(월드 유닛)
        private float _desiredJumpHeight = 3.0f;
        // 지면→정점까지 걸리는 시간(초): 체감 상의 점프 "속도"
        private float _timeToApex = 0.35f;

        // --- 내부 계산치 ---
        private float _baseGravityScale;      // 점프 중 적용할 중력 스케일
        private float _jumpVelocityY;         // 초기 상승 속도
        private float _prevGravityScale = 0; // 복구용
        private bool _isJumping;              // 점프 중 여부
        private bool _apexPassed;             // 정점 통과 여부

        // --- 애니메이션 이름 (상수) ---
        private const string AnimJumpStart = "jump";
        private const string AnimJumpWait  = "jump_wait";
        private const string AnimJumpEnd   = "jump_end";

        // --- Ground: 충돌 기반 체크 (TilemapCollider2D on Layer: GGemCo_TileMapGround) ---
        private LayerMask _groundMask;

        public void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            _inputManager = inputManager;
            _characterBase = characterBase;
            _characterBaseController = characterBaseController;

            _rb  = _characterBase.GetComponent<Rigidbody2D>();
            _col = _characterBase.GetComponent<Collider2D>();
            // _anim = _characterBase.GetComponentInChildren<Animator>();

            if (_rb == null || _col == null)
            {
                GcLogger.LogError("[ActionJump] Rigidbody2D/Collider2D가 필요합니다.");
                return;
            }
            // 바닥 레이어 마스크: GGemCo_TileMapGround (TilemapCollider2D가 이 레이어여야 함)
            _groundMask = LayerMask.GetMask(ConfigLayer.GetValue(ConfigLayer.Keys.TileMapGround));
            if (_groundMask == 0)
            {
                GcLogger.LogWarning($"[ActionJump] Layer '{ConfigLayer.GetValue(ConfigLayer.Keys.TileMapGround)}'를 찾을 수 없습니다. Project Settings > Tags and Layers를 확인하세요.");
            }

            _desiredJumpHeight = AddressableLoaderSettings.Instance.playerSettings.jumpHeight;
            _timeToApex = AddressableLoaderSettings.Instance.playerSettings.jumpSpeed;

            // 기본값으로 물리 상수 계산
            RecalculatePhysicsConstants(_desiredJumpHeight, _timeToApex);
        }

        public void OnDestroy()
        {
            // 현재 구조상, 외부 이벤트 구독 없음
        }

        /// <summary>
        /// 점프 파라미터 설정(필요 시 런타임에 호출 가능)
        /// </summary>
        public void Configure(float desiredJumpHeight, float timeToApex)
        {
            RecalculatePhysicsConstants(desiredJumpHeight, timeToApex);
        }

        private void RecalculatePhysicsConstants(float desiredJumpHeight, float timeToApex)
        {
            _desiredJumpHeight = Mathf.Max(0.01f, desiredJumpHeight);
            _timeToApex = Mathf.Max(0.05f, timeToApex);

            // g = 2h / t^2, v0 = g * t
            float gDesired = (2f * _desiredJumpHeight) / (_timeToApex * _timeToApex);
            float worldG = Mathf.Abs(Physics2D.gravity.y);
            _baseGravityScale = gDesired / worldG;
            _jumpVelocityY = gDesired * _timeToApex;
        }

        /// <summary>
        /// InputManager에서 Jump.started로 호출
        /// </summary>
        public void Jump(InputAction.CallbackContext ctx)
        {
            if (!ctx.started) return;
            if (_rb == null) return;

            // 공격/사망/점프 중이면 무시 (현재 프로젝트 패턴과 동일한 방어로직)
            if (_characterBase.IsStatusAttack()) return;
            if (_characterBase.IsStatusAttackComboWait()) return;
            // if (_characterBase.IsStatusDead?.Invoke() ?? false) return; // IsStatusDead가 메서드/프로퍼티 중 어떤 형태든 방어

            if (_characterBase.IsStatusJump()) return; // 이미 점프 중
            _characterBase.SetStatusJump();    // 점프 상태 진입 (프로젝트 내 구현에 따라 메서드 존재)

            // 물리 적용
            _prevGravityScale = _rb.gravityScale;
            _rb.gravityScale = _baseGravityScale;

            // 기존 수직속도와 비교해 더 큰 상승력을 부여(하강 중에도 확실히 들어 올림)
            float vy = Mathf.Max(_rb.linearVelocity.y, _jumpVelocityY);
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, vy);

            // 애니메이션: 시작 ~ 정점까지
            PlayAnimSafe(AnimJumpStart);

            _isJumping = true;
            _apexPassed = false;
        }

        /// <summary>
        /// InputManager.Update에서 매 프레임 호출
        /// </summary>
        public void Update()
        {
            if (!_isJumping || _rb == null) return;

            float vy = _rb.linearVelocity.y;

            // 정점 통과 시점 감지: 양(상승)→0→음(하강)으로 넘어가는 순간
            if (!_apexPassed && vy <= 0.0001f)
            {
                // 정점에서 짧게 'wait'를 재생 (Animator 전이 조건에 따라 즉시 fall로 넘어가도 무방)
                PlayAnimSafe(AnimJumpWait);
                _apexPassed = true;
            }
            // 정점 이후 하강으로 전이되면 fall 애니메이션
            if (_apexPassed && vy < -0.0001f)
            {
                PlayAnimSafe(AnimJumpEnd);
            }

            // 착지 판정
            if (vy <= 0f && IsGroundedByCollision())
            {
                FinishJump();
            }
        }

        private void FinishJump()
        {
            _isJumping = false;
            _apexPassed = false;

            if (_rb != null) _rb.gravityScale = _prevGravityScale;

            // 이동/대기 상태로 복귀 처리(프로젝트 표준: Stop()은 방향/속도/상태 초기화)
            _characterBase.Stop();
        }

        private void PlayAnimSafe(string stateName)
        {
            _characterBase.CharacterAnimationController?.PlayCharacterAnimation(stateName);
        }
        /// <summary>
        /// 바닥 체크(충돌 기반):
        /// - 캐릭터 Collider2D가 Layer 'GGemCo_TileMapGround'와 물리적으로 접촉 중인지 여부
        /// - 캐릭터/타일맵 콜라이더 모두 Non-Trigger여야 하며, Physics 2D Collision Matrix에서 상호 충돌이 허용되어야 함
        /// </summary>
        private bool IsGroundedByCollision()
        {
            if (_col == null) return false;
            return _col.IsTouchingLayers(_groundMask);
        }
    }
}
