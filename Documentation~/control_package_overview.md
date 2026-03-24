# Control 패키지 중요한 클래스 정리

업로드된 `control_runtime.zip`, `control_editor.zip` 기준으로 Control 패키지의 핵심 클래스를 Runtime / Editor 관점에서 정리한 문서입니다.

## 1. 패키지 개요

Control 패키지는 **플레이어 조작, 액션 상태 전환, 입력 정책, 상호작용, 모바일 HUD, 옵션 키 바인딩, 씬 초기 부트스트랩**을 담당합니다.

Core 패키지가 캐릭터의 공통 수명주기와 시스템 기반을 제공한다면, Control 패키지는 그 위에서 **실제 플레이어가 무엇을 입력하고 어떤 동작으로 변환되는지**를 책임지는 계층으로 볼 수 있습니다.

핵심 흐름은 다음과 같습니다.

1. `ControlPackageManager`, `GameLoaderManagerControl`, `BootstrapperAction`이 패키지 초기화를 담당합니다.
2. `AddressableLoaderSettingsControl`, `AddressableLoaderInputAction`이 설정 에셋과 Input Action 에셋을 로드합니다.
3. `InputManager`가 Player에 붙어 입력 수신과 액션 분배를 총괄합니다.
4. 각 액션 클래스(`ActionJump`, `ActionDash`, `ActionGuard` 등)가 실제 캐릭터 상태와 애니메이션, 물리 처리를 수행합니다.
5. 모바일 환경에서는 `MobileInputHudService` 계열이 온스크린 HUD를 생성하고 입력과 연결합니다.
6. 옵션 UI에서는 `UIPanelOptionControl`, `UIElementOptionControlChangeKey`가 입력 리바인딩을 처리합니다.

---

## 2. Runtime 핵심 클래스

### 2.1 최상위 진입점 / 패키지 초기화

#### `ControlPackageManager`
- Control 패키지의 런타임 진입점입니다.
- 게임 씬에서 패키지 수명주기를 관리하고, 옵션 패널(`UIPanelOptionControl`) 참조를 보관합니다.
- Control 패키지 전역 접근 지점 역할을 하므로, 씬 부트스트랩 관점에서 가장 먼저 확인해야 하는 클래스입니다.

#### `GameLoaderManagerControl`
- PreIntro 구간에서 Control 패키지 로딩 흐름에 개입하는 매니저입니다.
- 로딩 시작 전에 Control 관련 Addressable/설정 준비가 필요한 경우 이 클래스가 연결 지점이 됩니다.
- Control 패키지가 게임 본편 씬 이전에 무엇을 선로딩해야 하는지 추적할 때 중요합니다.

#### `BootstrapperAction`
- 캐릭터 생성/파괴 이벤트에 맞춰 Player 쪽 Control 컴포넌트 바인딩을 연결하는 부트스트랩 클래스입니다.
- 특히 모바일 HUD 서비스와 플레이어 입력을 연결하는 지점이므로, **HUD 중복 생성 / 플레이어 재스폰 후 입력 미연결** 문제를 볼 때 우선 확인해야 합니다.

---

### 2.2 입력 총괄

#### `InputManager`
- Control 패키지의 가장 중요한 클래스입니다.
- Player 오브젝트에 부착되어 키보드, 패드, 모바일 입력을 받아 실제 액션으로 변환합니다.
- 내부적으로 다음 역할을 한곳에서 통합합니다.
  - `PlayerInput` 액션 캐시
  - 이동 / 공격 / 방어 / 점프 / 대시 / 사다리 / 밀기당기기 / 벽 액션 관리
  - Auto Move 연동
  - 상호작용 입력 처리
  - 입력 버퍼링(press → release 정규화)
  - 피격 시 가드 판정 및 액션 취소 연동
- `FixedUpdate`, `Update`, 각종 `OnXXXPress/Release` 함수가 입력 파이프라인의 중심입니다.
- Control 패키지 분석 시 가장 먼저 읽어야 하는 클래스입니다.

