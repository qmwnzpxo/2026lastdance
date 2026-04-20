using Boss.Phase1;
using System.Collections.Generic;
using UnityEngine;

namespace BossSystem
{
    public class BossBlackboard
    {
        // ── 플레이어 정보 ──────────────────────────────────────────────────
        public Transform PlayerTransform { get; private set; }
        public Vector3 PlayerPosition => PlayerTransform != null
                                            ? PlayerTransform.position : Vector3.zero;
        public Vector3 PlayerVelocity { get; private set; }

        private Vector3 _prevPlayerPosition;
        private bool _prevPositionInitialized;

        // ── 살아있는 LaserCore 목록 ────────────────────────────────────────
        public IReadOnlyList<LaserCore> AliveCores => _aliveCores;
        private readonly List<LaserCore> _aliveCores = new(4);

        // ── 파괴 예약 큐 ─────────────────────────────────────────────────
        private readonly Queue<LaserCore> _pendingRemove = new(4);

        // ── 이벤트 ────────────────────────────────────────────────────────
        public event System.Action OnCoreListChanged;
        public event System.Action OnBarrierBroken;

        // ─────────────────────────────────────────────────────────────────

        public BossBlackboard(Transform playerTransform)
        {
            PlayerTransform = playerTransform;
        }

        public void Tick()
        {
            FlushPendingRemovals();
        }

        private void FlushPendingRemovals()
        {
            if (_pendingRemove.Count == 0) return;

            while (_pendingRemove.Count > 0)
            {
                var core = _pendingRemove.Dequeue();
                _aliveCores.Remove(core);
                Debug.Log($"[Blackboard] Core flushed. Alive={_aliveCores.Count}");
            }

            OnCoreListChanged?.Invoke();

            if (_aliveCores.Count == 0)
                OnBarrierBroken?.Invoke();
        }

        // ── 플레이어 속도 갱신 ────────────────────────────────────────────
        public void UpdatePlayerVelocity()
        {
            if (PlayerTransform == null) return;

            Vector3 current = PlayerTransform.position;
            if (!_prevPositionInitialized)
            {
                _prevPlayerPosition = current;
                _prevPositionInitialized = true;
                PlayerVelocity = Vector3.zero;
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            PlayerVelocity = (current - _prevPlayerPosition) / dt;
            _prevPlayerPosition = current;
        }

        // ── 코어 관리 API ─────────────────────────────────────────────────
        public void RegisterCore(LaserCore core)
        {
            if (core == null || _aliveCores.Contains(core)) return;
            _aliveCores.Add(core);
            core.OnCoreDestroyed += HandleCoreDestroyed;
            OnCoreListChanged?.Invoke();
        }

        private void HandleCoreDestroyed(LaserCore core)
        {
            _pendingRemove.Enqueue(core);
            Debug.Log($"[Blackboard] Core queued for removal. Queue={_pendingRemove.Count}");
        }
        // BossBlackboard.cs 의 코어 관리 API 섹션 하단에 추가

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 전용. 코어 목록을 강제 비우고 OnBarrierBroken 이벤트를 즉시 발생시킵니다.
        /// BossController의 ContextMenu 버튼에서 호출.
        /// </summary>
        public void Debug_ForceBarrierBroken()
        {
            foreach (var core in _aliveCores)
                if (core != null)
                    core.OnCoreDestroyed -= HandleCoreDestroyed;

            _aliveCores.Clear();
            _pendingRemove.Clear();

            OnCoreListChanged?.Invoke();
            OnBarrierBroken?.Invoke();
            Debug.Log("[Blackboard] Debug_ForceBarrierBroken: OnBarrierBroken invoked.");
        }
#endif
    }

}
