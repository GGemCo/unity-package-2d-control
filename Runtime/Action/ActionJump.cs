using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 점프 액션 (설정: 높이, 정점까지 시간)
    /// - 일부 애니메이션 미보유 시에도 동작하도록 폴백 포함
    /// - 1회성 단계는 Animation Event 미도착 시 워치독으로 자동 완료
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

        // Animator/클립 정보(폴백 판단 및 길이 계산용)
        private Dictionary<string, float> _clipLength = new();

        // --- 파라미터(2개) ---
        private float _desiredJumpHeight; // 월드 유닛
        private float _timeToApex;       // 지면→정점까지 시간(초)

        // --- 내부 계산치 ---
        private float _baseGravityScale;
        private float _jumpVelocityY;
        private float _prevGravityScale;

        // --- Phase ---
        private enum JumpPhase
        {
            None,
            StartOneShot,   // jump        (이벤트 → UpLoop)
            UpLoop,         // jump_up     (정점 감지 → ApexChange)
            ApexChange,     // change_fall (이벤트 → FallLoop)
            FallLoop,       // jump_fall   (착지 감지 → LandOneShot)
            LandOneShot     // jump_end    (이벤트 → Stop)
        }
        private JumpPhase _phase = JumpPhase.None;

        // 현재 1회성 단계에서 이벤트 대기 워치독
        private JumpPhase _awaitingEventFor = JumpPhase.None;
        private float _awaitingDeadline = 0f;
        private const float DEFAULT_ONESHOT_TIMEOUT = 0.2f; // 클립 길이를 못 구하면 사용

        // --- 애니메이션 이름 ---
        private const string AnimJumpStart      = "jump";
        private const string AnimJumpUpLoop     = "jump_up";
        private const string AnimJumpChangeFall = "jump_change_fall";
        private const string AnimJumpFallLoop   = "jump_fall";
        private const string AnimJumpEnd        = "jump_end";

        // --- Ground Layer ---
        private LayerMask _groundMask;

        // --- 보유 여부 캐시 ---
        private bool _hasStart, _hasUp, _hasChangeFall, _hasFall, _hasEnd;
        
        // Cliff-fall 감지용
        private bool _wasGrounded;
        private float _airborneTime;
        // 플랫폼/경사/계단 등에서 불필요한 낙하 전환 방지
        private const float CoyoteThreshold = 0.06f;  // 지면 상실 후 낙하로 인정까지의 지연
        private const float MinFallSpeedY = -0.10f; // 이 속도 이하일 때만 낙하로 간주

        // 점프 시에만 중력스케일 복구하도록 플래그 추가
        private bool _changedGravity; // Jump()에서 true, cliff-fall은 false

        
        public void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            _inputManager = inputManager;
            _characterBase = characterBase;
            _characterBaseController = characterBaseController;

            _rb = _characterBase.characterRigidbody2D;
            _col = _characterBase.colliderMapObject;

            if (_rb == null || _col == null)
            {
                GcLogger.LogError("[ActionJump] Rigidbody2D/Collider2D가 필요합니다.");
                return;
            }

            // Animator/클립 길이 수집
            _clipLength = _characterBase.CharacterAnimationController.GetAnimationAllLength();

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
            
            // 보유 여부 캐시
            _hasStart      = HasAnimation(AnimJumpStart);
            _hasUp         = HasAnimation(AnimJumpUpLoop);
            _hasChangeFall = HasAnimation(AnimJumpChangeFall);
            _hasFall       = HasAnimation(AnimJumpFallLoop);
            _hasEnd        = HasAnimation(AnimJumpEnd);
            
            _wasGrounded = IsGroundedByCollision();
            _airborneTime = 0f;
            _changedGravity = false;
        }

        public void OnDestroy() 
        {
            _characterBase.OnAnimationEventJump -= OnAnimationEventJump;
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
        /// <param name="ctx"></param>
        public void Jump(InputAction.CallbackContext ctx)
        {
            if (!ctx.started) return;
            if (_rb == null) return;

            if (_characterBase.IsStatusAttack()) return;
            if (_characterBase.IsStatusAttackComboWait()) return;
            if (_characterBase.IsStatusJump()) return;

            _characterBase.SetStatusJump();

            // 점프 입력: 중력 스케일 변경 및 복구 대상 표시
            _prevGravityScale = _rb.gravityScale;
            _rb.gravityScale  = _baseGravityScale;
            _changedGravity   = true;

            float vy = Mathf.Max(_rb.linearVelocity.y, _jumpVelocityY);
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, vy);

            EnterPhase(JumpPhase.StartOneShot); // jump(1회) 시작
        }

        /// <summary>
        /// 매 프레임 호출(Update)
        /// - 정점 통과 감지(UpLoop → ApexChange)
        /// - 하강 중 접지 감지(FallLoop → LandOneShot)
        /// - 1회성 단계 워치독(이벤트 누락 대비)
        /// </summary>
        public void Update()
        {
            if (_rb == null) return;

            bool grounded = IsGroundedByCollision();

            // --- [추가] 점프 FSM이 꺼져 있고(=입력 점프 아님), 지면을 잃었을 때 낙하 감지 ---
            if (_phase == JumpPhase.None)
            {
                if (!grounded)
                {
                    _airborneTime += Time.deltaTime;

                    // 충분히 공중 상태가 지속되고, 실제로 하강 중일 때만 낙하 인정
                    if (_airborneTime >= CoyoteThreshold && _rb.linearVelocity.y <= MinFallSpeedY)
                    {
                        // 전투 등 방해 상태는 존중
                        if (!_characterBase.IsStatusAttack() && !_characterBase.IsStatusAttackComboWait())
                        {
                            // 공중 상태로 전환(프로젝트 표준에 맞춰 Jump 상태 사용)
                            if (!_characterBase.IsStatusJump()) _characterBase.SetStatusJump();

                            // Cliff-fall은 중력 스케일을 변경하지 않음 (복구 불필요)
                            _changedGravity = false;

                            // 곧바로 Fall 루프 진입 → jump_fall이 있으면 재생
                            EnterPhase(JumpPhase.FallLoop);
                        }
                    }
                }
                else
                {
                    _airborneTime = 0f; // 지면 회복 시 초기화
                }
            }

            // --- 기존 점프 FSM 로직 ---
            if (_phase == JumpPhase.None) { _wasGrounded = grounded; return; }

            float vy = _rb.linearVelocity.y;

            switch (_phase)
            {
                case JumpPhase.UpLoop:
                    if (vy <= 0.0001f) EnterPhase(JumpPhase.ApexChange);
                    break;

                case JumpPhase.FallLoop:
                    if (vy <= 0f && grounded) EnterPhase(JumpPhase.LandOneShot);
                    break;
            }

            _wasGrounded = grounded;

            // 2) 이벤트 워치독 (이벤트 미도착 시 자동 완료)
            if (_awaitingEventFor != JumpPhase.None && Time.time >= _awaitingDeadline)
            {
                switch (_awaitingEventFor)
                {
                    case JumpPhase.StartOneShot:
                        HandleJumpStartOneShotEnd();
                        break;
                    case JumpPhase.ApexChange:
                        HandleJumpChangeFallOneShotEnd();
                        break;
                    case JumpPhase.LandOneShot:
                        HandleJumpLandOneShotEnd();
                        break;
                }
            }
        }

        // -------------------------- Animation Event Entry Points --------------------------

        private void HandleJumpStartOneShotEnd()
        {
            if (_phase != JumpPhase.StartOneShot) return;
            ClearAwaiting();
            // 상승 루프 진입 (없으면 전환만)
            if (_hasUp) PlayAnimSafe(AnimJumpUpLoop);
            _phase = JumpPhase.UpLoop;
        }

        private void HandleJumpChangeFallOneShotEnd()
        {
            if (_phase != JumpPhase.ApexChange) return;
            ClearAwaiting();
            // 하강 루프 진입 (없으면 전환만)
            if (_hasFall) PlayAnimSafe(AnimJumpFallLoop);
            _phase = JumpPhase.FallLoop;
        }

        private void HandleJumpLandOneShotEnd()
        {
            if (_phase != JumpPhase.LandOneShot) return;
            ClearAwaiting();
            FinishAndStop();
        }

        // -------------------------- Internal Helpers --------------------------

        private void EnterPhase(JumpPhase next)
        {
            _phase = next;
            ClearAwaiting();

            switch (next)
            {
                case JumpPhase.StartOneShot:
                    if (_hasStart)
                    {
                        PlayAnimSafe(AnimJumpStart);
                        StartAwaiting(next, AnimJumpStart);
                    }
                    else
                    {
                        // jump가 없으면 즉시 UpLoop로
                        HandleJumpStartOneShotEnd();
                    }
                    break;

                case JumpPhase.UpLoop:
                    if (_hasUp) PlayAnimSafe(AnimJumpUpLoop);
                    // 루프는 이벤트 대기 없음
                    break;

                case JumpPhase.ApexChange:
                    if (_hasChangeFall)
                    {
                        PlayAnimSafe(AnimJumpChangeFall);
                        StartAwaiting(next, AnimJumpChangeFall);
                    }
                    else
                    {
                        // change_fall이 없으면 즉시 FallLoop로
                        HandleJumpChangeFallOneShotEnd();
                    }
                    break;

                case JumpPhase.FallLoop:
                    if (_hasFall) PlayAnimSafe(AnimJumpFallLoop);
                    break;

                case JumpPhase.LandOneShot:
                    if (_hasEnd)
                    {
                        PlayAnimSafe(AnimJumpEnd);
                        StartAwaiting(next, AnimJumpEnd);
                    }
                    else
                    {
                        // end가 없으면 즉시 종료
                        HandleJumpLandOneShotEnd();
                    }
                    break;
            }
        }

        private void StartAwaiting(JumpPhase phase, string clipName)
        {
            _awaitingEventFor = phase;
            _awaitingDeadline = Time.time + GetClipDurationWithFallback(clipName);
        }

        private void ClearAwaiting()
        {
            _awaitingEventFor = JumpPhase.None;
            _awaitingDeadline = 0f;
        }

        private float GetClipDurationWithFallback(string clipName)
        {
            // 클립 길이가 있으면 약간의 마진(+0.02s) 포함
            if (_clipLength.TryGetValue(clipName, out var len) && len > 0f)
                return len + 0.02f;
            return DEFAULT_ONESHOT_TIMEOUT;
        }

        private void FinishAndStop()
        {
            _phase = JumpPhase.None;

            // 점프 입력으로만 중력을 바꿨을 때 복구
            if (_changedGravity && _rb != null)
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

        private bool HasAnimation(string stateName)
        {
            // 1) 캐릭터 애니메이션 컨트롤러가 "존재 여부"를 제공한다면 우선 사용
            if (_characterBase.CharacterAnimationController is { } ctrl)
            {
                // 선택: ctrl에 HasAnimation(string) API가 있다면 사용하도록 교체 가능
                return ctrl.HasAnimation(stateName);
            }

            return false;

            // // 2) Animator의 클립 이름으로 보수적 판단
            // return _clipLength.ContainsKey(stateName);
        }
        /// <summary>
        /// 애니메이션 event 처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
