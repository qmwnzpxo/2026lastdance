# 카메라 구조 정리 계획

작성일: 2026-11-28
대상 파일: CSH/0.Scripts/Camera/CameraFollow.cs (+ 이 파일을 쓰는 스크립트들)
상태: 계획만 정리함. 아직 코드는 바꾸지 않음.

이 문서를 읽어야 하는 경우
  - 카메라 구조를 크게 바꿀 때 (CameraFollow.cs, 가상 카메라·브레인 설정)
  - 카메라 연출을 새로 추가할 때 (줌, 전환, 흔들림, 컷씬·대화 카메라 등)
  - 카메라를 직접 건드리는 코드를 추가·수정할 때
  작업으로 구조가 바뀌면 "1. 현재 구조" 와 해당 단계 내용을 같이 갱신할 것.


------------------------------------------------------------
## 0. 한 줄 요약
------------------------------------------------------------

지금은 CameraFollow가 카메라 전환(위치·줌)을 직접 보간하고,
시네머신 브레인의 블렌드를 껐다 켰다 하면서 컷 전환을 만든다.
이걸 "카메라는 켜고 끄기만 하고, 전환은 브레인 블렌드에 맡기는" 구조로 바꾼다.


------------------------------------------------------------
## 1. 현재 구조
------------------------------------------------------------

씬 구성 (Chapter_1, Tutorial 등 공통)

  Main Camera
    - CinemachineBrain
        Update Method  : Smart Update
        Default Blend  : Custom 커브, 1초, 커브 값 0 -> 0
                         (1초 동안 안 움직이다가 끝에 한 번에 튐)
    - URP Pixel Perfect Camera (PPU 64, 1920x1080)

  CM_GamePlay (우선순위 10)
    - CinemachineFollow (플레이어 추적, 댐핑 1)
    - CinemachinePixelPerfect  <- 이쪽에만 있음

  CM_Zoom (우선순위 11, 평소에는 꺼져 있음)
    - CinemachineFollow
    - Pixel Perfect 확장 없음

CameraFollow.cs 가 하는 일

  1) 씬 시작 인트로 줌
     CM_Zoom 을 켜고 렌즈 크기를 async 루프로 직접 키운 뒤,
     블렌드 시간을 0으로 바꿔 CM_GamePlay 로 강제 컷.

  2) 락다운 진입/해제
     "LockdownCameraPivot" 이라는 빈 오브젝트를 만들어 CM_Zoom 이 따라가게 하고,
     피벗 위치와 렌즈 크기를 같은 SmoothStep 곡선으로 직접 보간.
     해제 끝에 ForceCameraPosition + 블렌드 시간 0 으로 CM_GamePlay 로 강제 컷.

  3) 픽셀 퍼펙트 보정 맞추기
     CM_Zoom 에는 보정이 없어서 CM_GamePlay 의 보정값을 손으로 계산해 맞춤.
     (GameplayOutputOrtho, SyncGameplayLensToOutput)

  4) 댐핑 변경 (대시 중 카메라 반응 속도)
     SetCameraDumping(scale) 로 CM_GamePlay 의 PositionDamping 만 바꿈.
     scale 은 인스펙터 댐핑에 곱하는 배율 (대시 중 0.2, 1 = 인스펙터 값으로 복구).
     CM_Zoom 댐핑은 CameraFollow 가 인트로·락다운에서 직접 관리 (SetZoomDamping / RestoreZoomDamping).
     (2026-11-28 수정: 예전에는 두 카메라를 모두 바꾸고 고정값 1로 복구해서,
      락다운 중 대시하면 CM_Zoom 댐핑이 1이 되어 복귀 끝 컷에서 위치가 튈 수 있었음)
     핑크 대시는 끝난 뒤 HoldThenRestoreDamping(0.2) 로
     0.1초 유지 -> 0.2초 동안 1로 부드럽게 복구 (인스펙터 dashDampingHold / dashDampingRecover).
     다른 칩 대시가 시작돼도 이 복구는 취소하지 않음 (의도적으로 제외).
     연속 핑크 대시는 복구를 처음부터 다시 시작.

  5) 카메라 흔들림 (Randeur02 님이 추가, 보스 패턴에서 사용)
     두 카메라의 FollowOffset 을 매 프레임 랜덤으로 흔들고 되돌림.

  6) 순간이동 후 스냅
     SnapToTarget / SnapMainToTarget 으로 PreviousStateIsValid = false.

