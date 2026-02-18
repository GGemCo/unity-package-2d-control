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


## 1. 액션 설계 규칙

- Action은 “상태 + 실행”을 갖되, 과도한 책임을 갖지 않도록 분리합니다.
- Phase 기반 액션은 파일 분할을 우선합니다.
  - 예: `WallPhaseHang`, `WallPhaseSlide`, `WallPhaseJump` …

## 2. 입력 핸들러 규칙

- Handler는 입력을 “해석”하고, 실제 동작은 Action/Controller로 위임합니다.
- Handler 내부에서 Scene 오브젝트 탐색/무거운 로직을 금지합니다.
- Update 루프에서 LINQ/할당 최소화(특히 입력 폴링 구간).

## 3. 자동이동(AutoMove) 연동 규칙

- Suspend/Resume는 중앙집중 Adapter에서 수행합니다.
- Suspend Token은 기능별로 분리(Guard/Wall/Attack/Interact 등)합니다.
- 우선순위(예: Wall > Guard > Attack)를 문서화하고 코드로 표현합니다.

## 4. 설정(ScriptableObject) 규칙

- 조작 튜닝 값은 ScriptableSettings에 모읍니다.
- “상태 전환 임계값”은 하드코딩하지 않습니다(예: 80ms 대기시간).

## 5. 디버그/가시화

- 입력/액션 전환 로그는 Debug 플래그로 제어합니다.
- 필요 시 Gizmo/DebugDraw를 제공하되, 기본은 비활성입니다.