#### `PlayerInputBindings`
- `InputManager`가 실제 `InputAction`을 바인딩/해제할 때 사용하는 전담 클래스입니다.
- 입력 액션 이름과 콜백 연결을 한 군데로 모아, `InputManager`가 지나치게 바인딩 코드에 오염되지 않도록 도와줍니다.
- 입력 액션 추가/삭제, 콜백 연결 구조 변경 시 함께 봐야 합니다.

#### `PlayerInputPolicy`
- 특정 상황에서 어떤 입력이 허용되는지 판단하는 정책 클래스입니다.
- 예를 들어 공격 중 점프 가능 여부, 점프 중 대시 가능 여부 같은 “입력 허용 조건”을 캡슐화합니다.
- `InputManager`의 분기 폭을 줄이는 핵심 보조 객체입니다.

#### 입력 핸들러 계열
- `AttackInputHandler`
- `GuardInputHandler`
- `JumpInputHandler`
- `DashInputHandler`
- `InteractionInputHandler`
- `SimulationToolInputHandler`

이 클래스들은 입력 자체를 직접 실행하기보다, **특정 입력의 해석 규칙**을 분리하는 역할을 합니다.

특징:
- `InputManager`의 비대화를 줄임
- 입력별 예외처리 분리
- 테스트 포인트를 작게 유지

이 패턴은 향후 회피, 락온, 스킬 입력 확장에도 재사용하기 좋은 구조입니다.

#### `BufferedReleaseResolver`
- 버튼 입력을 즉시 press로 처리하지 않고, release 타이밍을 포함한 버퍼 규칙으로 정규화하는 유틸리티입니다.
- 짧은 입력 누락, chord 입력, 모바일/패드의 타이밍 오차를 완화하는 데 중요합니다.
- 입력 씹힘, 짧은 탭이 누락되는 문제를 분석할 때 확인 대상입니다.

#### `AutoMoveAdapter`
- Core 패키지의 Auto Move 시스템과 Control 입력을 연결하는 어댑터입니다.
- Auto Move가 활성화되어 있을 때도 `InputManager`가 실제 이동 실행 주체가 되도록 맞춰 줍니다.
- 벽 액션, 가드, 제어 불가 상태, 공격 범위 진입 등의 이유로 Auto Move를 일시 정지시키는 로직이 모여 있습니다.

#### `UiPointerGuard`
- UI 위에서의 포인터 입력을 게임 조작 입력과 구분하기 위한 보조 클래스입니다.
- 모바일 HUD나 옵션 UI가 있을 때, UI 터치와 실제 이동/공격 입력 충돌을 줄이는 역할을 합니다.

---

### 2.3 플레이어 액션 계층

#### `ActionBase`
- 모든 플레이어 액션의 공통 베이스 클래스입니다.
- `InputManager`, `CharacterBase`, `CharacterBaseController`, 설정(`GGemCoPlayerActionSettings`) 참조를 공유합니다.
- 애니메이션 존재 여부 확인, 공통 초기화/설정 적용 패턴을 묶는 기반 클래스입니다.

#### `ActionMove`
- 가장 기본적인 좌우 이동 액션입니다.
- 구조는 단순하지만, 다른 액션과의 우선순위 충돌을 조율할 때 기준점이 됩니다.
- Auto Move와 직접 맞물리는 실제 이동 구동 계층입니다.

#### `ActionAttack`
- 일반 공격 콤보 실행을 담당합니다.
- 공격 콤보 인덱스 관리, 다음 콤보 전이, 공격 종료 후 후속 상태 전환을 처리합니다.
- `GGemCoAttackComboSettings`와 연결되어 공격 단계별 흐름을 구체화합니다.
- “공격 중 이동”, “콤보 끊김”, “애니메이션 종료 후 상태 복귀” 문제를 볼 때 핵심 클래스입니다.

#### `ActionGuard`
- 방어, 저스트 가드, 방어 중 스태미나 소비/회복 억제를 담당합니다.
- 입력 유지와 해제를 phase 기반으로 다루며, 피격 방향/전방 판정/감산 계산까지 포함합니다.
- `IIncomingHitGuardResolver` 흐름과 연결되어, 피격 시 방어 성공 여부를 판단하는 핵심 클래스입니다.
- Control 패키지에서 전투 규칙에 가장 깊게 관여하는 액션 중 하나입니다.

