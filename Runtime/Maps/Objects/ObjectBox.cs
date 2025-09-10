using GGemCo2DCore;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GGemCo2DControl
{
    public class ObjectBox : DefaultMapObject, IInteraction
    {
        [SerializeField] private int priority = 10; // 사다리보다 낮음
        [SerializeField] private string hint = "F: 밀기/당기기";

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