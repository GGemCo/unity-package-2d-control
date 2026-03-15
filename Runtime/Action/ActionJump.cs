using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DControl
{
    /// <summary>
    /// 점프 액션 (설정: 높이, 정점까지 시간)
    /// - 일부 애니메이션 미보유 시에도 동작하도록 폴백 포함
    /// - 1회성 단계는 Animation Event 미도착 시 워치독으로 자동 완료
    /// </summary>
    public class ActionJump : ActionBase
    {
        // 클래스 상단 필드/프로퍼티 섹션 인근
        public bool IsJumping => _phase != JumpPhase.None;

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
        private float _awaitingDeadline;
        private const float DefaultOneshotTimeout = 0.2f; // 클립 길이를 못 구하면 사용

        // --- 애니메이션 이름 (Settings 기반) ---
        private string _animJumpStart;
        private string _animJumpUpLoop;
        private string _animJumpChangeFall;
        private string _animJumpFallLoop;
        private string _animJumpEnd;

        // --- Ground / Ceiling Layer ---
        private LayerMask _groundMask;
        private LayerMask _solidGroundMask;
        private LayerMask _oneWayGroundMask;

        // --- Probe Settings ---
        private float _groundProbeWidthScale;
        private float _groundProbeHeight;
        private float _groundProbeExtraDistance;
        private float _ceilingProbeWidthScale;
        private float _ceilingProbeHeight;
        private float _ceilingProbeExtraDistance;

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
        // 클래스 필드
        private System.Func<bool> _isDashActive; // 외부(대시)에서 현재 대시 중인지 질의
        private System.Func<bool> _isWallActionActive; // 외부(벽 액션)에서 현재 벽 상태인지 질의

        public override void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            base.Initialize(inputManager, characterBase, characterBaseController);

            _rb = actionCharacterBase.characterRigidbody2D;
            _col = actionCharacterBase.colliderMapObject;

            if (_rb == null || _col == null)
            {
                GcLogger.LogError("[ActionJump] Rigidbody2D/Collider2D가 필요합니다.");
                return;
            }

            // Animator/클립 길이 수집
            _clipLength = actionCharacterBase.CharacterAnimationController.GetAnimationAllLength();

            RefreshCollisionMasks();

            actionCharacterBase.OnAnimationEventJump += OnAnimationEventJump;
            
            _wasGrounded = IsGroundedByCollision();
            _airborneTime = 0f;
            _changedGravity = false;
        }

        public override void OnDestroy() 
        {
            base.OnDestroy();
            actionCharacterBase.OnAnimationEventJump -= OnAnimationEventJump;
        }

        protected override void ApplySettings()
        {
            if (playerActionSettings)
            {
                _desiredJumpHeight = playerActionSettings.jumpHeight;
                _timeToApex = playerActionSettings.jumpSpeed;
                _groundProbeWidthScale = Mathf.Clamp(playerActionSettings.jumpGroundProbeWidthScale, 0.1f, 1f);
                _groundProbeHeight = Mathf.Max(0.01f, playerActionSettings.jumpGroundProbeHeight);
                _groundProbeExtraDistance = Mathf.Max(0f, playerActionSettings.jumpGroundProbeExtraDistance);
                _ceilingProbeWidthScale = Mathf.Clamp(playerActionSettings.jumpCeilingProbeWidthScale, 0.1f, 1f);
                _ceilingProbeHeight = Mathf.Max(0.01f, playerActionSettings.jumpCeilingProbeHeight);
                _ceilingProbeExtraDistance = Mathf.Max(0f, playerActionSettings.jumpCeilingProbeExtraDistance);
            }
            else
            {
                _groundProbeWidthScale = 0.8f;
                _groundProbeHeight = 0.08f;
                _groundProbeExtraDistance = 0.04f;
                _ceilingProbeWidthScale = 0.7f;
                _ceilingProbeHeight = 0.06f;
                _ceilingProbeExtraDistance = 0.02f;
            }

            RefreshCollisionMasks();
            RecalculatePhysicsConstants(_desiredJumpHeight, _timeToApex);

            ApplyJumpAnimationNames();
            RefreshJumpAnimationAvailability();
        }

        private void ApplyJumpAnimationNames()
        {
            // Settings가 없거나 값이 비어있으면 기본값으로 폴백
            var prefix = playerActionSettings != null && !string.IsNullOrWhiteSpace(playerActionSettings.prefixJumpAnimation)
                ? playerActionSettings.prefixJumpAnimation
                : "moveset_running_jump";

            _animJumpStart      = prefix;
            _animJumpUpLoop     = prefix + "_up";
            _animJumpChangeFall = prefix + "_change_fall";
            _animJumpFallLoop   = prefix + "_fall";
            _animJumpEnd        = prefix + "_end";
        }
        private void RefreshJumpAnimationAvailability()
        {
            // 캐릭터 애니메이션 컨트롤러 기준으로 존재 여부 캐시
            _hasStart      = HasAnimation(_animJumpStart);
            _hasUp         = HasAnimation(_animJumpUpLoop);
            _hasChangeFall = HasAnimation(_animJumpChangeFall);
            _hasFall       = HasAnimation(_animJumpFallLoop);
            _hasEnd        = HasAnimation(_animJumpEnd);
        }
        public void Configure(float desiredJumpHeight, float timeToApex)
        {
            RecalculatePhysicsConstants(desiredJumpHeight, timeToApex);
            
            ApplyJumpAnimationNames();
            RefreshJumpAnimationAvailability();
        }

        /// <summary>
        /// 외부 시스템(예: WallJumpEnd)에서 Jump FSM으로 제어권을 인계할 때 사용합니다.
        /// - Height/Speed 기반으로 점프 상수를 재계산하고, Jump 상태를 강제 진입합니다.
        /// - 착지 시 LandOneShot 단계가 정상 동작하도록 Jump FSM을 활성화합니다.
        /// </summary>
        /// <param name="initialVelocity">인계 시점의 초기 속도(수평 포함). y는 Height/Speed에 맞춘 최소 vy를 보장합니다.</param>
        /// <param name="desiredJumpHeight">정점 높이(월드 유닛)</param>
        /// <param name="timeToApex">정점까지 시간(초)</param>
        /// <param name="playStartOneShot">true면 StartOneShot을 재생(기본 false: UpLoop/FallLoop로 바로 진입)</param>
        public void BeginJumpFromExternal(Vector2 initialVelocity, float desiredJumpHeight, float timeToApex, bool playStartOneShot)
        {
            if (_rb == null) return;

            // Jump 물리 상수 재계산
            RecalculatePhysicsConstants(desiredJumpHeight, timeToApex);

            // Jump 상태로 전환(착지 처리 포함)
            if (!actionCharacterBase.IsStatusJump())
                actionCharacterBase.SetStatusJump();

            // 중력 스케일 변경 및 복구 대상 표시
            _prevGravityScale = _rb.gravityScale;
            _rb.gravityScale = _baseGravityScale;
            _changedGravity = true;

            // Height/Speed에 맞춘 최소 vy를 보장
            float vy = Mathf.Max(initialVelocity.y, _jumpVelocityY);
            _rb.SetLinearVelocity(new Vector2(initialVelocity.x, vy));

            // Phase 진입
            if (playStartOneShot && _hasStart)
            {
                EnterPhase(JumpPhase.StartOneShot);
                return;
            }

            // 이미 공중 인계이므로 Up/Fall 루프로 바로 진입
            if (_rb.GetLinearVelocity().y > 0.01f && _hasUp)
                EnterPhase(JumpPhase.UpLoop);
            else
                EnterPhase(JumpPhase.FallLoop);
        }

        private void RecalculatePhysicsConstants(float desiredJumpHeight, float timeToApex)
        {
            ApplyJumpAnimationNames();
            RefreshJumpAnimationAvailability();
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
        public void Jump()
        {
            if (_rb == null) return;

            if (actionCharacterBase.IsStatusAttack()) return;
            if (actionCharacterBase.IsStatusAttackComboWait()) return;
            if (actionCharacterBase.IsStatusJump()) return;

            actionCharacterBase.SetStatusJump();

            // 점프 입력: 중력 스케일 변경 및 복구 대상 표시
            _prevGravityScale = _rb.gravityScale;
            _rb.gravityScale  = _baseGravityScale;
            _changedGravity   = true;

            float vy = Mathf.Max(_rb.GetLinearVelocity().y, _jumpVelocityY);
            _rb.SetLinearVelocity(new Vector2(_rb.GetLinearVelocity().x, vy));

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

            // 벽 액션(매달림/미끄러짐)이 활성 상태면 Jump FSM의 낙하/착지 전이를 강제하지 않는다.
            // (벽 액션이 velocity/gravity를 별도 제어하기 때문)
            if (_isWallActionActive != null && _isWallActionActive())
            {
                _airborneTime = 0f;
                _wasGrounded = IsGroundedByCollision();
                return;
            }

            
            // --- 대시 중이면 점프 FSM의 '클리프 낙하 감지/상태 전환'을 잠시 중단 ---
            //  - 점프 상태가 아니고(_phase == None), 대시 중일 때 불필요한 Jump 상태 진입을 차단
            //  - 점프 중(원샷/루프 진행)인 상태에서도 대시가 개입했다면, 이벤트 워치독/전이 충돌을 방지
            if (_isDashActive != null && _isDashActive())
            {
                // 낙하 누적 타이머를 초기화하여 대시가 끝난 즉시 Jump 전환이 폭발하지 않도록 함
                _airborneTime = 0f;
                _wasGrounded = IsGroundedByCollision();
                return; // <-- 대시 중에는 Jump FSM 갱신을 스킵
            }
            
            bool grounded = IsGroundedByCollision();

            // --- [추가] 점프 FSM이 꺼져 있고(=입력 점프 아님), 지면을 잃었을 때 낙하 감지 ---
            if (_phase == JumpPhase.None)
            {
                if (!grounded)
                {
                    _airborneTime += Time.deltaTime;

                    // 충분히 공중 상태가 지속되고, 실제로 하강 중일 때만 낙하 인정
                    if (_airborneTime >= CoyoteThreshold && _rb.GetLinearVelocity().y <= MinFallSpeedY)
                    {
                        // 전투 등 방해 상태는 존중
                        if (!actionCharacterBase.IsStatusAttack() && !actionCharacterBase.IsStatusAttackComboWait())
                        {
                            // 공중 상태로 전환(프로젝트 표준에 맞춰 Jump 상태 사용)
                            if (!actionCharacterBase.IsStatusJump()) actionCharacterBase.SetStatusJump();

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

            float vy = _rb.GetLinearVelocity().y;

            switch (_phase)
            {
                case JumpPhase.UpLoop:
                    if (vy <= 0.0001f || IsCeilingHit()) EnterPhase(JumpPhase.ApexChange);
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
            if (_hasUp) PlayAnimSafe(_animJumpUpLoop);
            _phase = JumpPhase.UpLoop;
        }

        private void HandleJumpChangeFallOneShotEnd()
        {
            if (_phase != JumpPhase.ApexChange) return;
            ClearAwaiting();
            // 하강 루프 진입 (없으면 전환만)
            if (_hasFall) PlayAnimSafe(_animJumpFallLoop);
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
                        PlayAnimSafe(_animJumpStart);
                        StartAwaiting(next, _animJumpStart);
                    }
                    else
                    {
                        // jump가 없으면 즉시 UpLoop로
                        HandleJumpStartOneShotEnd();
                    }
                    break;

                case JumpPhase.UpLoop:
                    if (_hasUp) PlayAnimSafe(_animJumpUpLoop);
                    // 루프는 이벤트 대기 없음
                    break;

                case JumpPhase.ApexChange:
                    if (_hasChangeFall)
                    {
                        PlayAnimSafe(_animJumpChangeFall);
                        StartAwaiting(next, _animJumpChangeFall);
                    }
                    else
                    {
                        // change_fall이 없으면 즉시 FallLoop로
                        HandleJumpChangeFallOneShotEnd();
                    }
                    break;

                case JumpPhase.FallLoop:
                    if (_hasFall) PlayAnimSafe(_animJumpFallLoop);
                    break;

                case JumpPhase.LandOneShot:
                    if (_hasEnd)
                    {
                        PlayAnimSafe(_animJumpEnd);
                        StartAwaiting(next, _animJumpEnd);
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
            return DefaultOneshotTimeout;
        }

        private void FinishAndStop()
        {
            _phase = JumpPhase.None;

            // 점프 입력으로만 중력을 바꿨을 때 복구
            if (_changedGravity && _rb != null)
                _rb.gravityScale = _prevGravityScale;

            actionCharacterBase.Stop();
        }

        private void PlayAnimSafe(string stateName)
        {
            actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(stateName);
        }

        public bool IsGroundedByCollision()
        {
            if (_col == null || _groundMask == 0) return false;

            Bounds bounds = _col.bounds;
            Vector2 size = BuildGroundProbeSize(bounds);
            Vector2 center = new Vector2(bounds.center.x, bounds.min.y - _groundProbeExtraDistance + (size.y * 0.5f));

            return Physics2D.OverlapBox(center, size, 0f, _groundMask) != null;
        }

        private bool IsCeilingHit()
        {
            if (_col == null || _solidGroundMask == 0) return false;

            Bounds bounds = _col.bounds;
            Vector2 size = BuildCeilingProbeSize(bounds);
            Vector2 center = new Vector2(bounds.center.x, bounds.max.y + _ceilingProbeExtraDistance - (size.y * 0.5f));

            return Physics2D.OverlapBox(center, size, 0f, _solidGroundMask) != null;
        }

        private Vector2 BuildGroundProbeSize(Bounds bounds)
        {
            float width = Mathf.Max(0.02f, bounds.size.x * _groundProbeWidthScale);
            return new Vector2(width, _groundProbeHeight);
        }

        private Vector2 BuildCeilingProbeSize(Bounds bounds)
        {
            float width = Mathf.Max(0.02f, bounds.size.x * _ceilingProbeWidthScale);
            return new Vector2(width, _ceilingProbeHeight);
        }

        private void RefreshCollisionMasks()
        {
            string solidGroundLayerName = ConfigLayer.GetValue(ConfigLayer.Keys.TileMapGround);
            _solidGroundMask = LayerMask.GetMask(solidGroundLayerName);
            if (_solidGroundMask == 0)
            {
                GcLogger.LogWarning($"[ActionJump] Layer '{solidGroundLayerName}'를 찾을 수 없습니다. Project Settings > Tags and Layers 확인.");
            }

            _oneWayGroundMask = 0;
            if (playerActionSettings != null && !string.IsNullOrWhiteSpace(playerActionSettings.jumpOneWayPlatformLayerName))
            {
                _oneWayGroundMask = LayerMask.GetMask(playerActionSettings.jumpOneWayPlatformLayerName);
                if (_oneWayGroundMask == 0)
                {
                    GcLogger.LogWarning($"[ActionJump] One Way Platform Layer '{playerActionSettings.jumpOneWayPlatformLayerName}'를 찾을 수 없습니다. Project Settings > Tags and Layers 확인.");
                }
            }

            _groundMask = _solidGroundMask | _oneWayGroundMask;
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
        /// <summary>
        /// 점프를 강제로 중단합니다.
        /// - skipLandAnimation == false: 보유 시 'jump_end'를 재생하며 정상 종료 단계로 수렴(LandOneShot).
        /// - skipLandAnimation == true : 애니메이션 스킵하고 즉시 종료.
        /// - restoreGravity: 점프 입력으로 중력스케일을 변경한 경우(_changedGravity=true) 복구할지 여부(기본 true).
        /// 어느 Phase에서 호출돼도 안전합니다.
        /// </summary>
        public void CancelJump(bool skipLandAnimation = false, bool restoreGravity = true)
        {
            if (_rb == null) return;

            // 이미 종료 상태면 무시
            if (_phase == JumpPhase.None)
                return;

            // 워치독/대기 상태 해제
            ClearAwaiting();

            // 즉시 종료 경로 (엔딩 애니메이션 스킵 또는 jump_end 미보유)
            if (skipLandAnimation || !_hasEnd)
            {
                // 점프 입력으로 중력을 바꿨었다면 선택적으로 복구
                if (restoreGravity && _changedGravity)
                {
                    _rb.gravityScale = _prevGravityScale;
                }

                _phase = JumpPhase.None;
                _changedGravity = false; // 복구 처리 완료
                actionCharacterBase.Stop();   // 프로젝트 표준 상태 복귀(Idle/Run 등)
                return;
            }

            // 엔딩 애니메이션을 재생하며 종료
            // 현재 단계(시작/상승/전환/하강)가 무엇이든 LandOneShot로 수렴시킴
            EnterPhase(JumpPhase.LandOneShot);
        }
        // 외부에서 연결할 API
        public void SetDashActiveQuery(System.Func<bool> query)
        {
            _isDashActive = query;
        }

        public void SetWallActionActiveQuery(System.Func<bool> query)
        {
            _isWallActionActive = query;
        }
    }
}
