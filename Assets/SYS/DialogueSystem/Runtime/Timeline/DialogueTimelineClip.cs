// ──────────────────────────────────────────────────────────────────────────────
//  DialogueTimelineClip.cs
//  Timeline에서 대화를 트리거하는 커스텀 클립.
//
//  사용법:
//  1. Timeline 창에서 빈 트랙을 우클릭 → Add Signal Track 대신,
//     "Add Dialogue Track" 트랙 추가 (아래 DialogueTrack 참고)
//  2. 트랙 위 원하는 시간에 우클릭 → Add Dialogue Clip
//  3. 클립의 Inspector에서 Start Dialogue ID 입력
//  4. 씬에 DialogueManager가 있어야 함
//
//  동작:
//  클립 시작 → Timeline 일시정지 → DialogueManager.StartDialogue(id) 호출
//  대화 종료 → Timeline 자동 재개
// ──────────────────────────────────────────────────────────────────────────────

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DialogueSystem
{
    // ── PlayableAsset (클립 에셋) ───────────────────────────────────────────────

    [Serializable]
    public class DialogueTimelineClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("시작할 대화 ID (DialogueDatabase의 ID 값)")]
        public string startDialogueId;

        [Tooltip("대화가 끝나면 Timeline을 자동으로 재개합니다")]
        public bool resumeTimelineOnEnd = true;

        [Tooltip("대화 진행 중 Time Scale을 0으로 설정 (다른 애니메이션 정지)")]
        public bool pauseTimeScale = false;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable  = ScriptPlayable<DialogueTimelineBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();

            behaviour.startDialogueId    = startDialogueId;
            behaviour.resumeTimelineOnEnd = resumeTimelineOnEnd;
            behaviour.pauseTimeScale      = pauseTimeScale;

            return playable;
        }
    }

    // ── PlayableBehaviour (런타임 로직) ───────────────────────────────────────

    [Serializable]
    public class DialogueTimelineBehaviour : PlayableBehaviour
    {
        public string startDialogueId;
        public bool   resumeTimelineOnEnd;
        public bool   pauseTimeScale;

        private PlayableDirector _director;
        private bool             _started;
        private float            _prevTimeScale;

        public override void OnGraphStart(Playable playable)
        {
            _started = false;
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (!Application.isPlaying || _started) return;
            _started = true;

            // PlayableDirector 취득
            _director = FindDirector(info);

            // Timeline 일시정지
            _director?.Pause();

            if (pauseTimeScale)
            {
                _prevTimeScale  = Time.timeScale;
                Time.timeScale  = 0f;
            }

            var manager = DialogueManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[DialogueTimeline] DialogueManager가 씬에 없습니다.");
                ResumeIfNeeded();
                return;
            }

            // 대화 종료 콜백 등록
            manager.OnDialogueComplete += HandleDialogueComplete;

            if (string.IsNullOrEmpty(startDialogueId))
            {
                Debug.LogWarning("[DialogueTimeline] startDialogueId가 비어 있습니다.");
                HandleDialogueComplete();
                return;
            }

            manager.StartDialogue(startDialogueId);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // 클립이 끝나거나 Stop될 때 구독 해제
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.OnDialogueComplete -= HandleDialogueComplete;

            ResumeIfNeeded();
        }

        private void HandleDialogueComplete()
        {
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.OnDialogueComplete -= HandleDialogueComplete;

            ResumeIfNeeded();
        }

        private void ResumeIfNeeded()
        {
            if (pauseTimeScale)
                Time.timeScale = _prevTimeScale;

            if (resumeTimelineOnEnd && _director != null &&
                _director.state == PlayState.Paused)
            {
                _director.Play();
            }
        }

        private static PlayableDirector FindDirector(FrameData info)
        {
            // FrameData.output의 userData로 director를 가져오거나 씬에서 검색
            if (info.output.GetUserData() is PlayableDirector d) return d;
            return UnityEngine.Object.FindFirstObjectByType<PlayableDirector>();
        }
    }

    // ── TrackAsset (Timeline 트랙) ────────────────────────────────────────────

    [TrackClipType(typeof(DialogueTimelineClip))]
    [TrackColor(0.2f, 0.8f, 0.6f)]   // 녹청색 — Timeline에서 구분 쉽게
    public class DialogueTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph,
                                                  GameObject go, int inputCount)
        {
            return ScriptPlayable<EmptyBehaviour>.Create(graph, inputCount);
        }
    }

    // 믹서로 사용하는 빈 동작 (트랙 레벨 믹싱 불필요)
    public class EmptyBehaviour : PlayableBehaviour { }
}