#### `ActionJump`
- 점프 FSM의 핵심 클래스입니다.
- `StartOneShot → UpLoop → ApexChange → FallLoop → LandOneShot` 구조로 점프 단계를 나눠 관리합니다.
- 점프 높이, 정점 도달 시간, 중력 스케일 조정, 절벽 낙하 감지, 애니메이션 이벤트 워치독까지 포함합니다.
- 최근 프로젝트 흐름상 KnockUp / Crowd Control / Arc 계열과 충돌이 자주 발생하는 지점이므로 매우 중요합니다.

#### `ActionDash`
- 대시 시작 / 진행 / 종료를 phase 기반으로 처리합니다.
- 무중력 대시, 전방 충돌 차단, 대시 중 허용 입력 규칙 등을 포함합니다.
- 점프/벽/스킬과의 상호작용이 많은 액션이라 정책 레벨과 같이 보는 것이 좋습니다.

#### `ActionClimb`
- 사다리, 상하 이동, 진입/종료 애니메이션, 수직 이동 제약을 처리합니다.
- 맵 오브젝트(`ObjectClimb`)와 직접 맞물리므로 레벨 디자인 연동 포인트가 됩니다.
- 이동계 액션 중 맵과 가장 강하게 결합된 클래스입니다.

#### `ActionPushPull`
- 박스 밀기/당기기 상호작용을 담당합니다.
- 플레이어-오브젝트 간 스냅, 그립 방향 판정, 이동 속도, 애니메이션 단계 전환을 모두 관리합니다.
- `ObjectPushPull`과 세트로 보는 것이 좋습니다.

---

### 2.4 벽 액션 계층

#### `ActionWall`
- 벽 매달리기, 벽 슬라이드, 벽 점프를 통합 관리하는 메인 클래스입니다.
- 과거에 분산되어 있던 벽 액션을 phase 구조로 묶은 진입점 역할을 합니다.
- `FixedTick`, `OnJump`, `CancelWall`, `ForceCooldown`이 핵심 API입니다.

#### `ActionWallContext`
- 벽 액션이 공유해야 하는 런타임 상태를 담는 컨텍스트 객체입니다.
- 입력, 물리, 센서, 설정, 현재 phase 공유에 사용됩니다.
- 벽 액션이 여러 phase 클래스로 분리되어 있을 때 결합도를 낮추는 핵심 매개체입니다.

#### `ActionWallSettings`
- 벽 액션에서 사용하는 설정 값을 `GGemCoPlayerActionSettings`로부터 추출/적용하는 역할을 맡습니다.
- 실제 phase 로직이 설정 세부값을 직접 참조하지 않게 도와줍니다.

#### `ActionWallPhysics`
- 벽 액션 중 필요한 물리 계산을 담당하는 보조 클래스입니다.
- 속도 제어, 벽 점프 발사, 슬라이드 처리 등 물리 관련 로직을 집중시킬 때 중요합니다.

#### `ActionWallPhaseSwitch`
- 현재 벽 상태에서 어떤 phase로 전이할지 결정하는 분기 전담 클래스입니다.
- 벽 액션 확장 시 가장 변경 가능성이 높은 지점입니다.

#### Wall Phase 계열
- `IWallPhase`
- `WallPhaseBase`
- `WallPhaseHang`
- `WallPhaseSlide`
- `WallPhaseJump`
- `WallPhaseJumpEnd`
- `WallPhaseSlideEnd`
- `WallPhaseId`

이 계층은 벽 액션을 상태 머신 구조로 분리한 부분입니다.

권장 이해 순서:
1. `ActionWall`
2. `ActionWallContext`
3. `ActionWallPhaseSwitch`
4. 각 `WallPhase*`

#### `WallSensor2D`
- 벽 접촉 판정을 표준화한 센서 클래스입니다.
- Capsule/BoxCast 기반으로 좌우 벽 충돌을 감지하고, 벽 점프 예측 탐지까지 지원합니다.
- 벽 액션 오검출, 반대편 벽 점프 실패, 코너 판정 문제 분석 시 가장 먼저 확인해야 합니다.

---

### 2.5 모바일 입력 / HUD

