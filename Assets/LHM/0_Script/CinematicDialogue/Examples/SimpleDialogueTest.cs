using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DialogSystem.CinematicDialogue
{
    /// <summary>
    /// 테스트용 간단한 대화 시스템
    /// 특정 키 입력으로 다양한 대사를 재생한다.
    /// </summary>
    public class SimpleDialogueTest : MonoBehaviour
    {
        [SerializeField] private DialogueManager _dialogueManager;
        [SerializeField] private TextAsset _dialogueJson;

        // 키와 대사 ID 매핑
        private Dictionary<KeyCode, string> _keyBindings = new()
        {
            { KeyCode.Q, "intro_01" },        // Q키: 첫 번째 대사
            { KeyCode.W, "intro_02" },        // W키: 두 번째 대사
            { KeyCode.E, "intro_03" },        // E키: 세 번째 대사
            { KeyCode.R, "ending_01" },       // R키: 마지막 대사
        };

        private void Start()
        {
            Debug.Log("===== [SimpleDialogueTest] START =====");
            
            // 디버그 정보 출력
            if (_dialogueManager == null)
            {
                Debug.LogError("[SimpleDialogueTest] ❌ DialogueManager가 할당되지 않았습니다!");
                Debug.LogError("[SimpleDialogueTest] ❌ Inspector의 Dialogue Manager 필드에 DialogueManager를 드래그하세요!");
                return;
            }
            
            Debug.Log("[SimpleDialogueTest] ✓ DialogueManager 연결 완료!");
            Debug.Log("[SimpleDialogueTest] ✓ Q/W/E/R 키를 눌러 테스트하세요.");
            Debug.Log("=====================================");
        }

        private void Update()
        {
            if (_dialogueManager == null)
            {
                // 첫 시작에만 에러 출력하고 이후엔 조용히 있기
                return;
            }

            // Q, W, E, R 키 입력 확인
            foreach (var kvp in _keyBindings)
            {
                if (Input.GetKeyDown(kvp.Key))
                {
                    Debug.Log($"[SimpleDialogueTest] {kvp.Key} 키 감지! '{kvp.Value}' 재생 시작");
                    PlayDialogue(kvp.Value);
                    break;  // 한 번에 하나만 재생
                }
            }

            // ESC키로 중단
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("[SimpleDialogueTest] ESC 키 감지! 대사 중단");
                StopDialogue();
            }
        }

        /// <summary>
        /// 특정 대사 ID 재생
        /// </summary>
        private void PlayDialogue(string dialogueId)
        {
            if (_dialogueJson == null) return;
            Debug.Log($"[SimpleDialogueTest] 재생 시작: {dialogueId}");
            //_dialogueManager.PlayDialogueAsync(_dialogueJson, dialogueId).Forget();
        }

        /// <summary>
        /// 대사 중단
        /// </summary>
        private void StopDialogue()
        {
            Debug.Log("[SimpleDialogueTest] 대사 중단");
            _dialogueManager.Stop();
        }

        private void OnGUI()
        {
            // 화면에 키 안내 표시
            GUI.Label(new Rect(10, 10, 350, 120),
                "<size=20><b>대화 시스템 테스트</b></size>\n\n" +
                "Q - intro_01 (제니스 첫 인사)\n" +
                "W - intro_02 (제니스 두 번째)\n" +
                "E - intro_03 (리티 인사)\n" +
                "R - ending_01 (시스템 종료)\n" +
                "ESC - 대사 중단");
        }
    }
}
