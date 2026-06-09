using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.CinematicDialogue
{
    /// <summary>
    /// JSON 데이터로부터 역직렬화될 대화 데이터 구조.
    /// 각 대사는 고유한 ID, 화자, 텍스트, 그리고 선택적 분기 정보를 포함한다.
    /// </summary>
    [System.Serializable]
    public class DialogueEntry
    {
        [SerializeField]
        public string id;  // 대사의 고유 ID (예: "scene_1_01")

        [SerializeField]
        public string speakerName;  // 화자의 이름 (예: "제니스")

        [SerializeField]
        public string text;  // 원본 대사 텍스트 (연출 태그 포함)
        // 예: "안녕하세요. <speed=0.05>[wait:0.5][event:CameraShake] 저는 제니스입니다. [click]"

        [SerializeField]
        public List<string> nextIds;  // 다음 대사 ID 리스트 (선형 흐름 또는 분기용)

        [SerializeField]
        public string jumpId;  // 점프할 대사 ID (선택사항)
    }

    /// <summary>
    /// 여러 DialogueEntry를 관리하는 컨테이너.
    /// JSON 파일 전체 구조를 표현한다.
    /// </summary>
    [System.Serializable]
    public class DialogueDatabase
    {
        [SerializeField]
        public List<DialogueEntry> dialogues;

        public DialogueDatabase()
        {
            dialogues = new List<DialogueEntry>();
        }

        /// <summary>
        /// ID로 특정 대사를 검색한다.
        /// </summary>
        public DialogueEntry FindDialogue(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return dialogues.Find(d => d.id == id);
        }
    }

    /// <summary>
    /// DialogueManager에서 사용할 런타임 상태 정보.
    /// 현재 재생 중인 대사, 인덱스, 텍스트 축적 상태 등을 추적한다.
    /// </summary>
    [System.Serializable]
    public class DialoguePlaybackState
    {
        public DialogueEntry currentEntry;
        public int totalCharacters;  // 현재 대사의 총 글자 수 (태그 제외)
        public int displayedCharacters;  // 현재 표시된 글자 수
        public float elapsedTime;  // 현재 재생 시간
        public bool isWaitingForInput;  // 입력 대기 중 여부
        public bool isPaused;  // 일시 정지 상태

        public DialoguePlaybackState()
        {
            currentEntry = null;
            totalCharacters = 0;
            displayedCharacters = 0;
            elapsedTime = 0f;
            isWaitingForInput = false;
            isPaused = false;
        }
    }
}