CameraFollow 를 호출하는 곳

  - LockdownRoom.cs          : StartLockdown, EndLockdown (레드대쉬 되감기 포함)
  - PlayerDash.cs, BlueDash.cs, GreenDash.cs : SetCameraDumping
  - BossAttackPatterns.cs (LHM) : ShakeCamera
  - SectionSave.cs, DialogueManager.cs (LHM), DialogueTrigger.cs,
    AutoDialogueTrigger.cs, NpcDialogueTrigger.cs (LJS) : SnapMainToTarget
  - BossTestSceneBuilder.cs (LHM, 에디터 툴) : CM_GamePlay / CM_Zoom 구조를 그대로 생성

카메라를 CameraFollow 밖에서 건드리는 곳

  - DialogueManager.cs (LHM)
    대화 시작: CM_GamePlay 오브젝트 끄기 + CinemachineBrain 컴포넌트 끄기
               + 메인 카메라를 대화 위치로 직접 이동
    대화 종료: 메인 카메라를 플레이어 위치로 이동 + 브레인 켜기
               + CM_GamePlay 켜기 + 스냅


------------------------------------------------------------
## 2. 지금 구조에서 생긴 문제들 (이번에 고친 것 포함)
------------------------------------------------------------

  1) 락다운 복귀 끝에서 줌이 튐
     원인: CM_GamePlay 는 픽셀 퍼펙트로 보정된 크기(약 8.4375)가 나가는데,
           복귀 목표를 보정 전 렌즈 값(10)으로 잡았음.
     조치: 보정값 기준으로 복귀하도록 수정함.

  2) 복귀 끝 한 프레임 동안 락다운 화면처럼 보임 (가만히 있을 때 잘 보임)
     원인: Smart Update 브레인은 컷 프레임에 CM_GamePlay 를 새로 계산하지 않고
           대기 중에 계산해 둔 상태를 그대로 출력. 대기 중에는 픽셀 퍼펙트
           보정이 적용되지 않아 보정 전 크기(10)가 한 프레임 나감.
     조치: 락다운 진입/인트로 시작 때 CM_GamePlay 렌즈 원본 값을 보정값으로 맞춰 둠.

  3) 브레인 기본 블렌드가 "1초 멈췄다 튀는" 0 -> 0 커브
     그래서 블렌드를 못 쓰고 DefaultBlend.Time 을 0으로 바꿨다가
     한 프레임 뒤 되돌리는 방식을 쓰고 있음. 타이밍이 조금만 어긋나도 깨짐.

  4) 대화 연출이 브레인 자체를 끔
     락다운 중이거나 복귀 중에 대화가 끼면 CameraFollow 의 처리와 충돌할 수 있음.
     (이번 튜토리얼 문제의 원인은 아니었음)

  5) 흔들림이 FollowOffset 을 직접 바꿈
     락다운 카메라 위치 계산도 FollowOffset 을 쓰기 때문에,
     락다운 중 흔들림이 겹치면 위치가 어긋날 수 있음. (지금은 보스전에서만 써서 문제 없음)

공통 원인
  우리 코드가 직접 제어하는 것과 브레인이 알아서 하는 것이 서로 엇갈림.
  버그를 고칠 때마다 보정 코드가 하나씩 늘어나는 구조.


------------------------------------------------------------
## 3. 목표 구조
------------------------------------------------------------