#### `MobileInputHudService`
- 모바일 HUD의 메인 서비스입니다.
- 플레이어 바인딩, HUD 생성/파괴, 씬 로드/언로드 반응, 설정 변경 반영, 표시 여부 판단을 담당합니다.
- `MobileHudRootView`의 생명주기를 제어하는 실질적 오케스트레이터입니다.
- HUD가 중복 생성되거나, 플레이어 재생성 후 입력이 끊기는 문제에서 핵심 분석 대상입니다.

#### `MobileInputHudPresenter`
- HUD View를 실제로 만들고, 버튼/조이스틱을 설정값에 따라 배치하는 프레젠터입니다.
- `CreateOrInstantiateView`, `ApplyJoystick`, `ApplyButtons`가 핵심입니다.
- Service가 생명주기를 맡고, Presenter가 “화면 구성”을 맡는 구조입니다.

#### `MobileInputBindingResolver`
- 온스크린 버튼과 `InputAction` 바인딩 경로를 연결하는 해석기입니다.
- 이동/버튼 액션이 실제 어떤 control path를 타야 하는지 정리합니다.
- 패드/모바일 공용 입력 체계를 맞출 때 유용합니다.

#### `MobileInputHudVisibilityService`
- 플랫폼 조건, 외부 suppress 사유를 종합해 HUD 표시 여부를 결정합니다.
- Service 본체에서 표시 판단 로직이 비대해지는 것을 막아 줍니다.

#### `MobileInputHudBootstrap`
- 모바일 HUD 서비스 초기 연결용 부트스트랩 성격의 클래스입니다.
- 실제 프로젝트에서 플레이어 등장 시 HUD를 붙이는 흐름과 연결됩니다.

#### `MobileInputHudSafeAreaFitter`
- 노치/세이프 에어리어를 고려해 HUD 루트를 보정합니다.
- 모바일 UI 실사용 품질에 직접 영향을 주는 보조 클래스입니다.

#### `MobileOnScreenUtility`
- 온스크린 입력 관련 공통 유틸리티입니다.
- 작은 보조 기능이지만 모바일 계층에서 공통 의존점으로 사용됩니다.

#### View 계열
- `MobileHudRootView`
- `MobileHudButtonView`
- `MobileHudJoystickView`

역할 구분:
- `MobileHudRootView`: 전체 HUD 루트와 버튼 탐색
- `MobileHudButtonView`: 개별 버튼 표현
- `MobileHudJoystickView`: 가상 조이스틱 입력과 노브 이동

특히 `MobileHudJoystickView`는 `OnPointerDown`, `OnDrag`, `OnPointerUp`을 직접 처리하므로, 조이스틱 감도/중심 복귀/입력 떨림 문제에서 중요합니다.

---

### 2.6 옵션 / 키 바인딩 UI

#### `UIPanelOptionControl`
- Control 옵션 패널의 메인 UI 클래스입니다.
- 현재 컨트롤 스킴 표시, 바인딩 그룹 필터링, UI 생성, 적용/되돌리기/기본값 복원까지 담당합니다.
- 게임 내 “조작 설정” 화면의 진입점으로 이해하면 됩니다.

#### `UIElementOptionControlChangeKey`
- 개별 입력 액션 하나를 리바인딩하는 UI 엘리먼트입니다.
- 현재 바인딩 라벨 표시, 리바인드 시작, 삭제, 기본값 복원, 중복 바인딩 제거를 담당합니다.
- 키 변경 UX 문제는 대부분 이 클래스와 `UIPanelOptionControl`을 함께 보면 됩니다.

---

### 2.7 상호작용 / 맵 오브젝트

#### `InteractionScanner2D`
- 플레이어 근처의 상호작용 가능 오브젝트를 감지하는 스캐너입니다.
- `OnTriggerEnter2D`, `OnTriggerExit2D` 기반으로 현재 상호작용 후보를 관리합니다.
- `InteractionInputHandler`와 연결됩니다.

#### `IInteraction`
- 상호작용 오브젝트 공통 인터페이스입니다.
- 사다리, 밀기/당기기, 기타 오브젝트 상호작용 시스템의 표준 진입점 역할을 합니다.

