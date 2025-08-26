using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 점프 액션 (설정: 높이, 정점까지 시간)
    /// 애니메이션 시퀀스:
    ///   jump(1회, 이벤트) → jump_up(루프)
    ///   정점 통과 시: jump_change_fall(1회, 이벤트) → jump_fall(루프)
    ///   착지 시: jump_end(1회, 이벤트) → Stop()
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

        // --- 파라미터(2개) ---
        private float _desiredJumpHeight = 3.0f; // 월드 유닛
        private float _timeToApex = 0.35f;       // 지면→정점까지 시간(초)

        // --- 내부 계산치 ---
        private float _baseGravityScale;
        private float _jumpVelocityY;
        private float _prevGravityScale;

        // --- Phase ---
        private enum JumpPhase
        {
            None,
            StartOneShot,   // jump (이벤트로 UpLoop 전환)
            UpLoop,         // jump_up
            ApexChange,     // jump_change_fall (이벤트로 FallLoop 전환)
            FallLoop,       // jump_fall
            LandOneShot     // jump_end (이벤트로 종료)
        }

        private JumpPhase _phase = JumpPhase.None;

        // --- 애니메이션 이름 ---
        private const string AnimJumpStart      = "jump";
        private const string AnimJumpUpLoop     = "jump_up";
        private const string AnimJumpChangeFall = "jump_change_fall";
        private const string AnimJumpFallLoop   = "jump_fall";
        private const string AnimJumpEnd        = "jump_end";

        // --- Ground Layer ---
        private LayerMask _groundMask;

        public void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            _inputManager = inputManager;
            _characterBase = characterBase;
            _characterBaseController = characterBaseController;

            _rb  = _characterBase.GetComponent<Rigidbody2D>();
            _col = _characterBase.GetComponent<Collider2D>();

            if (_rb == null || _col == null)
            {
                GcLogger.LogError("[ActionJump] Rigidbody2D/Collider2D가 필요합니다.");
                return;
            }

            // Ground Layer
            _groundMask = LayerMask.GetMask(ConfigLayer.GetValue(ConfigLayer.Keys.TileMapGround));
            if (_groundMask == 0)
            {
                GcLogger.LogWarning($"[ActionJump] Layer '{ConfigLayer.GetValue(ConfigLayer.Keys.TileMapGround)}'를 찾을 수 없습니다. Project Settings > Tags and Layers 확인.");
            }

            // 파라미터 로드
            _desiredJumpHeight = AddressableLoaderSettings.Instance.playerSettings.jumpHeight;
            _timeToApex        = AddressableLoaderSettings.Instance.playerSettings.jumpSpeed;

            RecalculatePhysicsConstants(_desiredJumpHeight, _timeToApex);
            
            _characterBase.OnAnimationEventJump += OnAnimationEventJump;
        }

        public void OnDestroy()
        {
            // 외부 이벤트 구독 없음
        }

        public void Configure(float desiredJumpHeight, float timeToApex)
        {
            RecalculatePhysicsConstants(desiredJumpHeight, timeToApex);
        }

        private void RecalculatePhysicsConstants(float desiredJumpHeight, float timeToApex)
        {
            _desiredJumpHeight = Mathf.Max(0.01f, desiredJumpHeight);
            _timeToApex        = Mathf.Max(0.05f,  timeToApex);

            // g = 2h / t^2, v0 = g * t
            float gDesired = (2f * _desiredJumpHeight) / (_timeToApex * _timeToApex);
            float worldG   = Mathf.Abs(Physics2D.gravity.y);
            _baseGravityScale = gDesired / worldG;
            _jumpVelocityY    = gDesired * _timeToApex;
        }

        /// <summary>
        /// InputManager에서 Jump.started로 호출
        /// </summary>
        public void Jump(InputAction.CallbackContext ctx)
        {
            if (!ctx.started) return;
            if (_rb == null) return;

            // 상태 방어
            if (_characterBase.IsStatusAttack()) return;
            if (_characterBase.IsStatusAttackComboWait()) return;
            if (_characterBase.IsStatusJump()) return;

            _characterBase.SetStatusJump();

            // 물리 적용
            _prevGravityScale = _rb.gravityScale;
            _rb.gravityScale  = _baseGravityScale;

            // 하강 중에도 충분히 끌어올림
            float vy = Mathf.Max(_rb.linearVelocity.y, _jumpVelocityY);
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, vy);

            // jump(1회) 시작 —> 끝 이벤트로 UpLoop 전환
            EnterPhase(JumpPhase.StartOneShot);
        }

        /// <summary>
        /// 매 프레임 호출(Update)
        /// - 정점 통과 감지(UpLoop → ApexChange)
        /// - 하강 중 접지 감지(FallLoop → LandOneShot)
        /// </summary>
        public void Update()
        {
            if (_rb == null) return;
            if (_phase == JumpPhase.None) return;

            float vy = _rb.linearVelocity.y;

            switch (_phase)
            {
                case JumpPhase.UpLoop:
                    // 상승 → 정점 통과
                    if (vy <= 0.0001f)
                    {
                        EnterPhase(JumpPhase.ApexChange);
                    }
                    break;

                case JumpPhase.FallLoop:
                    // 하강 중 접지
                    if (vy <= 0f && IsGroundedByCollision())
                    {
                        EnterPhase(JumpPhase.LandOneShot);
                    }
                    break;
            }
        }

        // -------------------------- Animation Event Entry Points --------------------------

        /// <summary>jump(1회) 종료 이벤트</summary>
        public void HandleJumpStartOneShotEnd()
        {
            if (_phase != JumpPhase.StartOneShot) return;
            // 상승 루프 진입
            PlayAnimSafe(AnimJumpUpLoop);
            _phase = JumpPhase.UpLoop;
        }

        /// <summary>jump_change_fall(1회) 종료 이벤트</summary>
        public void HandleJumpChangeFallOneShotEnd()
        {
            if (_phase != JumpPhase.ApexChange) return;
            // 하강 루프 진입
            PlayAnimSafe(AnimJumpFallLoop);
            _phase = JumpPhase.FallLoop;
        }

        /// <summary>jump_end(1회) 종료 이벤트</summary>
        public void HandleJumpLandOneShotEnd()
        {
            if (_phase != JumpPhase.LandOneShot) return;
            FinishAndStop();
        }

        // -------------------------- Internal Helpers --------------------------

        private void EnterPhase(JumpPhase next)
        {
            _phase = next;

            switch (next)
            {
                case JumpPhase.StartOneShot:
                    PlayAnimSafe(AnimJumpStart);
                    break;

                case JumpPhase.ApexChange:
                    PlayAnimSafe(AnimJumpChangeFall);
                    break;

                case JumpPhase.LandOneShot:
                    PlayAnimSafe(AnimJumpEnd);
                    break;
            }
        }

        private void FinishAndStop()
        {
            _phase = JumpPhase.None;

            if (_rb != null)
                _rb.gravityScale = _prevGravityScale;

            _characterBase.Stop();
        }

        private void PlayAnimSafe(string stateName)
        {
            _characterBase.CharacterAnimationController?.PlayCharacterAnimation(stateName);
        }

        private bool IsGroundedByCollision()
        {
            if (_col == null) return false;
            return _col.IsTouchingLayers(_groundMask);
        }

        private void OnAnimationEventJump(CharacterBase sender, EventArgsOnAnimationEventJump e)
        {
            switch (e.EventName)
            {
                case AnimationConstants.EventNameJumpUp:
                    HandleJumpStartOneShotEnd();
                    break;
                case AnimationConstants.EventNameJumpFall:
                    HandleJumpChangeFallOneShotEnd();
                    break;
                case AnimationConstants.EventNameJumpEnd:
                    HandleJumpLandOneShotEnd();
                    break;
            }
        }
    }
}
