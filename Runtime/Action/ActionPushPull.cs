using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 박스 밀기/당기기 액션
    /// - F: push(1회) → push_wait(loop)
    /// - ←: pull_play(loop) + 박스 x-
    /// - →: push_play(loop) + 박스 x+
    /// - F: push_end(1회) 후 종료
    /// - 애니 이벤트 누락 대비 워치독 포함
    /// - 종료 시 InteractionEnded(IInteractable) 발행
    /// </summary>
    public class ActionPushPull : ActionBase
    {
        // 외부 질의/이벤트
        public bool IsPushing { get; private set; }
        public event System.Action<IInteraction> InteractionEnded;

        // --- 캐시 ---
        private Rigidbody2D _playerRb;
        private Collider2D _playerCol;
        private PlayerInput _playerInput;

        // 박스 타겟
        private ObjectPushPull _pushPull;
        private Rigidbody2D _objectPushPullRigidbody2D;
        private Collider2D _objectPushPullCollider2D;
        // 캐릭터-박스 결합 정보
        private enum GripSide { Left, Right }
        private GripSide _gripSide;
        [SerializeField] private float _gripGap = 0.02f; // 캐릭터와 박스 사이 최소 간격
        // 환경 충돌 캐스트 버퍼
        private readonly RaycastHit2D[] _castHits = new RaycastHit2D[4];
        private ContactFilter2D _castFilter;

        // 이동/전이 파라미터
        private float _pushMoveSpeed;       // 박스 밀기 이동 속도(유닛/초)
        private float _pullMoveSpeed;       // 박스 당기기 이동 속도(유닛/초)
        // 밀기 / 당기기 제한
        private bool _canPush;
        private bool _canPull;
        
        private float _origPlayerDrag;  // 진입 시 플레이어 드래그/속도 백업(선택)
        private Vector2 _origPlayerVel;

        // 입력
        private InputAction _moveAction;

        // 애니 이름/보유 여부
        private const string AnimEnter   = "push";
        private const string AnimWait    = "push_wait";
        private const string AnimPull    = "pull_play";
        private const string AnimPush    = "push_play";
        private const string AnimEnd     = "push_end";

        private Dictionary<string, float> _clipLength = new();
        private bool _hasEnter, _hasWait, _hasPull, _hasPush, _hasEnd;

        // Phase
        private enum Phase
        {
            None,
            EnterOneShot, // push(1회) → wait
            WaitLoop,     // push_wait(loop)
            PullLoop,     // pull_play(loop)
            PushLoop,     // push_play(loop)
            EndOneShot    // push_end(1회) → 종료
        }
        private Phase _phase = Phase.None;

        // 이벤트 워치독
        private Phase _awaitingEventFor = Phase.None;
        private float _awaitingDeadline;
        private const float DefaultOneShotTimeout = 0.25f;

        // Ground 판정(선택: 밀기 중 점프 방지)
        private LayerMask _groundMask;

        public override void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            base.Initialize(inputManager, characterBase, characterBaseController);

            _playerRb  = actionCharacterBase.characterRigidbody2D;
            _playerCol = actionCharacterBase.colliderMapObject;
            _playerInput = actionCharacterBase.GetComponent<PlayerInput>();

            if (_playerRb == null || _playerCol == null)
            {
                GcLogger.LogError("[ActionPushPull] Rigidbody2D/Collider2D가 필요합니다.");
                return;
            }

            // 애니 길이 수집(워치독)
            _clipLength = actionCharacterBase.CharacterAnimationController.GetAnimationAllLength();

            // 보유 여부 캐시
            _hasEnter = HasAnimation(AnimEnter);
            _hasWait  = HasAnimation(AnimWait);
            _hasPull  = HasAnimation(AnimPull);
            _hasPush  = HasAnimation(AnimPush);
            _hasEnd   = HasAnimation(AnimEnd);

            if (_playerInput != null)
                _moveAction = _playerInput.actions[ConfigCommonControl.NameActionMove];

            // Ground 마스크(점프 금지/지면 제약 등에 활용 가능)
            _groundMask = LayerMask.GetMask(ConfigLayer.GetValue(ConfigLayer.Keys.TileMapGround));
            
            // 캐스트 필터: 트리거 제외, 모든 레이어 기본 충돌
            _castFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };
        }
        protected override void ApplySettings()
        {
            _pushMoveSpeed = playerActionSettings ? Mathf.Max(0.05f, playerActionSettings.pushMoveSpeed) : 1.0f;
            _pullMoveSpeed = playerActionSettings ? Mathf.Max(0.05f, playerActionSettings.pullMoveSpeed) : 1.0f;
        }

        /// <summary>InputManager.TryBeginPushPull 에서 호출(F 진입)</summary>
        public bool Begin(ObjectPushPull target)
        {
            if (IsPushing) return true;
            if (target == null) return false;

            _pushPull = target;
            ApplySettings();
            if (_pushPull.PushMoveSpeed > 0) 
                _pushMoveSpeed = _pushPull.PushMoveSpeed;
            if (_pushPull.PullMoveSpeed > 0) 
                _pullMoveSpeed = _pushPull.PullMoveSpeed;
            _canPush = _pushPull.CanPush;
            _canPull = _pushPull.CanPull;

            // 박스 Rigidbody/Collider 확보
            _objectPushPullRigidbody2D = _pushPull.TargetBody;
            _objectPushPullCollider2D = _pushPull.TargetCollider;
            if (_objectPushPullRigidbody2D == null || _objectPushPullCollider2D == null)
            {
                GcLogger.LogError("[ActionPushPull] Box에 Rigidbody2D/Collider2D가 필요합니다.");
                return false;
            }

            _objectPushPullCollider2D.isTrigger = false;
            
            _origPlayerDrag = _playerRb.GetLinearDamping();
            _origPlayerVel  = _playerRb.GetLinearVelocity();
            
            // 어느 쪽을 잡았는지 판정 후 스냅
            DecideGripSideAndSnapToEdge();

            actionCharacterBase.SetStatusPush();
            IsPushing = true;
            EnterPhase(Phase.EnterOneShot);
            return true;
        }

        /// <summary>InputManager.EndPushPull 에서 호출(F 종료)</summary>
        public void End(ObjectPushPull target)
        {
            if (!IsPushing) return;
            // 종료 애니 1회 재생 경로로 수렴
            EnterPhase(Phase.EndOneShot);
        }

        /// <summary>InputManager.FixedUpdate에서 호출</summary>
        public void Update()
        {
            if (!IsPushing || _playerRb == null) return;

            float x = 0f;
            float moveX = 0f;
            float y = 0f;
            if (_moveAction != null)
            {
                x = _moveAction.ReadValue<Vector2>().x;
                moveX = _moveAction.ReadValue<Vector2>().x;
                y = _moveAction.ReadValue<Vector2>().y;
            }
            
            // 오른쪽 그립일 경우 입력 반전
            if (_gripSide == GripSide.Right)
                x = -x;

            switch (_phase)
            {
                case Phase.EnterOneShot:
                    // 이벤트/워치독 후 Wait로 전이됨
                    break;

                case Phase.WaitLoop:
                {
                    if (x < -0.1f)
                    {
                        if (!_canPull)
                        {
                            GcLogger.Log($"당기기가 제한되어 있습니다.");
                            return;
                        }
                        EnterPhase(Phase.PullLoop);
                    }
                    else if (x > 0.1f)
                    {
                        if (!_canPush)
                        {
                            GcLogger.Log($"밀기가 제한되어 있습니다.");
                            return;
                        }
                        EnterPhase(Phase.PushLoop);
                    }
                    else
                    {
                        PlayIfHas(AnimWait);
                        DampVelocities();
                    }
                    break;
                }

                case Phase.PullLoop:
                {
                    if (!(x < -0.1f))
                    {
                        EnterPhase(Phase.WaitLoop);
                        break;
                    }
                    PlayIfHas(AnimPull);
                    MoveHorizontal(moveX);
                    break;
                }

                case Phase.PushLoop:
                {
                    if (!(x > 0.1f))
                    {
                        EnterPhase(Phase.WaitLoop);
                        break;
                    }
                    PlayIfHas(AnimPush);
                    MoveHorizontal(moveX);
                    break;
                }

                case Phase.EndOneShot:
                    // 이벤트/워치독 후 FinishAndStop()
                    break;
            }

            // 원샷 단계 워치독
            if (_awaitingEventFor != Phase.None && Time.time >= _awaitingDeadline)
            {
                switch (_awaitingEventFor)
                {
                    case Phase.EnterOneShot: HandleEnterOneShotEnd(); break;
                    case Phase.EndOneShot:   HandleEndOneShotEnd();   break;
                }
            }
        }

        // ---------------- Phase 전이 ----------------

        private void EnterPhase(Phase next)
        {
            _phase = next;
            ClearAwaiting();

            switch (next)
            {
                case Phase.EnterOneShot:
                    if (_hasEnter)
                    {
                        Play(AnimEnter);
                        StartAwaiting(next, AnimEnter);
                    }
                    else
                    {
                        HandleEnterOneShotEnd();
                    }
                    break;

                case Phase.WaitLoop:
                    if (_hasWait) PlayIfHas(AnimWait);
                    DampVelocities();
                    break;

                case Phase.PullLoop:
                    PlayIfHas(AnimPull);
                    break;

                case Phase.PushLoop:
                    PlayIfHas(AnimPush);
                    break;

                case Phase.EndOneShot:
                    if (_hasEnd)
                    {
                        Play(AnimEnd);
                        StartAwaiting(next, AnimEnd);
                    }
                    else
                    {
                        HandleEndOneShotEnd();
                    }
                    break;
            }
        }

        private void HandleEnterOneShotEnd()
        {
            if (_phase != Phase.EnterOneShot) return;
            ClearAwaiting();
            EnterPhase(Phase.WaitLoop);
        }

        private void HandleEndOneShotEnd()
        {
            if (_phase != Phase.EndOneShot) return;
            ClearAwaiting();
            FinishAndStop();
        }

        // ---------------- 동작 유틸 ----------------

        private void MoveHorizontal(float x)
        {
            if (_objectPushPullRigidbody2D == null || _objectPushPullCollider2D == null) return;

            // 1) 프레임 이동량 계산 (프로젝트 표준 속도 합성 사용)

            float step = actionCharacterBase.currentMoveStep * actionCharacterBase.GetCurrentMoveSpeed() *
                         (_phase == Phase.PullLoop?_pullMoveSpeed:_pushMoveSpeed) * Time.deltaTime;
            float dx   = x * step;
            if (Mathf.Approximately(dx, 0f)) { DampVelocities(); return; }

            // 2) 박스 이동 시도: 충돌 예측(Cast) → 가능하면 MovePosition
            Vector2 boxPos   = _objectPushPullRigidbody2D.position;
            Vector2 toDir    = new Vector2(Mathf.Sign(dx), 0f);
            float   dist     = Mathf.Abs(dx);

            bool canMoveBox = true;
            int hitCount = _objectPushPullRigidbody2D.Cast(toDir, _castFilter, _castHits, dist);
            for (int i = 0; i < hitCount; i++)
            {
                var h = _castHits[i];
                if (h.collider == null) continue;

                // 자기 자신/플레이어와의 충돌은 무시
                if (h.collider == _objectPushPullCollider2D || h.collider == _playerCol) continue;

                // 벽/장애물에 부딪히면 이동 불가
                canMoveBox = false;
                break;
            }

            if (canMoveBox)
            {
                _objectPushPullRigidbody2D.MovePosition(new Vector2(boxPos.x + dx, boxPos.y));
            }
            else
            {
                // 이동 불가 시 박스 속도 감쇠
                _objectPushPullRigidbody2D.SetLinearVelocity(new Vector2(0f, _objectPushPullRigidbody2D.GetLinearVelocity().y));
            }

            // 3) 박스 최신 바운즈 기준으로 플레이어를 모서리에 스냅(항상 붙여 유지)
            SnapPlayerToBoxEdge(_gripSide);

            // 4) 캐릭터의 수직 속도는 보존(점프 방지용이면 0으로)
            _playerRb.SetLinearVelocity(new Vector2(0f, _playerRb.GetLinearVelocity().y));
        }

        private void DampVelocities()
        {
            // GcLogger.Log($"DampVelocities");
            // 박스/플레이어의 수평 속도 제거하여 정지 안정화
            if (_objectPushPullRigidbody2D != null)     
                _objectPushPullRigidbody2D.SetLinearVelocity(new Vector2(0f, _objectPushPullRigidbody2D.GetLinearVelocity().y));
            if (_playerRb != null)  
                _playerRb.SetLinearVelocity(new Vector2(0f, _playerRb.GetLinearVelocity().y));

            // 플레이어는 모서리에 계속 스냅
            if (_objectPushPullCollider2D != null) SnapPlayerToBoxEdge(_gripSide);
        }

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

        private void StartAwaiting(Phase phase, string clipName)
        {
            _awaitingEventFor = phase;
            _awaitingDeadline = Time.time + GetClipDurationWithFallback(clipName);
        }

        private void ClearAwaiting()
        {
            _awaitingEventFor = Phase.None;
            _awaitingDeadline = 0f;
        }

        private float GetClipDurationWithFallback(string clipName)
        {
            if (_clipLength.TryGetValue(clipName, out var len) && len > 0f)
                return len + 0.02f;
            return DefaultOneShotTimeout;
        }

        private void FinishAndStop()
        {
            var ended = _pushPull;

            _phase = Phase.None;
            IsPushing = false;

            // 플레이어 물리 복구
            _playerRb.SetLinearDamping(_origPlayerDrag);
            _playerRb.SetLinearVelocity(_origPlayerVel);
            
            _objectPushPullCollider2D.isTrigger = true;

            actionCharacterBase.Stop();

            _pushPull = null;

            // InputManager에 종료 알림
            InteractionEnded?.Invoke(ended);
        }

        /// <summary>
        /// 어느 Phase에서든 강제 종료.
        /// skipEndAnimation = true 면 'push_end' 재생 없이 즉시 종료.
        /// </summary>
        public void Cancel(bool skipEndAnimation = true)
        {
            if (_phase == Phase.None) return;

            ClearAwaiting();

            if (skipEndAnimation || !_hasEnd)
            {
                FinishAndStop();
                return;
            }
            EnterPhase(Phase.EndOneShot);
        }

        // --- 선택: 지면 체크(밀기 중 점프 금지 등에 활용) ---
        private bool IsGroundedByCollision()
        {
            if (_playerCol == null) return false;
            return _playerCol.IsTouchingLayers(_groundMask);
        }
        private void DecideGripSideAndSnapToEdge()
        {
            // 플레이어가 박스 어느 쪽에 가까운지로 결정
            Bounds boxB = _objectPushPullCollider2D.bounds;
            Bounds plyB = _playerCol.bounds;

            float distToLeft  = Mathf.Abs(plyB.center.x - boxB.min.x);
            float distToRight = Mathf.Abs(plyB.center.x - boxB.max.x);
            _gripSide = (distToLeft <= distToRight) ? GripSide.Left : GripSide.Right;

            SnapPlayerToBoxEdge(_gripSide);
        }

        private void SnapPlayerToBoxEdge(GripSide side)
        {
            Bounds boxB = _objectPushPullCollider2D.bounds;
            Bounds plyB = _playerCol.bounds;

            float plyHalfW = plyB.extents.x;
            float targetX;
            if (side == GripSide.Left)
            {
                // 플레이어를 박스 왼쪽 바깥에 붙임
                targetX = boxB.min.x - _gripGap - plyHalfW;
            }
            else
            {
                // 플레이어를 박스 오른쪽 바깥에 붙임
                targetX = boxB.max.x + _gripGap + plyHalfW;
            }

            Vector3 p = actionCharacterBase.transform.position;
            p.x = targetX;
            actionCharacterBase.transform.position = p;

            // 캐릭터 바라보는 방향(선택)
            if (side == GripSide.Left && actionCharacterBase.CurrentFacing == CharacterConstants.FacingDirection8.Left)
            {
                actionCharacterBase.SetFacing(CharacterConstants.FacingDirection8.Right);
            }
            else if (side == GripSide.Right &&
                     actionCharacterBase.CurrentFacing == CharacterConstants.FacingDirection8.Right)
            {
                actionCharacterBase.SetFacing(CharacterConstants.FacingDirection8.Left);
            }
        }
    }
}