#### `ObjectClimb`
- 사다리/오르내리기용 맵 오브젝트입니다.
- `ActionClimb`와 함께 읽어야 실제 climb 동작을 이해할 수 있습니다.

#### `ObjectPushPull`
- 밀기/당기기 대상 오브젝트입니다.
- 이동 가능 여부, 속도 등 액션 측 파라미터 공급원 역할을 합니다.

#### `ObjectDamageArea`
- 맵 상의 데미지 영역 표현용 오브젝트입니다.
- 입력/조작 시스템과 직접적 핵심은 아니지만, Control 패키지 안에서 캐릭터 이동과 상호작용하는 맵 오브젝트로 포함됩니다.

---

### 2.8 설정 / 로더

#### `GGemCoPlayerActionSettings`
- 플레이어 액션 전반의 설정 ScriptableObject입니다.
- 점프, 대시, 벽 액션, 가드, 자동 이동, 디버그 Gizmo 등 매우 많은 파라미터를 보관합니다.
- Control 패키지 밸런싱과 동작 미세 조정의 중심입니다.
- `Changed` 이벤트를 통해 플레이 중 설정 변경 즉시 반영도 고려되어 있습니다.

#### `GGemCoMobileHudSettings`
- 모바일 HUD 레이아웃과 표시 관련 설정 ScriptableObject입니다.
- HUD 프리팹, 버튼 배치, 조이스틱 구성 등의 기준 데이터로 쓰입니다.

#### `GGemCoAttackComboSettings`
- 일반 공격 콤보 정의용 설정 자산입니다.
- `ActionAttack`가 콤보 단계 전환을 처리할 때 참조합니다.

#### `AddressableLoaderSettingsControl`
- Control 패키지에서 필요한 설정 ScriptableObject들을 Addressables로 로드합니다.
- 런타임 시작 시 `GGemCoPlayerActionSettings`, `GGemCoMobileHudSettings` 같은 에셋이 정상 로드되지 않는 경우 확인해야 합니다.

#### `AddressableLoaderInputAction`
- Input Action Asset을 Addressables로 로드하는 클래스입니다.
- 입력 에셋 초기화 실패, 컨트롤 스킴 누락, 모바일 HUD 바인딩 실패에서 중요한 분석 지점입니다.

#### `ConfigScriptableObjectControl`
- Control 패키지의 설정 타입 목록과 조회 규칙을 제공하는 정적 설정 매핑 클래스입니다.
- Editor 툴이 어떤 ScriptableObject를 생성해야 하는지도 이 클래스와 연결됩니다.

#### `ConfigAddressableControl`, `ConfigAddressableSettingControl`, `ConfigCommonControl`
- Addressable 키, 경로, 액션 이름 등 Control 패키지 상수를 모아 둔 설정 클래스입니다.
- 하드코딩 문자열을 추적할 때 중요합니다.

---

### 2.9 보조 시스템

#### `StaminaRegenController`
- 방어/전투 상황에 따른 스태미나 회복 규칙을 계산하는 컨트롤러입니다.
- `ActionGuard`와 함께 읽어야 방어 중 스태미나 흐름을 정확히 이해할 수 있습니다.

#### `ActionAttackSMB`
- 공격 애니메이션 상태와 연결되는 StateMachineBehaviour입니다.
- 애니메이션 상태 머신과 액션 코드 연결 지점으로 사용됩니다.

#### `ActionJumpDebugDrawer`, `ActionJumpProbeUtility`
- 점프 디버깅과 probe 계산을 보조합니다.
- 점프 착지/천장/절벽 감지 문제를 시각적으로 확인할 때 유용합니다.

#### `ActionWallDebug`, `ActionWallDebugDrawer`
- 벽 액션 디버그 시각화용 클래스입니다.
- 벽 센서/phase 전환 문제 분석을 돕습니다.

#### `AttackConstants`, `PlayerButtonId`, `PlayerButtonSet`, `ResolvedButtonChord`
- 입력/공격 공통 상수와 데이터 구조입니다.
- 직접 기능을 수행하는 주역은 아니지만, 입력 시스템 확장에서 자주 만납니다.

---

## 3. Editor 핵심 클래스

### 3.1 설정 생성 툴