원칙
  - 가상 카메라는 "켜고 끄기(또는 우선순위 변경)"만 한다.
  - 위치·줌 전환은 브레인 블렌드가 처리한다.
  - 모든 가상 카메라의 출력 기준(픽셀 퍼펙트 등)을 통일한다.
  - 브레인 컴포넌트는 끄지 않는다.

목표 구성

  Main Camera
    - CinemachineBrain
        Default Blend : EaseInOut 1초 (일반 커브)
        Custom Blends : 카메라 쌍별 블렌드 에셋 (필요할 때만)

  CM_GamePlay  (우선순위 10)  플레이어 추적
  CM_Lockdown  (우선순위 20)  락다운 방 중심 추적, 렌즈 = 방 크기
  CM_Intro     (우선순위 30)  씬 시작 인트로 (또는 CM_Zoom 재사용)
  CM_Dialogue  (우선순위 40)  대화 위치 고정

  흔들림: CinemachineImpulseSource (보내는 쪽) + CinemachineImpulseListener (각 가상 카메라)


------------------------------------------------------------
## 4. 먼저 정해야 할 것 (작업 전 결정 필요)
------------------------------------------------------------

  [결정 1] 픽셀 퍼펙트와 부드러운 줌 중 무엇을 우선할지
    픽셀 퍼펙트는 크기를 정해진 단계(8.4375, 4.21875 ...)로만 바꿀 수 있어서
    부드러운 줌과 원래 충돌한다.

    A. 모든 가상 카메라에 CinemachinePixelPerfect 를 붙인다
       - 출력 기준이 통일되어 지금 같은 튐 문제가 원천적으로 없어짐
       - 대신 락다운/인트로 줌이 계단식으로 끊겨 보임
    B. 줌을 쓰는 카메라에는 붙이지 않고, CM_GamePlay 렌즈 값을 보정값으로 고정한다
       - 부드러운 줌 유지
       - CM_GamePlay 렌즈 값을 처음부터 보정값(예: 8.4375)으로 넣어 두면
         지금의 수동 보정 코드는 대부분 필요 없어짐
       - 해상도가 바뀌면 보정값도 바뀌므로 그때만 다시 맞추면 됨
    C. 픽셀 퍼펙트를 끈다 (도트 그래픽이 아니라면 고려)

    추천: B (지금 연출을 유지하면서 가장 단순)

  [결정 2] 락다운 카메라를 방마다 둘지, 하나를 공용으로 쓸지
    - 방마다: 방 프리팹에 CM_Lockdown 을 넣고 켜고 끄기만 하면 됨. 설정이 눈에 보임.
    - 공용 하나: 진입 때 Follow 대상과 렌즈만 바꿔서 켬. 씬 구성이 단순.
    추천: 공용 하나 (지금 LockdownRoom 이 렌즈 크기를 계산하므로 그대로 넘기면 됨)

  [결정 3] 다른 담당자 코드 수정 여부
    - DialogueManager.cs (LHM 폴더)
    - BossAttackPatterns.cs, BossTestSceneBuilder.cs (LHM 폴더)
    - DialogueTrigger 등 (LJS 폴더)
    담당자와 먼저 이야기하고 진행할 것.


------------------------------------------------------------
## 5. 단계별 작업
------------------------------------------------------------

각 단계는 따로 커밋하고, 단계마다 "확인" 항목을 플레이로 점검한다.
앞 단계가 끝나야 다음 단계로 간다.


