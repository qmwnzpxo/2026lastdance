# 🎬 시네마틱 대화 시스템 (Cinematic Dialogue System)

## 개요

**JSON 기반의 자동화된 시네마틱 텍스트 연출 대화 시스템**입니다.
에디터에서 일일이 효과를 주지 않고, JSON 데이터와 커스텀 태그로 텍스트 애니메이션, 타이밍, 흐름 제어를 모두 관리할 수 있습니다.

---

## 🏗️ 아키텍처

```
DialogueData.cs
  ├─ DialogueEntry: 단일 대사 데이터
  ├─ DialogueDatabase: 전체 대사 컬렉션
  └─ DialoguePlaybackState: 실행 상태 추적

DialogueParser.cs
  ├─ DialogueCommand: 실행 가능한 명령 단위
  └─ 정규식 기반 텍스트/태그 파싱 로직

DialogueManager.cs (MonoBehaviour)
  ├─ JSON 로드 및 데이터베이스 생성
  ├─ 비동기 텍스트 출력 (글자 단위)
  ├─ 커맨드 실행 (속도, 대기, 이벤트, 클릭, 점프)
  └─ 분기 및 점프 로직
```

---

## 📝 JSON 포맷

### 기본 구조

```json
[
  {
    "id": "scene_1_01",           // 고유한 대사 ID
    "speakerName": "제니스",       // 화자 이름
    "text": "안녕하세요. <speed=0.05>[wait:0.5] 저는 제니스입니다. [click]",
    "nextIds": ["scene_1_02"],    // 다음 대사 ID들
    "jumpId": ""                   // 점프 대상 (선택사항)
  },
  ...
]
```

### 지원되는 태그

#### 1. 연출용 태그 (삼각괄호 `<>`)

| 태그 | 설명 | 예시 | 기능 |
|------|------|------|------|
| `<speed=값>` | 타이핑 속도 변경 (초/글자) | `<speed=0.1>` | 이후 글자를 0.1초씩 출력 |
| `<shake>` | 텍스트 흔들림 | `<shake>` | 텍스트가 좌우로 흔들림 |
| `<pulse>` | 텍스트 펄싱 | `<pulse>` | 텍스트 크기 변동 |
| `<emphasis>` | 텍스트 강조 | `<emphasis>` | 첫 단어 크기 증가 및 깜빡임 |

#### 2. 흐름 제어용 태그 (대괄호 `[]`)

| 태그 | 설명 | 예시 | 기능 |
|------|------|------|------|
| `[wait:초]` | 지정 시간 대기 | `[wait:1.5]` | 1.5초 대기 |
| `[click]` | 클릭 대기 | `[click]` | 사용자 마우스 클릭까지 일시정지 |
| `[event:이름]` | 외부 이벤트 트리거 | `[event:CameraShake]` | 콜백으로 게임 이벤트 실행 |
| `[jump:ID]` | 대사 점프 | `[jump:scene_2_05]` | 현재 대사 완료 후 해당 ID로 이동 |

### 실제 예제

```json
{
  "id": "intro_01",
  "speakerName": "제니스",
  "text": "안녕하세요. <speed=0.05>[wait:0.5] 저는 제니스입니다. [click]",
  "nextIds": ["intro_02"],
  "jumpId": ""
}
```

**실행 흐름:**
1. "안녕하세요. " → 기본 속도로 출력
2. 0.5초 대기 (`[wait:0.5]`)
3. "저는 제니스입니다. " → 0.05초/글자 속도로 출력
4. 클릭 대기 (`[click]`) → 사용자 입력까지 진행 안 함

---

## 🎮 Unity에서 사용하기

### 1. 씬 설정

1. **Canvas 생성**
   - `UI > Text (TextMeshPro)` 추가

2. **DialogueManager 스크립트 적용**
   - 빈 GameObject에 `DialogueManager.cs` 추가
   - Inspector 설정:
     - `Dialogue Json Asset` → `Assets/Dialogue/CinematicDialogue/Resources/dialogues.json`
     - `Display Text` → TextMeshPro 컴포넌트 연결
     - `Speaker Name Text` → 화자 이름용 TMP (선택사항)

3. **DialogueExample 추가** (테스트용)
   - 빈 GameObject에 `DialogueExample.cs` 추가
   - `Dialogue Manager` → DialogueManager 인스턴스 연결

### 2. 코드에서 실행

```csharp
// 방법 1: 간단한 호출
await _dialogueManager.PlayDialogueAsync("intro_01");

// 방법 2: 취소 토큰 함께 사용
var cts = new CancellationTokenSource();
await _dialogueManager.PlayDialogueAsync("intro_01", cts.Token);

// 취소
cts.Cancel();
```

