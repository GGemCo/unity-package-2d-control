using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

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
    public class ActionPushPull
    {
        // 외부 질의/이벤트
        public bool IsPushing { get; private set; }
        public event System.Action<IInteraction> InteractionEnded;

        // --- 외부 참조 ---
        private InputManager _inputManager;
        private CharacterBase _characterBase;
        private CharacterBaseController _characterBaseController;

        // --- 캐시 ---
        private Rigidbody2D _playerRb;
        private Collider2D _playerCol;
        private PlayerInput _playerInput;

        // 박스 타겟
        private ObjectBox _box;
        private Rigidbody2D _boxRb;
        private TilemapCollider2D _boxCol;
        // 캐릭터-박스 결합 정보
        private enum GripSide { Left, Right }
        private GripSide _gripSide;
        [SerializeField] private float _gripGap = 0.02f; // 캐릭터와 박스 사이 최소 간격
        // 환경 충돌 캐스트 버퍼
        private readonly RaycastHit2D[] _castHits = new RaycastHit2D[4];
        private ContactFilter2D _castFilter;

        // 이동/전이 파라미터
        private float _moveSpeed;       // 박스 이동 속도(유닛/초)
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

        public void Initialize(InputManager inputManager, CharacterBase characterBase, CharacterBaseController characterBaseController)
        {
            _inputManager = inputManager;
            _characterBase = characterBase;
            _characterBaseController = characterBaseController;

            _playerRb  = _characterBase.characterRigidbody2D;
            _playerCol = _characterBase.colliderMapObject;
            _playerInput = _characterBase.GetComponent<PlayerInput>();

            if (_playerRb == null || _playerCol == null)
            {
                GcLogger.LogError("[ActionPushPull] Rigidbody2D/Collider2D가 필요합니다.");
                return;
            }

            // 설정 로드
            var playerActionSettings = AddressableLoaderSettingsControl.Instance.playerActionSettings;
            _moveSpeed = playerActionSettings ? Mathf.Max(0.05f, playerActionSettings.pushMoveSpeed) : 1.0f;

            // 애니 길이 수집(워치독)
            _clipLength = _characterBase.CharacterAnimationController.GetAnimationAllLength();

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

        public void OnDestroy()
        {
            // 필요 시 이벤트 해제 등
        }

        /// <summary>InputManager.TryBeginPushPull 에서 호출(F 진입)</summary>
        public bool Begin(ObjectBox target)
        {
            if (IsPushing) return true;
            if (target == null) return false;

            _box = target;

            // 박스 Rigidbody/Collider 확보
            _boxRb = _box.GetComponent<Rigidbody2D>();
            _boxCol = _box.GetComponent<TilemapCollider2D>();
            if (_boxRb == null || _boxCol == null)
            {
                GcLogger.LogError("[ActionPushPull] Box에 Rigidbody2D/Collider2D가 필요합니다.");
                return false;
            }

            _boxCol.isTrigger = false;
            
            _origPlayerDrag = _playerRb.linearDamping;
            _origPlayerVel  = _playerRb.linearVelocity;

            // 어느 쪽을 잡았는지 판정 후 스냅
            DecideGripSideAndSnapToEdge();

            _characterBase.SetStatusPush();
            IsPushing = true;
            EnterPhase(Phase.EnterOneShot);
            return true;
        }

        /// <summary>InputManager.EndPushPull 에서 호출(F 종료)</summary>
        public void End(ObjectBox target)
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
                        EnterPhase(Phase.PullLoop);
                    }
                    else if (x > 0.1f)
                    {
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
            if (_boxRb == null || _boxCol == null) return;

            // 1) 프레임 이동량 계산 (프로젝트 표준 속도 합성 사용)
            float step = _characterBase.currentMoveStep * _characterBase.GetCurrentMoveSpeed() * _moveSpeed * Time.deltaTime;
            float dx   = x * step;
            if (Mathf.Approximately(dx, 0f)) { DampVelocities(); return; }

            // 2) 박스 이동 시도: 충돌 예측(Cast) → 가능하면 MovePosition
            Vector2 boxPos   = _boxRb.position;
            Vector2 toDir    = new Vector2(Mathf.Sign(dx), 0f);
            float   dist     = Mathf.Abs(dx);

            bool canMoveBox = true;
            int hitCount = _boxRb.Cast(toDir, _castFilter, _castHits, dist);
            for (int i = 0; i < hitCount; i++)
            {
                var h = _castHits[i];
                if (h.collider == null) continue;

                // 자기 자신/플레이어와의 충돌은 무시
                if (h.collider == _boxCol || h.collider == _playerCol) continue;

                // 벽/장애물에 부딪히면 이동 불가
                canMoveBox = false;
                break;
            }

            if (canMoveBox)
            {
                _boxRb.MovePosition(new Vector2(boxPos.x + dx, boxPos.y));
            }
            else
            {
                // 이동 불가 시 박스 속도 감쇠
                _boxRb.linearVelocity = new Vector2(0f, _boxRb.linearVelocity.y);
            }

            // 3) 박스 최신 바운즈 기준으로 플레이어를 모서리에 스냅(항상 붙여 유지)
            SnapPlayerToBoxEdge(_gripSide);

            // 4) 캐릭터의 수직 속도는 보존(점프 방지용이면 0으로)
            _playerRb.linearVelocity = new Vector2(0f, _playerRb.linearVelocity.y);
        }

        private void DampVelocities()
        {
            // GcLogger.Log($"DampVelocities");
            // 박스/플레이어의 수평 속도 제거하여 정지 안정화
            if (_boxRb != null)     _boxRb.linearVelocity     = new Vector2(0f, _boxRb.linearVelocity.y);
            if (_playerRb != null)  _playerRb.linearVelocity  = new Vector2(0f, _playerRb.linearVelocity.y);

            // 플레이어는 모서리에 계속 스냅
            if (_boxCol != null) SnapPlayerToBoxEdge(_gripSide);
        }

        private void Play(string stateName)
        {
            _characterBase.CharacterAnimationController?.PlayCharacterAnimation(stateName);
        }

        private void PlayIfHas(string stateName)
        {
            if (HasAnimation(stateName))
                _characterBase.CharacterAnimationController?.PlayCharacterAnimation(stateName);
        }

        private bool HasAnimation(string stateName)
        {
            if (_characterBase.CharacterAnimationController is { } ctrl)
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
            var ended = _box;

            _phase = Phase.None;
            IsPushing = false;

            // 플레이어 물리 복구
            _playerRb.linearDamping = _origPlayerDrag;
            _playerRb.linearVelocity = _origPlayerVel;
            
            _boxCol.isTrigger = true;

            _characterBase.Stop();

            _box = null;

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
            Bounds boxB = _boxCol.bounds;
            Bounds plyB = _playerCol.bounds;

            float distToLeft  = Mathf.Abs(plyB.center.x - boxB.min.x);
            float distToRight = Mathf.Abs(plyB.center.x - boxB.max.x);
            _gripSide = (distToLeft <= distToRight) ? GripSide.Left : GripSide.Right;

            SnapPlayerToBoxEdge(_gripSide);
        }

        private void SnapPlayerToBoxEdge(GripSide side)
        {
            Bounds boxB = _boxCol.bounds;
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

            Vector3 p = _characterBase.transform.position;
            p.x = targetX;
            _characterBase.transform.position = p;

            // 캐릭터 바라보는 방향(선택)
            // _characterBase.FaceTo(side == GripSide.Left ? +1 : -1); // 프로젝트 기준에 맞게 좌/우 플립
        }
    }
}