#### `DefaultSettingsToolControl`
- Control 패키지 기본 설정 생성 툴 창입니다.
- 다른 생성 유틸리티를 묶어 보여주는 Editor 진입점입니다.

#### `SettingGGemCoControl`
- Control 패키지의 기본 ScriptableObject 설정 파일들을 생성하거나 기존 자산을 선택해 주는 툴입니다.
- 프로젝트 초기 세팅 자동화에 유용합니다.

#### `CreateInputAction`
- 기본 `.inputactions` 파일을 자동 생성하는 툴입니다.
- 키보드/게임패드 기본 바인딩과 컨트롤 스킴을 초기 구성하는 역할을 합니다.
- Input System 초기 세팅 자동화에서 중요합니다.

---

### 3.2 Addressables 세팅 툴

#### `AddressableEditorControl`
- Control 패키지 Addressables 설정용 메인 에디터 창입니다.
- ScriptableObject 등록과 Input Action 등록 기능을 한 화면에서 제공합니다.

#### `SettingScriptableObjectControl`
- Control 관련 설정 ScriptableObject들을 Addressables에 등록합니다.
- 로딩 씬에서 선로드해야 할 설정 자산을 Addressables 그룹에 맞게 배치합니다.

#### `SettingInputAction`
- Input Action Asset을 Addressables에 등록합니다.
- 입력 자산 로딩을 런타임 Addressable 체계와 연결하는 실무용 툴입니다.

---

### 3.3 씬 세팅 툴

#### `SceneEditorPreIntroControl`
- PreIntro 씬에 `GameLoaderManagerControl`을 배치하는 설정 툴입니다.
- 로딩 전용 씬 초기 세팅 자동화에 사용됩니다.

#### `SceneEditorGameControl`
- Game 씬에 `ControlPackageManager`를 배치하는 설정 툴입니다.
- Control 패키지가 게임 씬에서 정상 동작하기 위한 최소 구성 자동화 도구입니다.

#### `DefaultSceneEditorControl` 연계
- Control 패키지 Editor 툴 일부는 Core Editor 쪽 `DefaultSceneEditorControl`을 기반으로 동작합니다.
- 즉, Control Editor는 독립 도구라기보다 Core Editor 기반 위에 얹힌 패키지별 확장 구조입니다.

---

## 4. 우선적으로 읽으면 좋은 추천 순서

### Runtime 추천 순서
1. `InputManager`
2. `ActionBase`
3. `ActionJump`
4. `ActionGuard`
5. `ActionDash`
6. `ActionWall` + `WallSensor2D`
7. `ActionClimb`
8. `ActionPushPull`
9. `AutoMoveAdapter`
10. `MobileInputHudService`
11. `UIPanelOptionControl`
12. `GGemCoPlayerActionSettings`

### Editor 추천 순서
1. `DefaultSettingsToolControl`
2. `SettingGGemCoControl`
3. `CreateInputAction`
4. `AddressableEditorControl`
5. `SettingScriptableObjectControl`
6. `SettingInputAction`
7. `SceneEditorGameControl`
8. `SceneEditorPreIntroControl`

---

## 5. Control 패키지 구조를 한 문장으로 요약하면

Control 패키지는 **`InputManager`를 중심으로 여러 액션 클래스와 입력 정책, 모바일 HUD, 키 바인딩 UI를 연결하여 플레이어 조작 전체를 실행 가능한 상태 머신 집합으로 구성한 패키지**입니다.

---

## 6. 프로젝트 소스 문서용 메모

소스 문서로 사용할 때는 다음 기준으로 후속 문서를 분리하면 관리가 쉬워집니다.

- `InputManager` 중심의 **입력 파이프라인 문서**
- `ActionJump`, `ActionDash`, `ActionWall` 중심의 **이동 액션 문서**
- `ActionAttack`, `ActionGuard`, `StaminaRegenController` 중심의 **전투 조작 문서**
- `MobileInputHudService` 중심의 **모바일 HUD 문서**
- `UIPanelOptionControl` 중심의 **옵션/리바인딩 문서**

이렇게 나누면 패키지 구조를 설명하는 개요 문서와, 실제 유지보수용 상세 문서를 분리해서 운영하기 좋습니다.
