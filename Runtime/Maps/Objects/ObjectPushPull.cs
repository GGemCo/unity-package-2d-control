using GGemCo2DCore;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GGemCo2DControl
{
    public class ObjectPushPull : DefaultMapObject, IInteraction
    {
        [Header("상호작용 (Interaction)")]
        // 숫자가 낮을수록 우선순위 높음 (예: 사다리보다 낮게 설정)
        [SerializeField] private int priority = 10; 
        [SerializeField] private string hint = "F: 밀기/당기기";

        [Header("밀기 / 당기기 속도")]
        [Tooltip("0이면 GGemCoPlayerActionSettings의 pushMoveSpeed 속도 사용\n0보다 크면 이 값을 우선 사용")]
        [SerializeField] private float pushMoveSpeed = 0f;
        [Tooltip("0이면 GGemCoPlayerActionSettings의 pullMoveSpeed 속도 사용\n0보다 크면 이 값을 우선 사용")]
        [SerializeField] private float pullMoveSpeed = 0f;

        [Header("밀기 / 당기기 제한")]
        [Tooltip("체크 해제 시 밀기 불가")]
        [SerializeField] private bool canPush = true;
        [Tooltip("체크 해제 시 당기기 불가")]
        [SerializeField] private bool canPull = true;

        // 실제로 밀릴 박스의 Rigidbody2D
        private Rigidbody2D _targetBody;
        private Collider2D _col;
        
        public Rigidbody2D TargetBody => _targetBody;
        public Collider2D TargetCollider => _col;
        public int Priority => priority;

        protected override void Awake()
        {
            base.Awake();
            _col = GetComponent<Collider2D>();
            _col.isTrigger = true;
            _targetBody = GetComponent<Rigidbody2D>();
            _targetBody.gravityScale = 0f;
            _targetBody.freezeRotation = true;
        }
        public bool IsAvailable(GameObject interactor)
        {
            return _targetBody != null;
        }

        public string GetHint() => hint;
        
        // ==== 기본 접근자 ====
        public float PushMoveSpeed => pushMoveSpeed;
        public float PullMoveSpeed => pullMoveSpeed;
        public bool CanPush => canPush;
        public bool CanPull => canPull;

        public bool BeginInteract(GameObject interactor)
        {
            var inputMgr = interactor.GetComponent<InputManager>();
            if (inputMgr == null) return false;

            return inputMgr.TryBeginPushPull(this);
        }

        public void EndInteract(GameObject interactor)
        {
            var inputMgr = interactor.GetComponent<InputManager>();
            inputMgr?.EndPushPull(this);
        }
    }
}