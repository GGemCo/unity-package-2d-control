using GGemCo2DCore;

namespace GGemCo2DControl
{
    /// <summary>
    /// CutsceneManager의 컷신 세션 이벤트를 모바일 입력 HUD의 표시 억제 사유로 변환합니다.
    /// Core 패키지는 모바일 HUD를 직접 알지 않고, Control 패키지가 컷신 상태를 구독해 조이스틱 표시를 제어합니다.
    /// </summary>
    internal sealed class MobileInputHudCutsceneSuppressor
    {
        /// <summary>
        /// 컷신 때문에 모바일 HUD를 숨길 때 사용하는 reason 키입니다.
        /// 다른 숨김 사유와 충돌하지 않도록 한 곳에서만 정의합니다.
        /// </summary>
        private const string ReasonCutscene = "Cutscene";

        private readonly MobileInputHudService _hudService;
        private CutsceneManager _cutsceneManager;

        /// <summary>
        /// 모바일 HUD 서비스와 연결된 컷신 억제 브리지를 생성합니다.
        /// </summary>
        /// <param name="hudService">가시성 억제 사유를 적용할 모바일 HUD 서비스입니다.</param>
        public MobileInputHudCutsceneSuppressor(MobileInputHudService hudService)
        {
            _hudService = hudService;
        }

        /// <summary>
        /// 구독할 CutsceneManager를 교체합니다.
        /// 기존 매니저의 이벤트는 해제하고 새 매니저의 현재 세션 상태를 즉시 HUD 가시성에 반영합니다.
        /// </summary>
        /// <param name="cutsceneManager">구독할 컷신 매니저입니다. null이면 컷신 억제 상태를 해제합니다.</param>
        public void Bind(CutsceneManager cutsceneManager)
        {
            if (_cutsceneManager == cutsceneManager)
            {
                SyncCurrentState();
                return;
            }

            UnsubscribeCurrentManager();
            _cutsceneManager = cutsceneManager;

            if (_cutsceneManager != null)
            {
                _cutsceneManager.CutsceneStarted += OnCutsceneStarted;
                _cutsceneManager.CutsceneEnded += OnCutsceneEnded;
            }

            SyncCurrentState();
        }

        /// <summary>
        /// 현재 구독을 모두 해제하고 컷신 억제 사유를 제거합니다.
        /// 서비스가 파괴되거나 씬 연결이 끊길 때 HUD가 숨김 상태로 남지 않도록 사용합니다.
        /// </summary>
        public void Dispose()
        {
            UnsubscribeCurrentManager();
            SetCutsceneSuppressed(false);
        }

        /// <summary>
        /// 현재 컷신 매니저의 세션 활성 상태를 모바일 HUD 억제 상태에 반영합니다.
        /// 이미 컷신이 시작된 뒤 서비스가 생성되는 경우에도 조이스틱이 즉시 숨겨지도록 합니다.
        /// </summary>
        private void SyncCurrentState()
        {
            SetCutsceneSuppressed(_cutsceneManager != null && _cutsceneManager.IsSessionActive());
        }

        /// <summary>
        /// 기존 컷신 매니저 이벤트 구독을 해제합니다.
        /// </summary>
        private void UnsubscribeCurrentManager()
        {
            if (_cutsceneManager == null)
            {
                return;
            }

            _cutsceneManager.CutsceneStarted -= OnCutsceneStarted;
            _cutsceneManager.CutsceneEnded -= OnCutsceneEnded;
            _cutsceneManager = null;
        }

        /// <summary>
        /// 컷신 세션 시작 시 모바일 HUD를 숨김 사유에 추가합니다.
        /// </summary>
        private void OnCutsceneStarted()
        {
            SetCutsceneSuppressed(true);
        }

        /// <summary>
        /// 컷신 세션 종료 시 모바일 HUD의 컷신 숨김 사유를 제거합니다.
        /// </summary>
        private void OnCutsceneEnded()
        {
            SetCutsceneSuppressed(false);
        }

        /// <summary>
        /// 모바일 HUD 서비스에 컷신 숨김 사유를 적용하거나 해제합니다.
        /// </summary>
        /// <param name="suppressed">컷신 때문에 HUD를 숨기려면 true입니다.</param>
        private void SetCutsceneSuppressed(bool suppressed)
        {
            _hudService?.SetSuppressed(ReasonCutscene, suppressed);
        }
    }
}