### 3. 이벤트 처리

```csharp
// 콜백 등록
_dialogueManager.onEventTriggered += (eventName) =>
{
    Debug.Log($"이벤트: {eventName}");
    // 카메라 흔들기, 사운드 재생 등 구현
};

_dialogueManager.onJumpRequested += (targetId) =>
{
    Debug.Log($"점프: {targetId}");
    // 분기 처리 로직
};

_dialogueManager.onDialogueCompleted += () =>
{
    Debug.Log("대사 완료");
};
```

---

## 🔧 커스텀 커맨드 추가하기

### DialogueParser에 새 태그 추가

```csharp
// DialogueParser.cs의 ParseAngleBracketTag 메서드에 추가
case "customevent":
    // 커스텀 이벤트 처리
    commands.Add(new DialogueCommand(DialogueCommand.CommandType.Event, "custom"));
    break;
```

### DialogueManager에서 커맨드 처리

```csharp
// DialogueManager.cs의 ExecuteCommandsAsync 메서드에 추가
case DialogueCommand.CommandType.CustomEvent:
    // 커스텀 처리 로직
    onEventTriggered?.Invoke("custom_event");
    break;
```

---

## 📊 정규식 패턴 해석

### DialogueParser의 핵심 정규식

```csharp
// 대괄호 태그: [태그명:값]
\[([a-zA-Z_]+):?([^\]]*)\]
 └─────────┬────────┘  └──┬──┘
      태그명          대괄호 안의 값

// 삼각괄호 태그: <태그명=값>
\<([a-zA-Z_]+)=?([^\>]*)\>
 └─────────┬────────┘  └──┬──┘
      태그명             값
```

**예시:**
- `[click]` → `tagName="click"`, `tagValue=""`
- `[wait:1.5]` → `tagName="wait"`, `tagValue="1.5"`
- `<speed=0.1>` → `tagName="speed"`, `tagValue="0.1"`

---

## ⚡ 성능 최적화 팁

1. **JSON 사전 로드**
   - Awake에서 한 번만 로드, 재사용

2. **스트링 빌드 최소화**
   - TextMeshPro의 text 프로퍼티 설정을 최소화
   - 로테이션 스트링 빌더 활용 고려

3. **비동기 취소**
   - 필요시 `CancellationToken`으로 조기 종료

```csharp
var cts = new CancellationTokenSource();
var task = _dialogueManager.PlayDialogueAsync("intro", cts.Token);

// 사용자가 ESC 누르면
cts.Cancel();
```

---

## 🐛 디버그

### DialogueParser 파싱 결과 확인

```csharp
// Parser.cs 사용
List<DialogueCommand> commands = DialogueParser.Parse(rawText);
DialogueParser.DebugPrintCommands(commands);  // 로그 출력
```

### 현재 재생 상태 조회

```csharp
DialoguePlaybackState state = _dialogueManager.GetPlaybackState();
Debug.Log($"현재 글자: {state.displayedCharacters}/{state.totalCharacters}");
Debug.Log($"입력 대기: {state.isWaitingForInput}");
```

---

## 📚 확장 예제: 복잡한 분기 시스템

```json
{
  "id": "choice_point",
  "speakerName": "NPC",
  "text": "어느 길로 가시겠어요? [click]",
  "nextIds": ["path_a", "path_b"],
  "jumpId": ""
}
```

**C# 처리:**
```csharp
// 선택지 UI 표시
ShowChoiceUI(new[] { "경로 A", "경로 B" });

// 사용자 선택 후
if (userChoice == 0)
    await _dialogueManager.PlayDialogueAsync("path_a");
else
    await _dialogueManager.PlayDialogueAsync("path_b");
```

---

## 📋 체크리스트

- [ ] JSON 파일 `Assets/Dialogue/CinematicDialogue/Resources/dialogues.json`에 배치
- [ ] Canvas와 TextMeshPro UI 생성
- [ ] DialogueManager를 GameObject에 추가하고 참조 연결
- [ ] 테스트: `Space` → 대화 시작, `ESC` → 중단
- [ ] 커스텀 이벤트 콜백 구현
- [ ] 점프/분기 로직 통합

---

## 🎯 다음 단계

1. **멀티샷 이펙트**: 여러 글자에 다른 이펙트 적용
2. **성우 목소리**: `[event:PlayVoice:clip_name]` 형태로 음성 재생
3. **타이머 UI**: 남은 시간 표시
4. **자동 진행**: 일정 시간 후 자동으로 다음 대사

