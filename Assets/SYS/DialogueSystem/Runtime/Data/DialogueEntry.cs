using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 대화 한 줄의 데이터. CSV에서 로드하거나 ScriptableObject에 직접 입력.
    /// </summary>
    [Serializable]
    public class DialogueEntry
    {
        [Tooltip("고유 식별자 (예: NPC001_001)")]
        public string id;

        [Tooltip("화자 이름 (UI에 표시)")]
        public string speaker;

        [Tooltip("Resources/Portraits/ 아래 스프라이트 파일명")]
        public string portraitKey;

        [Tooltip("효과 태그 포함 원본 텍스트\n예: [wave]안녕하세요[/wave]! [shake]조심해요![/shake]")]
        [TextArea(2, 5)]
        public string rawText;

        [Tooltip("다음 대화 ID. 비워두면 대화 종료.")]
        public string nextId;

        [Tooltip("선택지 목록. 있으면 nextId 무시.")]
        public List<ChoiceData> choices = new();

        [Tooltip("Resources/BGM/ 아래 오디오 클립 파일명")]
        public string bgmKey;

        [Tooltip("Resources/Voice/ 아래 오디오 클립 파일명")]
        public string voiceKey;

        [Tooltip("카메라 효과: none / shake / zoom_in / zoom_out")]
        public string cameraEffect;

        [Tooltip("0이면 클릭 대기. 양수면 N초 후 자동 진행.")]
        public float autoAdvanceTime;

        [Tooltip("이 플래그가 true일 때만 이 대화 표시. 비워두면 항상 표시.")]
        public string conditionFlag;

        [Tooltip("이 대화가 표시된 후 세팅할 플래그 키")]
        public string setFlag;
    }

    [Serializable]
    public class ChoiceData
    {
        [Tooltip("선택지 표시 텍스트")]
        public string text;

        [Tooltip("선택 시 이동할 대화 ID")]
        public string nextId;

        [Tooltip("이 플래그가 false면 선택지 숨김. 비워두면 항상 표시.")]
        public string conditionFlag;
    }
}
