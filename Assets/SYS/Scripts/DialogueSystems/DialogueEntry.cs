using System.Collections.Generic;

namespace DialogueSystem
{
    [System.Serializable]
    public class DialogueEntry
    {
        public string Id;
        public string Speaker;
        public string Portrait;       // Resources/Portraits/ 안의 파일명 (확장자 제외)
        public string RawText;        // {shake} {wave} {big} {color} {pause} 태그 포함
        public string NextId;         // 비어있으면 대화 종료
        public List<ChoiceEntry> Choices;
        public string Command;        // FADE_IN, PLAY_SFX:name 등
        public float AutoDelay;       // > 0 이면 자동 진행 (초)
        public float SpeedOverride;   // 0 = 기본 타이핑 속도 사용
    }

    [System.Serializable]
    public class ChoiceEntry
    {
        public string Label;
        public string TargetId;
    }
}