### 1단계. 브레인 기본 블렌드 정리  (작고 위험 낮음)

  작업
    - Main Camera 의 CinemachineBrain > Default Blend 를
      Custom(0 -> 0) 에서 EaseInOut 으로 변경 (시간은 1초 그대로)
    - 모든 챕터 씬(Tutorial, Chapter_1, Chapter_1_Boss, Basecamp)에서 같은 값인지 확인

  이유
    - 0 -> 0 커브는 의도한 설정이 아닐 가능성이 큼
    - 지금 코드는 전환 때 블렌드 시간을 0으로 바꿔 쓰므로,
      이 단계만으로는 락다운/인트로 동작이 바뀌지 않음
    - 대화 종료처럼 CameraFollow 를 거치지 않는 전환이 부드러워짐

  확인
    - 락다운 진입/해제, 인트로 줌이 이전과 똑같이 동작하는지
    - 대화 종료 후 카메라 복귀가 어색하지 않은지


### 2단계. 픽셀 퍼펙트 기준 통일  ([결정 1] 에 따라)

  B 안 기준 작업
    - CM_GamePlay 렌즈 Orthographic Size 를 보정값으로 직접 입력
      (1920x1080 기준 8.4375)
    - CameraFollow 의 GameplayOutputOrtho / SyncGameplayLensToOutput 은
      해상도 변경 대응용으로만 남기거나 제거

  확인
    - 플레이 시작 직후, 락다운 전후, 인트로 전후 화면 크기가 모두 같은지
    - 창 크기를 바꿨을 때 크기가 크게 튀지 않는지


### 3단계. 락다운을 블렌드 방식으로 변경  (가장 큰 작업)

  작업
    - 씬에 CM_Lockdown 가상 카메라 추가 (우선순위 20, 평소 꺼짐)
      Pixel Perfect 확장은 [결정 1] 에 맞춤
    - CameraFollow.StartLockdown(roomCenter, ortho, duration)
        -> CM_Lockdown.Follow = roomCenter
        -> CM_Lockdown 렌즈 크기 = ortho
        -> CM_Lockdown 켜기
    - CameraFollow.EndLockdown(duration)
        -> CM_Lockdown 끄기
    - 전환 시간: Custom Blends 에셋에
        CM_GamePlay -> CM_Lockdown : EaseInOut, 1초
        CM_Lockdown -> CM_GamePlay : EaseInOut, 1초
      (방마다 시간이 달라야 하면 진입/해제 직전에 브레인 DefaultBlend 시간을 바꾸는 방식 검토)
    - 아래 코드 제거
        LockdownEnterAsync, LockdownExitAsync, AnimateLockdownAsync,
        LockdownPivot, CurrentZoomPivotPosition, GameplayEquivalentPivotPosition,
        _lockdownExiting / resuming 처리
    - LockdownRoom.cs 는 호출부(StartLockdown / EndLockdown)를 그대로 두면 됨
      레드대쉬 되감기(RestoreSnapshot)도 EndLockdown 만 부르므로 그대로 동작해야 함

  확인
    - 락다운 진입/해제가 끊김 없이 부드러운지 (가만히 있을 때 / 움직일 때 둘 다)
    - 해제 연출 도중 다른 락다운에 바로 들어갈 때 (튜토리얼 1번째 -> 2번째 방)
    - 빨강 칩 녹화/재생 중 락다운 되감기
    - 락다운 중 대화가 시작될 때


### 4단계. 인트로 줌 정리

  작업
    - 인트로도 같은 원칙으로: CM_Intro 를 켜 둔 상태로 시작하고,
      렌즈를 fromOrtho -> toOrtho 로 키운 뒤 끄면 블렌드로 CM_GamePlay 복귀
      (라이브 중인 카메라의 렌즈를 바꾸는 건 문제 없음)
    - CutToGameplayAsync, DefaultBlend.Time 을 0으로 바꾸는 코드 제거
    - 필요하면 Timeline 의 Cinemachine Track 으로 대체 검토

  확인
    - 씬 시작 인트로가 이전과 같은 느낌인지
    - 인트로 끝에서 튀지 않는지


