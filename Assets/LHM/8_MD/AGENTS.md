# AGENTS.md

이 저장소의 모든 규칙은 **`AI_RULES.md`** 에 있다. 작업 전에 반드시 `AI_RULES.md` 전체를 먼저 읽는다.
(포함 문법을 쓰지 않는 이유: Antigravity는 이 문서와 `GEMINI.md` 를 함께 읽으므로, 여기서도 포함하면 같은 규칙이 두 번 들어가 토큰을 낭비하고 24KB 제한에 걸린다.)

코드 구조는 `docs/ARCHITECTURE.md`, 기획은 `docs/spec/*.md` 를 따른다.

## 역할 확인

이 저장소는 역할이 정해진 AI 협업 체계(AI_RULES 1장)로 운영된다.

| 도구 / 모델 | 역할 | 역할 문서 |
|---|---|---|
| Claude (Claude Code 등) | 구현 | `CLAUDE.md` |
| Gemini (Gemini CLI, Antigravity) | 리뷰 (코드 수정 금지) | `GEMINI.md` |
| 그 외 도구 (Windsurf, Copilot, Rider AI 등) | 현민이 지시한 범위의 보조 작업 | 이 문서 |

- Antigravity는 이 문서와 `GEMINI.md` 를 함께 읽는다. 이 경우 역할은 `GEMINI.md` 의 **리뷰 담당**을 따른다.
- 그 외 도구는 `AI_RULES.md` 의 규칙을 그대로 지킨다. 특히:
  - 2장 Unity 6·C# 금지표, 0장 2번 **씬·프리팹·에셋 YAML 직접 수정 금지**
  - 6장 검증: Unity를 실행하지 않는다. 검증은 현민이 씬에서 한다 → "씬 테스트 절차"를 쓴다
  - 7장 **커밋 금지**: git은 읽기 전용 명령만 쓴다. 커밋은 현민이 직접 판단한다
  - 작업을 마치면 `docs/HANDOFF.md` 를 갱신한다 (에디터 작업, 씬 테스트 절차, 커밋 제안 포함).
