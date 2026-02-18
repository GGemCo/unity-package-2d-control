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


## 1. 새로운 액션 추가(ActionX)

1) Action 클래스 생성(상태/진입/종료/틱)
2) 입력 Handler 연결(버튼/Chord/조건)
3) ScriptableSettings에 튜닝 파라미터 추가
4) AutoMove Suspend 필요 여부 결정 + 토큰 추가
5) 애니메이션 파라미터/상태 전환 확인
6) 테스트
- [ ] 지상/공중/벽/전투 상태 조합
- [ ] 자동이동 On/Off 조합

---

## 2. 입력 동시 판정/대기시간 정책 변경

1) Settings(대기 ms, 허용 버튼 조합) 변경
2) Handler 해석 로직 수정
3) 단위 테스트(가능하면) 또는 디버그 로그 기반 검증
4) 실제 컨트롤 체감 테스트(패드/키보드)

---

## 3. Wall 시스템 변경(Phase 추가/수정)

1) Phase ID(enum) 추가
2) Phase 클래스 추가(Enter/Tick/Exit)
3) 전환 조건(센서/입력/물리) 추가
4) Rigidbody2D 설정 변경 시 원복 경로 보장
5) Debug Gizmo(레이캐스트/감지 영역) 필요 시 추가
6) AutoMove/Jump 시스템과의 인계 지점 확인

---

## 4. Editor 툴(튜닝/테스트) 추가

- EditorWindow 기반으로 “대상 선택 → 파라미터 변경 → 즉시 적용” 흐름 구성
- Undo 지원
- 테스트 시, 플레이어/몬스터 공용으로 작동하도록 추상화(가능하면)