### 5단계. 흔들림을 Impulse 로 변경  (담당자와 협의 후)

  작업
    - 각 가상 카메라에 CinemachineImpulseListener 추가
    - CameraFollow.ShakeCamera(duration, magnitude) 는 함수 이름을 그대로 두고
      내부만 ImpulseSource.GenerateImpulse 로 바꿈
      -> BossAttackPatterns.cs 는 수정하지 않아도 됨
    - FollowOffset 을 직접 바꾸는 ShakeAsync 제거

  확인
    - 보스 착지 패턴 흔들림 세기·시간이 비슷한지


### 6단계. 대화 카메라 정리  (담당자와 협의 후)

  작업
    - CM_Dialogue 가상 카메라 추가 (우선순위 40, 평소 꺼짐)
    - DialogueManager 의 "전투씬 방식":
        브레인 끄기 / CM_GamePlay 끄기 / 메인 카메라 직접 이동  을 없애고
        CM_Dialogue 위치를 대화 지점으로 옮긴 뒤 켜기, 종료 시 끄기 로 변경
    - 대화 종료 후 SnapMainToTarget 호출은 필요 없어질 수 있음 (블렌드로 복귀)

  확인
    - 튜토리얼·챕터1 대화 시작/종료
    - 락다운 중 대화, 대화 중 락다운 해제


### 7단계. 마무리 정리

  - SetCameraDumping 은 대시에서 계속 쓰므로 유지 (CM_GamePlay 전용, 배율 방식)
    (이름 오타 Dumping -> Damping 정리는 호출부가 많아 별도 작업으로)
  - 핑크 대시 댐핑: 종료 후 잠깐 유지 -> 부드럽게 복구 방식으로 적용함 (2026-11-28)
    남은 점: 핑크 대시 직후 0.3초 안에 칩을 바꿔 초록/파랑 대시를 쓰면
    진행 중인 복구가 그 대시의 낮은 댐핑을 덮어쓸 수 있음 (복구 취소 처리를 일부러 넣지 않음)
  - BossTestSceneBuilder (에디터 툴) 이 만드는 카메라 구성도 새 구조에 맞게 수정
  - 사용하지 않는 필드/코드 정리, 이 문서의 "현재 구조" 를 새 구조로 갱신


------------------------------------------------------------
## 6. 전체 확인 목록 (단계마다 해당되는 것만)
------------------------------------------------------------

  씬
    [ ] Tutorial
    [ ] Basecamp
    [ ] Chapter_1
    [ ] Chapter_1_Boss

  상황
    [ ] 씬 시작 인트로
    [ ] 락다운 진입 / 해제 (가만히 / 움직이면서)
    [ ] 락다운 해제 도중 다음 락다운 진입
    [ ] 빨강 칩 녹화·재생 중 락다운
    [ ] 대시 중 카메라 댐핑 (핑크 / 초록 / 파랑)
    [ ] 대화 시작 / 종료, 락다운과 겹칠 때
    [ ] 보스 패턴 흔들림
    [ ] 사망 후 리스폰, 구역 저장 이어하기 직후 카메라 위치
    [ ] 창 크기 변경 (픽셀 퍼펙트 보정)


------------------------------------------------------------
## 7. 위험 요소
------------------------------------------------------------

  - 블렌드 방식으로 바꾸면 전환 "느낌"이 지금과 달라질 수 있음
    (지금은 위치와 줌을 같은 SmoothStep 으로 움직임. 블렌드는 커브 하나로 같이 움직이므로
     크게 다르지 않을 것으로 예상하지만, 3단계 후 반드시 비교)
  - 씬 파일(.unity)을 여러 명이 동시에 수정하면 충돌이 잘 남
    -> 카메라 오브젝트를 프리팹으로 묶어 두면 씬 충돌을 줄일 수 있음 (선택)
  - 다른 담당자 코드(LHM, LJS 폴더)를 건드리는 5·6단계는 반드시 먼저 협의
  - 작업 중 언제든 이전 단계 커밋으로 되돌릴 수 있도록 단계별 커밋 유지
