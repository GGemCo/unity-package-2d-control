# Control 문서

이 폴더는 **Control 패키지**의 구조/규칙/변경 절차를 표준화하기 위한 문서입니다.

- Runtime 네임스페이스: `GGemCo2DControl`
- Editor 네임스페이스: `GGemCo2DControlEditor`

Unity 공식 문서 참고 링크:
- Assembly Definition(런타임/에디터 분리): https://docs.unity3d.com/6000.3/Documentation/Manual/cus-asmdef.html
- ScriptableObject(데이터 컨테이너/저장 특성): https://docs.unity3d.com/6000.3/Documentation/Manual/class-ScriptableObject.html
- EditorWindow(커스텀 툴): https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorWindow.html
- EditorWindow(UI Toolkit 가이드): https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateEditorWindow.html
- Addressables(패키지): https://docs.unity3d.com/Packages/com.unity.addressables%40latest/
- Addressables(개요): https://docs.unity3d.com/Packages/com.unity.addressables%401.24/manual/AddressableAssetsOverview.html
- Undo(에디터 Undo/Redo): https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Undo.html
- Serialization(직렬화 규칙): https://docs.unity3d.com/Manual/script-Serialization.html


## 1. 역할

Control은 “캐릭터 조작/액션” 계층입니다.

- 입력(Input) → 해석(Handlers) → 액션(Action) 실행
- 이동/점프/대시/가드/벽 액션 등 캐릭터 모션 제어
- 자동이동(AutoMove) 연동 및 전투/비전투 상태에 따른 입력 정책

Core의 Character/Stat/Animator와 결합하지만, **게임 규칙(스킬/어펙트)** 자체는 Control에 두지 않습니다.

## 2. 구성 개요

- `Core/` : 입력 처리 파이프라인, 어댑터, 공통 유틸
- `Action/` : Action* (Guard, Jump, Wall 등) 액션 단위 구현
- `Wall/` : 벽 타기/미끄러짐/점프 등 Phase 기반 상태 머신
- `Interaction/` : 상호작용 입력 및 대상 처리
- `Config/`, `ScriptableSettings/` : 조작/튜닝 파라미터(SO)
- `UI/` : 조작 관련 UI(필요 시)
- `Maps/` : 맵/영역 연동(예: Patrol 영역 등)

Editor(ControlEditor)는 주로 테스트/튜닝 툴을 제공합니다.

## 3. 입력 처리 표준

- 입력 이벤트는 “해석(Chord/Sequence)”과 “실행(Action)”을 분리합니다.
- 동시 입력/대기시간 같은 정책은 ScriptableSettings에서 튜닝 가능해야 합니다.
- 자동이동/벽/가드 등 여러 시스템이 “Suspend Token”을 공유할 때,
  - 토큰 충돌/해제 누락이 발생하지 않도록 토큰을 세분화하고 우선순위를 문서화합니다.

## 4. 확장 포인트(권장)

- 새로운 액션 추가: `IAction`(또는 기존 패턴) + Handler 연결
- 이동 관련 확장: MotionController(또는 Adapter) 계층에 위임하여 Action이 비대해지는 것을 방지
- Phase 기반 액션(예: Wall): Phase 클래스 분할을 유지하여 파일당 책임을 제한
