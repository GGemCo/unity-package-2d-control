using GGemCo2DCore;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GGemCo2DControl
{
    [RequireComponent(typeof(TilemapCollider2D))]
    public class ObjectClimb : DefaultMapObject, IInteraction
    {
        [Header("Interaction")]
        [SerializeField] private int priority = 100;
        [SerializeField] private string hint = "F: 사다리 오르기/내리기";

        [Header("Exit Snap Offsets (World)")]
        [Tooltip("상단에서 종료할 때 살짝 위로 스냅할 오프셋(+Y)")]
        [SerializeField] private float topExitSnapOffset = 0.2f;
        [Tooltip("하단에서 종료할 때 살짝 아래로 스냅할 오프셋(-Y)")]
        [SerializeField] private float bottomExitSnapOffset = 0.2f;

        [Header("Climb Settings")]
        [Tooltip("0이면 GGemCoPlayerActionSettings의 climbSpeed 사용, 0보다 크면 이 값을 우선 사용")]
        [SerializeField] private float climbSpeed = 0f;

        [Header("Debug & Gizmos")]
        [SerializeField] private bool drawBoundsGizmos = false;
        [SerializeField] private Color boundsColor = new Color(0.1f, 0.8f, 1f, 0.6f);

        // ★ NEW: 오프셋 포함 표시 여부 및 색상
        [SerializeField] private bool includeOffset = false;
        [SerializeField] private Color offsetBoundsColor = new Color(1f, 0.9f, 0.1f, 0.45f);
        [SerializeField] private Color snapLineColor = new Color(1f, 0.4f, 0.1f, 0.9f);

        private TilemapCollider2D _col;

        public int Priority => priority;
        public string GetHint() => hint;

        protected override void Awake()
        {
            base.Awake();
            _col = GetComponent<TilemapCollider2D>();
            _col.isTrigger = true; // 사다리 감지 용도
        }

        // ==== 기본 접근자 ====
        public float GetTopY() => _col.bounds.max.y;
        public float GetBottomY() => _col.bounds.min.y;
        public float TopExitSnapOffset => topExitSnapOffset;
        public float BottomExitSnapOffset => bottomExitSnapOffset;
        public float ClimbSpeed => climbSpeed;

        public bool IsAvailable(GameObject interactor) => true;

        public bool BeginInteract(GameObject interactor)
        {
            var inputMgr = interactor.GetComponent<InputManager>();
            return inputMgr != null && inputMgr.TryBeginLadder(this);
        }

        public void EndInteract(GameObject interactor)
        {
            var inputMgr = interactor.GetComponent<InputManager>();
            inputMgr?.EndLadder(this);
        }

        // ==== 월드 좌표/경계 값 ====
        public Bounds WorldBounds => _col.bounds;
        public Vector3 WorldCenter => _col.bounds.center;
        public Vector3 WorldSize => _col.bounds.size;
        public float WorldTopY => _col.bounds.max.y;
        public float WorldBottomY => _col.bounds.min.y;
        public float WorldLeftX => _col.bounds.min.x;
        public float WorldRightX => _col.bounds.max.x;
        public Vector3 WorldTopSnapPos => new Vector3(WorldCenter.x, WorldTopY + topExitSnapOffset, WorldCenter.z);
        public Vector3 WorldBottomSnapPos => new Vector3(WorldCenter.x, WorldBottomY - bottomExitSnapOffset, WorldCenter.z);
        public float WorldCenterX => WorldCenter.x;

        // ==== ActionClimb 연동 유틸 ====
        public bool TryGetAreaCollider(out Collider2D areaCol)
        {
            areaCol = _col;
            return areaCol != null;
        }

        public void GetHorizontalBounds(out float leftX, out float rightX)
        {
            var b = _col.bounds;
            leftX = (float)b.min.x;
            rightX = (float)b.max.x;
        }

        public void GetVerticalBounds(out float bottomY, out float topY)
        {
            var b = _col.bounds;
            bottomY = (float)b.min.y;
            topY = (float)b.max.y;
        }

        public bool ContainsX(float x, float margin = 0f)
        {
            var b = _col.bounds;
            return x >= b.min.x - margin && x <= b.max.x + margin;
        }

        public bool Contains(Vector3 worldPoint, float margin = 0f)
        {
            var b = _col.bounds;
            b.Expand(new Vector3(margin * 2f, margin * 2f, 0f));
            return b.Contains(worldPoint);
        }

        public float ClampX(float x)
        {
            var b = _col.bounds;
            return Mathf.Clamp(x, (float)b.min.x, (float)b.max.x);
        }

        public Vector3 ClampPosition(Vector3 worldPos)
        {
            var b = _col.bounds;
            float clampedX = Mathf.Clamp(worldPos.x, (float)b.min.x, (float)b.max.x);
            float clampedY = Mathf.Clamp(worldPos.y, (float)b.min.y, (float)b.max.y);
            return new Vector3(clampedX, clampedY, worldPos.z);
        }

        public Vector3 SnapToCenterX(Vector3 worldPos)
        {
            return new Vector3(WorldCenterX, worldPos.y, worldPos.z);
        }

        // ==== Gizmos ====
        private void OnDrawGizmosSelected()
        {
            if (!drawBoundsGizmos) return;

            var col = GetComponent<TilemapCollider2D>();
            var b = col != null ? col.bounds : new Bounds(transform.position, Vector3.zero);

            // 기본 바운즈
            Gizmos.color = boundsColor;
            Gizmos.DrawWireCube(b.center, b.size);

            if (!includeOffset) return;

            // 오프셋(top/bottom) 포함 바운즈 계산
            //  - 상단은 +topExitSnapOffset 만큼 아래로,
            //  - 하단은 +bottomExitSnapOffset 만큼 위로로 확장
            float extraUp = topExitSnapOffset;
            float extraDown = bottomExitSnapOffset;

            // 새 중심/크기 계산 (Bounds는 min/max 직접 설정 불가)
            var expandedSize = new Vector3(
                b.size.x,
                b.size.y - extraUp - extraDown,
                b.size.z
            );
            var expandedCenter = new Vector3(
                b.center.x,
                b.center.y - (extraUp - extraDown) * 0.5f,
                b.center.z
            );

            // 오프셋 포함 바운즈
            Gizmos.color = offsetBoundsColor;
            Gizmos.DrawWireCube(expandedCenter, expandedSize);

            // 스냅 라인(Top/Bottom)
            float left = b.min.x;
            float right = b.max.x;

            Vector3 topSnapA = new Vector3(left,  b.max.y - extraUp, b.center.z);
            Vector3 topSnapB = new Vector3(right, b.max.y - extraUp, b.center.z);
            Vector3 botSnapA = new Vector3(left,  b.min.y + extraDown, b.center.z);
            Vector3 botSnapB = new Vector3(right, b.min.y + extraDown, b.center.z);

            Gizmos.color = snapLineColor;
            Gizmos.DrawLine(topSnapA, topSnapB);
            Gizmos.DrawLine(botSnapA, botSnapB);

            // 스냅 포인트를 눈에 띄게 표시(작은 구)
            Gizmos.DrawSphere(new Vector3(b.center.x, b.max.y - extraUp, b.center.z), Mathf.Min(b.size.x, b.size.y) * 0.2f);
            Gizmos.DrawSphere(new Vector3(b.center.x, b.min.y + extraDown, b.center.z), Mathf.Min(b.size.x, b.size.y) * 0.2f);
        }
    }
}
