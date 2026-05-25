using GGemCo2DCore;

namespace GGemCo2DControl
{
    /// <summary>
    /// InteractionManager의 NPC 인터랙션 상태를 모바일 입력 HUD의 표시 억제 사유로 변환합니다.
    /// 대화 중에는 조이스틱과 액션 버튼 터치를 막고, 대화 UI 터치만 남기기 위해 사용합니다.
    /// </summary>
    internal sealed class MobileInputHudInteractionSuppressor
    {
        /// <summary>
        /// NPC 인터랙션 때문에 모바일 HUD를 숨길 때 사용하는 reason 키입니다.
        /// 다른 숨김 사유와 독립적으로 관리하기 위해 고정 문자열로 분리합니다.
        /// </summary>
        private const string ReasonInteraction = "NpcInteraction";

        private readonly MobileInputHudService _hudService;
        private InteractionManager _interactionManager;

        /// <summary>
        /// 모바일 HUD 서비스와 연결된 NPC 인터랙션 억제 브리지를 생성합니다.
        /// </summary>
        /// <param name="hudService">가시성 억제 사유를 적용할 모바일 HUD 서비스입니다.</param>
        public MobileInputHudInteractionSuppressor(MobileInputHudService hudService)
        {
            _hudService = hudService;
        }

        /// <summary>
        /// 구독할 InteractionManager를 교체합니다.
        /// 기존 매니저의 이벤트는 해제하고 새 매니저의 현재 인터랙션 상태를 즉시 HUD 가시성에 반영합니다.
        /// </summary>
        /// <param name="interactionManager">구독할 인터랙션 매니저입니다. null이면 인터랙션 억제 상태를 해제합니다.</param>
        public void Bind(InteractionManager interactionManager)
        {
            if (_interactionManager == interactionManager)
            {
                SyncCurrentState();
                return;
            }

            UnsubscribeCurrentManager();
            _interactionManager = interactionManager;

            if (_interactionManager != null)
            {
                _interactionManager.InteractionActiveChanged += OnInteractionActiveChanged;
            }

            SyncCurrentState();
        }

        /// <summary>
        /// 현재 구독을 모두 해제하고 NPC 인터랙션 억제 사유를 제거합니다.
        /// 서비스가 파괴되거나 씬 연결이 끊길 때 HUD가 숨김 상태로 남지 않도록 사용합니다.
        /// </summary>
        public void Dispose()
        {
            UnsubscribeCurrentManager();
            SetInteractionSuppressed(false);
        }

        /// <summary>
        /// 현재 인터랙션 매니저의 활성 상태를 모바일 HUD 억제 상태에 반영합니다.
        /// 이미 대화가 열린 뒤 서비스가 생성되는 경우에도 조이스틱이 즉시 숨겨지도록 합니다.
        /// </summary>
        private void SyncCurrentState()
        {
            SetInteractionSuppressed(_interactionManager != null && _interactionManager.IsInteractioning());
        }

        /// <summary>
        /// 기존 인터랙션 매니저 이벤트 구독을 해제합니다.
        /// </summary>
        private void UnsubscribeCurrentManager()
        {
            if (_interactionManager == null)
            {
                return;
            }

            _interactionManager.InteractionActiveChanged -= OnInteractionActiveChanged;
            _interactionManager = null;
        }

        /// <summary>
        /// NPC 인터랙션 활성 상태가 바뀌면 모바일 HUD 억제 상태를 갱신합니다.
        /// </summary>
        /// <param name="active">NPC 인터랙션이 진행 중이면 true입니다.</param>
        private void OnInteractionActiveChanged(bool active)
        {
            SetInteractionSuppressed(active);
        }

        /// <summary>
        /// 모바일 HUD 서비스에 NPC 인터랙션 숨김 사유를 적용하거나 해제합니다.
        /// </summary>
        /// <param name="suppressed">NPC 인터랙션 때문에 HUD를 숨기려면 true입니다.</param>
        private void SetInteractionSuppressed(bool suppressed)
        {
            _hudService?.SetSuppressed(ReasonInteraction, suppressed);
        }
    }
}
