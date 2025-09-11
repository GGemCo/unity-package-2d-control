using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 사다리 액션
    /// - F로 진입: climb(1회) → climb_wait(loop)
    /// - ↑ 입력: climb_up(loop) + Y+
    /// - ↓ 입력: climb_down(loop) + Y-
    /// - 상/하단 도달: climb_end(1회) 후 종료
    /// - 좌/우 이동(옵션): canMoveSidePlayClimbing=true일 때 허용, 범위 이탈 예측 시 종료
    /// - 애니 이벤트 누락 대비 워치독 포함
    /// </summary>
    public class ActionClimb : ActionBase
    {
        // 외부 질의
        public bool IsClimbing => _phase != ClimbPhase.None;
        public event System.Action<IInteraction> InteractionEnded;

        // --- 캐시 ---
        private Rigidbody2D _rigidbody;
        private Collider2D _colliderMapObject;
        private Collider2D _colliderHitArea;
        private PlayerInput _playerInput;

        // 애니 길이(워치독)
        private Dictionary<string, float> _clipLength = new();

        // 이동/전이 파라미터
        private float _climbSpeed;               // 유닛/초
        private float _prevGravityScale;         // 복귀용
        private Vector2 _velocityCache;          // 진입 시 속도 저장
        private bool _changedGravity;            // 진입 시 중력 0 고정 여부
        private bool _canMoveSideWhileClimbing;  // 좌/우 이동 허용 여부

        // Ladder 참조 및 경계
        private ObjectClimb _climb;
        private float _topY, _bottomY, _topSnap, _bottomSnap;
        private float _leftX, _rightX;           // 가로 경계
        private readonly float _sideExitMargin = 0.0f;    // 경계 예외 허용폭(필요 시 조정)

        // 입력
        private InputAction _moveAction;

        // 애니 이름
        private const string AnimLadderEnter = "climb";
        private const string AnimLadderWait  = "climb_wait";
        private const string AnimLadderUp    = "climb_up";
        private const string AnimLadderDown  = "climb_down";
        private const string AnimLadderEnd   = "climb_end";
        // 하단 부분에서 시작할때
        private const string AnimLadderStartBottom = "climb_start_bottom";
        // 상단 부분에서 시작할때
        private const string AnimLadderStartTop    = "climb_start_top";
        // 하단 부분에서 종료할때
        private const string AnimLadderEndBottom = "climb_end_bottom";
        // 상단 부분에서 종료할때
        private const string AnimLadderEndTop    = "climb_end_top";

        // 보유 여부
        private bool _hasEnter, _hasStartBottom, _hasStartTop, _hasEndBottom, _hasEndTop, _hasWait, _hasUp, _hasDown, _hasEnd;

        // Phase
        private enum ClimbPhase
        {
            None,
            EnterOneShot, // climb(1회) → wait
            WaitLoop,     // climb_wait(loop)
            UpLoop,       // climb_up(loop)
            DownLoop,     // climb_down(loop)
            EndOneShot    // climb_end(1회) → 종료
        }
        private ClimbPhase _phase = ClimbPhase.None;

        // 이벤트 워치독
        private ClimbPhase _awaitingEventFor = ClimbPhase.None;
        private float _awaitingDeadline;
        private const float DefaultOneShotTimeout = 0.25f;
        // 시작 위치 판단 결과
        private enum StartSide { Bottom, Top }               
        private StartSide _startSide = StartSide.Bottom;     // 진입 시 계산 결과 저장
        // 종료 원인
        private enum EndKind { Default, Top, Bottom, Side }               // ★ 추가
        private EndKind _endKind = EndKind.Default;                       // ★ 추가

        // 시작 위치 판정 마진(옵션)
        // private readonly float _startSideDetectMargin = 0.0f; // min/max에 여유치가 필요하면 조절

        public override void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            base.Initialize(inputManager, characterBase, characterBaseController);

            _rigidbody  = actionCharacterBase.characterRigidbody2D;
            _colliderMapObject = actionCharacterBase.colliderMapObject;
            _colliderHitArea   = actionCharacterBase.colliderHitArea;
            _playerInput = actionCharacterBase.GetComponent<PlayerInput>();

            if (_rigidbody == null || _colliderMapObject == null)
            {
                GcLogger.LogError("[ActionClimb] Rigidbody2D/Collider2D가 필요합니다.");
                return;
            }

            // 애니 길이 수집(워치독용)
            _clipLength = actionCharacterBase.CharacterAnimationController.GetAnimationAllLength();

            // 보유 여부 캐시
            _hasEnter       = HasAnimation(AnimLadderEnter);
            _hasWait        = HasAnimation(AnimLadderWait);
            _hasUp          = HasAnimation(AnimLadderUp);
            _hasDown        = HasAnimation(AnimLadderDown);
            _hasEnd         = HasAnimation(AnimLadderEnd);
            _hasStartBottom = HasAnimation(AnimLadderStartBottom);
            _hasStartTop    = HasAnimation(AnimLadderStartTop);
            _hasEndBottom = HasAnimation(AnimLadderEndBottom);
            _hasEndTop    = HasAnimation(AnimLadderEndTop);

            if (_playerInput != null)
                _moveAction = _playerInput.actions[ConfigCommonControl.NameActionMove];
        }

        protected override void ApplySettings()
        {
            _climbSpeed = playerActionSettings ? Mathf.Max(0.01f, playerActionSettings.climbSpeed) : 2.8f;
            _canMoveSideWhileClimbing = playerActionSettings && playerActionSettings.canMoveSidePlayClimbing; // [NEW]
        }

        /// <summary>InputManager.TryBeginLadder 에서 호출</summary>
        public bool Begin(ObjectClimb climb)
        {
            if (_rigidbody == null) return false;
            if (IsClimbing) return true;

            _climb = climb;
            _topY      = _climb.GetTopY();
            _bottomY   = _climb.GetBottomY();
            _topSnap   = _climb.TopExitSnapOffset;
            _bottomSnap= _climb.BottomExitSnapOffset;
            if (_climb.ClimbSpeed > 0) _climbSpeed = _climb.ClimbSpeed;

            // 가로 경계 계산 (가능하면 Ladder의 콜라이더 바운즈 사용)
            if (!TryGetLadderHorizontalBounds(_climb, out _leftX, out _rightX))
            {
                // 최소 안전값: 월드 중앙 ±0.3 (프로젝트 상황에 맞게 조정/교체)
                float cx = _climb.WorldCenterX;
                _leftX  = cx - 0.3f;
                _rightX = cx + 0.3f;
            }
            
            // 시작 위치(아래/위) 판정
            {
                float y = actionCharacterBase.transform.position.y;
                // 상/하단과의 거리 비교(마진이 있으면 상하단 근처로 인식)
                // float distToBottom = Mathf.Abs(y - (_bottomY + _startSideDetectMargin));
                // float distToTop    = Mathf.Abs(y - (_topY    - _startSideDetectMargin));
                // _startSide = (distToTop < distToBottom) ? StartSide.Top : StartSide.Bottom;
                
                // 탑/바텀 근처 임계치(비율 또는 절대 거리)로 판정
                float height = _topY - _bottomY;
                float nearThreshold = Mathf.Max(0.1f, height * 0.15f); // 전체 높이의 15% 구간을 탑/바텀 근처로 간주
                if (y >= _topY - nearThreshold)      _startSide = StartSide.Top;
                else if (y <= _bottomY + nearThreshold) _startSide = StartSide.Bottom;
                else
                {
                    // 중간 지점 → 공통 시작 애니(또는 바로 wait로)
                    _startSide = StartSide.Bottom; // 임의 값
                    // 그리고 EnterOneShot에서 _hasStartBottom/_hasStartTop이 없으면 AnimLadderEnter로 폴백됨
                }
            }

            // 물리 고정
            _prevGravityScale      = _rigidbody.gravityScale;
            _velocityCache         = _rigidbody.linearVelocity;
            _rigidbody.gravityScale= 0f;
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.bodyType    = RigidbodyType2D.Kinematic;
            _changedGravity        = true;

            // 상태 전이
            actionCharacterBase.SetStatusClimb();
            // X를 사다리 중앙으로 스냅
            actionCharacterBase.MoveTeleport(_climb.WorldCenterX, actionCharacterBase.transform.position.y);

            // 진입 애니 → 대기
            EnterPhase(ClimbPhase.EnterOneShot);
            return true;
        }

        /// <summary>InputManager.EndLadder 에서 호출</summary>
        public void End(ObjectClimb climb)
        {
            if (!IsClimbing) return;
            _endKind = EndKind.Default;
            CancelClimb(skipEndAnimation: true, restoreGravity: true);
        }

        /// <summary>InputManager.FixedUpdate 에서 매 프레임 호출</summary>
        public void Update()
        {
            if (!IsClimbing || _rigidbody == null) return;

            // 입력
            Vector2 input = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            float v = input.y;
            float h = _canMoveSideWhileClimbing ? input.x : 0f;

            float y = _rigidbody.position.y;

            switch (_phase)
            {
                case ClimbPhase.EnterOneShot:
                    // 이벤트/워치독으로 wait 전이
                    break;

                case ClimbPhase.WaitLoop:
                {
                    // 수평만 입력된 경우: 애니는 wait 유지, 위치만 갱신
                    if (Mathf.Abs(h) > 0.1f && Mathf.Abs(v) <= 0.1f)
                    {
                        if (!TryMoveHorizontalWithinBounds(h))
                        {
                            _endKind = EndKind.Side;
                            EnterPhase(ClimbPhase.EndOneShot);
                            break;
                        }
                        PlayIfHas(AnimLadderWait);
                        _rigidbody.linearVelocity = Vector2.zero;
                        break;
                    }

                    if (v > 0.1f)
                    {
                        if (y >= _topY - _topSnap)
                        {
                            _endKind = EndKind.Top;
                            EnterPhase(ClimbPhase.EndOneShot);
                        }
                        else
                            EnterPhase(ClimbPhase.UpLoop);
                    }
                    else if (v < -0.1f)
                    {
                        if (y <= _bottomY + _bottomSnap)
                        {
                            _endKind = EndKind.Bottom;
                            EnterPhase(ClimbPhase.EndOneShot);
                        }
                        else
                            EnterPhase(ClimbPhase.DownLoop);
                    }
                    else
                    {
                        PlayIfHas(AnimLadderWait);
                        _rigidbody.linearVelocity = Vector2.zero;
                    }
                    break;
                }

                case ClimbPhase.UpLoop:
                {
                    if (y >= _topY - _topSnap)
                    {
                        _endKind = EndKind.Top;
                        EnterPhase(ClimbPhase.EndOneShot);
                        break;
                    }

                    if (!(v > 0.1f))
                    {
                        EnterPhase(ClimbPhase.WaitLoop);
                        break;
                    }

                    // 수평 입력이 있으면 같이 이동(경계 이탈 시 종료)
                    if (Mathf.Abs(h) > 0.1f && !TryMoveHorizontalWithinBounds(h))
                    {
                        _endKind = EndKind.Side;
                        EnterPhase(ClimbPhase.EndOneShot);
                        break;
                    }

                    PlayIfHas(AnimLadderUp);
                    MoveVertical(v);
                    break;
                }

                case ClimbPhase.DownLoop:
                {
                    if (y <= _bottomY + _bottomSnap)
                    {
                        _endKind = EndKind.Bottom;
                        EnterPhase(ClimbPhase.EndOneShot);
                        break;
                    }

                    if (!(v < -0.1f))
                    {
                        EnterPhase(ClimbPhase.WaitLoop);
                        break;
                    }

                    if (Mathf.Abs(h) > 0.1f && !TryMoveHorizontalWithinBounds(h))
                    {
                        _endKind = EndKind.Side;
                        EnterPhase(ClimbPhase.EndOneShot);
                        break;
                    }

                    PlayIfHas(AnimLadderDown);
                    MoveVertical(v);
                    break;
                }

                case ClimbPhase.EndOneShot:
                    // 이벤트/워치독으로 FinishAndStop()
                    break;
            }

            // 원샷 단계 워치독
            if (_awaitingEventFor != ClimbPhase.None && Time.time >= _awaitingDeadline)
            {
                switch (_awaitingEventFor)
                {
                    case ClimbPhase.EnterOneShot: HandleEnterOneShotEnd(); break;
                    case ClimbPhase.EndOneShot:   HandleEndOneShotEnd();   break;
                }
            }
        }

        // --------- Phase 전환/핸들러 ---------

        private void EnterPhase(ClimbPhase next)
        {
            _phase = next;
            ClearAwaiting();

            switch (next)
            {
                case ClimbPhase.EnterOneShot:
                    // 시작 애니 선택 로직
                    string startAnim = null;
                    if (_startSide == StartSide.Bottom && _hasStartBottom)
                        startAnim = AnimLadderStartBottom;
                    else if (_startSide == StartSide.Top && _hasStartTop)
                        startAnim = AnimLadderStartTop;
                    else if (_hasEnter)
                        startAnim = AnimLadderEnter; // 공통 시작 폴백

                    if (!string.IsNullOrEmpty(startAnim))
                    {
                        Play(startAnim);
                        StartAwaiting(next, startAnim);
                    }
                    else
                    {
                        // 모든 시작 애니가 없으면 곧바로 대기로
                        HandleEnterOneShotEnd();
                    }
                    break;

                case ClimbPhase.WaitLoop:
                    if (_hasWait) PlayIfHas(AnimLadderWait);
                    _rigidbody.linearVelocity = Vector2.zero;
                    break;

                case ClimbPhase.UpLoop:
                    PlayIfHas(AnimLadderUp);
                    break;

                case ClimbPhase.DownLoop:
                    PlayIfHas(AnimLadderDown);
                    break;

                case ClimbPhase.EndOneShot:
                    // ★ 종료 애니 선택 우선순위: Side → Top → Bottom → Default
                    string endAnim = null;

                    if (_endKind == EndKind.Side)
                    {
                        HandleEndOneShotEnd();
                        return;
                    }
                    
                    if (_endKind == EndKind.Top && _hasEndTop)
                        endAnim = AnimLadderEndTop;
                    else if (_endKind == EndKind.Bottom && _hasEndBottom)
                        endAnim = AnimLadderEndBottom;
                    else if (_hasEnd)
                        endAnim = AnimLadderEnd;

                    if (!string.IsNullOrEmpty(endAnim))
                    {
                        Play(endAnim);
                        StartAwaiting(next, endAnim);
                    }
                    else
                    {
                        // 모든 종료 애니가 없으면 즉시 종료
                        HandleEndOneShotEnd();
                    }
                    break;
            }
        }

        private void HandleEnterOneShotEnd()
        {
            if (_phase != ClimbPhase.EnterOneShot) return;
            ClearAwaiting();
            EnterPhase(ClimbPhase.WaitLoop);
        }

        private void HandleEndOneShotEnd()
        {
            if (_phase != ClimbPhase.EndOneShot) return;
            ClearAwaiting();
            FinishAndStop();
        }

        // --------- 이동 유틸 ---------

        private void MoveVertical(float v)
        {
            // 좌우는 별도 처리. 여기서는 v만 사용.
            Vector3 current = actionCharacterBase.transform.position;
            Vector3 delta   = new Vector3(0f, v, 0f) * (actionCharacterBase.currentMoveStep * actionCharacterBase.GetCurrentMoveSpeed() * _climbSpeed * Time.deltaTime);
            actionCharacterBase.transform.position = current + delta;
        }

        /// <summary>
        /// 수평 이동 시도. 다음 위치가 사다리 가로 경계에서 벗어나면 false.
        /// </summary>
        private bool TryMoveHorizontalWithinBounds(float h)
        {
            Vector3 current = actionCharacterBase.transform.position;
            float   step    = (actionCharacterBase.currentMoveStep * actionCharacterBase.GetCurrentMoveSpeed() * _climbSpeed * Time.deltaTime);
            float   nextX   = current.x + h * step;

            // 경계 체크(예측): 벗어나면 종료 유도
            if (nextX < _leftX - _sideExitMargin || nextX > _rightX + _sideExitMargin)
                return false;

            current.x = Mathf.Clamp(nextX, _leftX, _rightX);
            actionCharacterBase.transform.position = current;
            return true;
        }

        // --------- 애니/워치독 유틸 ---------

        private void Play(string stateName)
        {
            actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(stateName);
        }

        private void PlayIfHas(string stateName)
        {
            if (HasAnimation(stateName))
                actionCharacterBase.CharacterAnimationController?.PlayCharacterAnimation(stateName);
        }

        private bool HasAnimation(string stateName)
        {
            if (actionCharacterBase.CharacterAnimationController is { } ctrl)
                return ctrl.HasAnimation(stateName);
            return _clipLength.ContainsKey(stateName);
        }

        private void StartAwaiting(ClimbPhase phase, string clipName)
        {
            _awaitingEventFor = phase;
            _awaitingDeadline = Time.time + GetClipDurationWithFallback(clipName);
        }

        private void ClearAwaiting()
        {
            _awaitingEventFor = ClimbPhase.None;
            _awaitingDeadline = 0f;
        }

        private float GetClipDurationWithFallback(string clipName)
        {
            if (_clipLength.TryGetValue(clipName, out var len) && len > 0f) return len + 0.02f;
            return DefaultOneShotTimeout;
        }

        private void FinishAndStop()
        {
            var endedTarget = _climb;
            _phase = ClimbPhase.None;

            if (_changedGravity && _rigidbody != null)
            {
                _rigidbody.gravityScale   = _prevGravityScale;
                _rigidbody.linearVelocity = _velocityCache;
                _rigidbody.bodyType       = RigidbodyType2D.Dynamic;
            }
            _changedGravity = false;
            _endKind = EndKind.Default;

            actionCharacterBase.Stop(); // Idle/Run 등 표준 상태로 복귀
            _climb = null;
            InteractionEnded?.Invoke(endedTarget);
        }

        /// <summary>
        /// 어느 Phase에서든 강제 종료.
        /// </summary>
        public void CancelClimb(bool skipEndAnimation = true, bool restoreGravity = true)
        {
            if (_phase == ClimbPhase.None) return;
            ClearAwaiting();

            if (skipEndAnimation || !_hasEnd)
            {
                var endedTarget = _climb;
                if (restoreGravity && _changedGravity && _rigidbody != null)
                {
                    _rigidbody.gravityScale   = _prevGravityScale;
                    _rigidbody.linearVelocity = _velocityCache;
                    _rigidbody.bodyType       = RigidbodyType2D.Dynamic;
                }
                _changedGravity = false;
                _endKind = EndKind.Default;

                _phase = ClimbPhase.None;
                actionCharacterBase.Stop();
                _climb = null;
                InteractionEnded?.Invoke(endedTarget);
                return;
            }

            EnterPhase(ClimbPhase.EndOneShot);
        }

        // --------- 경계 획득 유틸 --------- 
        private static bool TryGetLadderHorizontalBounds(ObjectClimb climb, out float left, out float right)
        {
            left = right = climb.WorldCenterX;
            // 1) ObjectLadder가 전용 콜라이더를 제공하는 경우
            if (climb.TryGetAreaCollider(out var areaCol) && areaCol != null)
            {
                var b = areaCol.bounds;
                left = (float)b.min.x;
                right= (float)b.max.x;
                return true;
            }

            // 2) 직접 Collider2D를 보유하고 있다면
            var col = climb.GetComponent<Collider2D>();
            if (col != null)
            {
                var b = col.bounds;
                left = (float)b.min.x;
                right= (float)b.max.x;
                return true;
            }

            // 실패
            return false;
        }
    }
}
