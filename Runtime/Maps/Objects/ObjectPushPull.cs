using GGemCo2DCore;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GGemCo2DControl
{
    public class ObjectPushPull : DefaultMapObject, IInteraction
    {
        [Header("Interaction")]
        [SerializeField] private int priority = 10; // 사다리보다 낮음
        [SerializeField] private string hint = "F: 밀기/당기기";
        
        [Header("push pull Settings")]
        [Tooltip("0이면 GGemCoPlayerActionSettings의 pushMoveSpeed 사용, 0보다 크면 이 값을 우선 사용")]
        [SerializeField] private float pushMoveSpeed = 0f;
        [Tooltip("0이면 GGemCoPlayerActionSettings의 pullMoveSpeed 사용, 0보다 크면 이 값을 우선 사용")]
        [SerializeField] private float pullMoveSpeed = 0f;

        // 실제로 밀릴 박스의 Rigidbody2D
        private Rigidbody2D _targetBody;
        private TilemapCollider2D _col;
        
        public Rigidbody2D TargetBody => _targetBody;
        public int Priority => priority;

        protected override void Awake()
        {
            base.Awake();
            _col = GetComponent<TilemapCollider2D>();
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