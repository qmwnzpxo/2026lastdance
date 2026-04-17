using System.Linq;
using BossSystem;
using BossSystem.Patterns;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════
// BossPatternInjector.cs  —  씬 오브젝트 참조를 SO 패턴에 주입하는 브릿지
//
// ── 존재 이유 ─────────────────────────────────────────────────────────────
//   ScriptableObject는 Project 에셋이므로 씬 오브젝트(Transform 등)를
//   직접 직렬화할 수 없다. (Inspector에서 드래그하면 "타입 미스" 발생)
//
//   이 컴포넌트는 씬에 존재하며:
//     - Inspector에서 씬 오브젝트를 자유롭게 참조 가능
//     - Awake/Start에서 SO 패턴의 SetWaypoints() 등을 호출해 주입
//
// ── 사용법 ────────────────────────────────────────────────────────────────
//   1. 보스 오브젝트(또는 별도 매니저)에 이 컴포넌트 추가
//   2. Inspector에서:
//      - BossController 슬롯에 보스 오브젝트 연결
//      - Panel Waypoints 배열에 씬의 웨이포인트 Transform들 연결
//   3. Play → Awake에서 자동으로 Phase1PanelPattern에 주입
// ═══════════════════════════════════════════════════════════════════════════
namespace BossSystem
{
    public class BossPatternInjector : MonoBehaviour
    {
        [Header("보스 컨트롤러 참조")]
        [SerializeField] private BossController _bossController;

        [Header("Phase1 Panel 웨이포인트")]
        [Tooltip("씬에 배치된 웨이포인트 Transform들. SO에 직렬화 불가 → 여기서 주입.")]
        [SerializeField] private Transform[] _panelWaypoints;

        private void Awake()
        {
            if (_bossController == null)
            {
                Debug.LogError("[BossPatternInjector] BossController가 연결되지 않았습니다.");
                return;
            }

            InjectPanelWaypoints();
        }

        private void InjectPanelWaypoints()
        {
            if (_panelWaypoints == null || _panelWaypoints.Length == 0)
            {
                Debug.LogWarning("[BossPatternInjector] Panel Waypoints가 설정되지 않았습니다.");
                return;
            }

            // BossController의 모든 Phase를 순회해서 Phase1PanelPattern을 찾아 주입
            // BossController에 phases 접근용 프로퍼티가 없으므로 GetComponent로 접근
            var phases = _bossController.GetPhasesForInjection();
            if (phases == null) return;

            int injectedCount = 0;
            foreach (var phase in phases)
            {
                if (phase == null) continue;

                // persistentPatterns + sequentialPatterns 모두 검색
                var allPatterns = phase.persistentPatterns
                    .Concat(phase.sequentialPatterns)
                    .OfType<Phase1PanelPattern>();

                foreach (var pattern in allPatterns)
                {
                    pattern.SetWaypoints(_panelWaypoints);
                    injectedCount++;
                    Debug.Log($"[BossPatternInjector] Phase1PanelPattern에 웨이포인트 {_panelWaypoints.Length}개 주입 완료.");
                }
            }

            if (injectedCount == 0)
                Debug.LogWarning("[BossPatternInjector] Phase1PanelPattern을 찾지 못했습니다. " +
                                 "BossPhase에 패턴이 등록되어 있는지 확인하세요.");
        }
    }
}
