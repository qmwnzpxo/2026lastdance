using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace DialogueSystem.Timeline
{
    // ════════════════════════════════════════════════════════════
    //  DialogueTrack
    //  Timeline 창에서 + → DialogueTrack 으로 추가
    // ════════════════════════════════════════════════════════════

    [TrackColor(0.9f, 0.6f, 0.1f)]
    [TrackClipType(typeof(DialoguePlayableAsset))]
    public class DialogueTrack : TrackAsset { }

    // ════════════════════════════════════════════════════════════
    //  DialoguePlayableAsset
    //  클립 Inspector에서 database와 startId 설정
    // ════════════════════════════════════════════════════════════

    public class DialoguePlayableAsset : PlayableAsset
    {
        [Tooltip("사용할 DialogueDatabase")]
        public DialogueDatabase database;

        [Tooltip("시작할 대화 ID")]
        public string startId;

        [Tooltip("대화가 끝날 때까지 타임라인을 일시정지")]
        public bool pauseTimelineUntilEnd = true;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable  = ScriptPlayable<DialoguePlayableBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.database      = database;
            behaviour.startId       = startId;
            behaviour.pauseTimeline = pauseTimelineUntilEnd;
            return playable;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  DialoguePlayableBehaviour
    //  런타임 로직 — 직접 수정할 일 거의 없음
    // ════════════════════════════════════════════════════════════

    public class DialoguePlayableBehaviour : PlayableBehaviour
    {
        public DialogueDatabase database;
        public string startId;
        public bool pauseTimeline;

        private PlayableDirector _director;
        private bool _triggered;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (!Application.isPlaying || _triggered) return;
            _triggered = true;

            _director = playable.GetGraph().GetResolver() as PlayableDirector;
            if (pauseTimeline) _director?.Pause();

            DialogueManager.Instance.StartDialogue(database, startId);
            DialogueManager.Instance.OnDialogueEnd.AddListener(OnDialogueEnd);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info) =>
            _triggered = false;

        public override void OnPlayableDestroy(Playable playable)
        {
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.OnDialogueEnd.RemoveListener(OnDialogueEnd);
        }

        void OnDialogueEnd()
        {
            DialogueManager.Instance.OnDialogueEnd.RemoveListener(OnDialogueEnd);
            if (pauseTimeline) _director?.Play();
        }
    }
}
