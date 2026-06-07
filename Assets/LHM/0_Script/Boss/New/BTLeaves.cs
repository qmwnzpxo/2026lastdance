using UnityEngine;
using System;
using System.Collections;

namespace BossBT
{
    /// <summary>
    /// Behavior Tree 리프 노드들
    /// 조건 노드와 행동 노드들을 포함
    /// </summary>

    /// <summary>
    /// CheckDistance - 조건 노드: 거리 범위 충족 여부
    /// 생성자: (Transform boss, Transform player, float minDist, float maxDist)
    /// Evaluate: X축 절대 거리가 [minDist, maxDist) 범위면 Success, 범위 밖이면 Failure
    /// </summary>
    public class CheckDistance : BTNode
    {
        private Transform boss;
        private Transform player;
        private float minDist;
        private float maxDist;

        public CheckDistance(Transform boss, Transform player, float minDist, float maxDist)
        {
            this.boss = boss;
            this.player = player;
            this.minDist = minDist;
            this.maxDist = maxDist;
        }

        public override NodeStatus Evaluate()
        {
            if (boss == null || player == null)
                return NodeStatus.Failure;

            float distance = Mathf.Abs(boss.position.x - player.position.x);
            if (distance >= minDist && distance < maxDist)
            {
                return NodeStatus.Success;
            }
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// CheckHPRatio - 조건 노드: HP 비율 범위 충족 여부
    /// 생성자: (Func<float> getHPRatio, float minRatio, float maxRatio)
    /// Evaluate: getHPRatio()가 [minRatio, maxRatio] 범위면 Success, 범위 밖이면 Failure
    /// </summary>
    public class CheckHPRatio : BTNode
    {
        private Func<float> getHPRatio;
        private float minRatio;
        private float maxRatio;

        public CheckHPRatio(Func<float> getHPRatio, float minRatio, float maxRatio)
        {
            this.getHPRatio = getHPRatio;
            this.minRatio = minRatio;
            this.maxRatio = maxRatio;
        }

        public override NodeStatus Evaluate()
        {
            float hpRatio = getHPRatio();
            if (hpRatio >= minRatio && hpRatio <= maxRatio)
            {
                return NodeStatus.Success;
            }
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// CheckMaxDroneCount - 조건 노드: 드론 수 초과 체크
    /// 생성자: (Func<int> getActiveDroneCount, int maxCount)
    /// Evaluate: 현재 활성 드론 수 < maxCount이면 Success, maxCount 이상이면 Failure
    /// </summary>
    public class CheckMaxDroneCount : BTNode
    {
        private Func<int> getActiveDroneCount;
        private int maxCount;

        public CheckMaxDroneCount(Func<int> getActiveDroneCount, int maxCount)
        {
            this.getActiveDroneCount = getActiveDroneCount;
            this.maxCount = maxCount;
        }

        public override NodeStatus Evaluate()
        {
            int activeCount = getActiveDroneCount();
            if (activeCount < maxCount)
            {
                return NodeStatus.Success;
            }
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// ExecuteAttackNode - 행동 노드: 공격 패턴 코루틴 실행
    /// 생성자: (BossAttackPatterns patterns, AttackPattern pattern, MonoBehaviour owner)
    /// 내부 상태: bool _running, bool _done
    /// Evaluate 로직:
    ///   1) _running == false이면 → 코루틴 시작, _running = true, Running 반환
    ///   2) _running == true, _done == false이면 → Running 반환
    ///   3) _done == true이면 → Reset() 호출 후 Success 반환
    /// Reset(): _running = false, _done = false 로 초기화
    /// 타임아웃: 10초 내에 완료되지 않으면 강제 실패
    /// </summary>
    public class ExecuteAttackNode : BTNode
    {
        private BossAttackPatterns patterns;
        private AttackPattern pattern;
        private MonoBehaviour owner;
        private BossController bossController;
        private bool _running;
        private bool _done;
        private Coroutine currentCoroutine;
        private float startTime;
        private const float TIMEOUT = 10f;

        public ExecuteAttackNode(BossAttackPatterns patterns, AttackPattern pattern, MonoBehaviour owner, BossController controller)
        {
            this.patterns = patterns;
            this.pattern = pattern;
            this.owner = owner;
            this.bossController = controller;
            _running = false;
            _done = false;
        }

        public override NodeStatus Evaluate()
        {
            if (!_running)
            {
                _running = true;
                _done = false;
                startTime = Time.time;
                currentCoroutine = owner.StartCoroutine(ExecutePatternCoroutine());
                return NodeStatus.Running;
            }
            else if (_running && !_done)
            {
                // 타임아웃 체크
                if (Time.time - startTime > TIMEOUT)
                {
                    Debug.LogWarning($"[BossBT] ExecuteAttackNode 타임아웃: {pattern} (10초 초과)");
                    Reset();
                    return NodeStatus.Failure;
                }
                return NodeStatus.Running;
            }
            else // _done == true
            {
                Reset();
                return NodeStatus.Success;
            }
        }

        private IEnumerator ExecutePatternCoroutine()
        {
            yield return owner.StartCoroutine(patterns.ExecutePattern(pattern, bossController));
            _done = true;
        }

        public override void Reset()
        {
            base.Reset();
            _running = false;
            _done = false;
            if (currentCoroutine != null)
            {
                owner.StopCoroutine(currentCoroutine);
                currentCoroutine = null;
            }
        }
    }

    /// <summary>
    /// WaitNode - 행동 노드: 지정 시간 대기
    /// 생성자: (float duration, MonoBehaviour owner)
    /// Evaluate 로직:
    ///   1) 첫 호출 시 코루틴 시작, Running 반환
    ///   2) 완료 전 Running 반환
    ///   3) 완료 후 Reset() 호출 후 Success 반환
    /// Reset(): 내부 타이머 및 플래그 초기화
    /// 타임아웃: 지정 시간 + 5초 내에 완료되지 않으면 강제 실패
    /// </summary>
    public class WaitNode : BTNode
    {
        private float duration;
        private MonoBehaviour owner;
        private bool _running;
        private bool _done;
        private Coroutine currentCoroutine;
        private float startTime;

        public WaitNode(float duration, MonoBehaviour owner)
        {
            this.duration = duration;
            this.owner = owner;
            _running = false;
            _done = false;
        }

        public override NodeStatus Evaluate()
        {
            if (!_running)
            {
                _running = true;
                _done = false;
                startTime = Time.time;
                currentCoroutine = owner.StartCoroutine(WaitCoroutine());
                return NodeStatus.Running;
            }
            else if (_running && !_done)
            {
                // 타임아웃 체크 (지정 시간 + 5초 여유)
                if (Time.time - startTime > duration + 5f)
                {
                    Debug.LogWarning($"[BossBT] WaitNode 타임아웃: {duration}초 대기 실패");
                    Reset();
                    return NodeStatus.Failure;
                }
                return NodeStatus.Running;
            }
            else // _done == true
            {
                Reset();
                return NodeStatus.Success;
            }
        }

        private IEnumerator WaitCoroutine()
        {
            yield return new WaitForSeconds(duration);
            _done = true;
        }

        public override void Reset()
        {
            base.Reset();
            _running = false;
            _done = false;
            if (currentCoroutine != null)
            {
                owner.StopCoroutine(currentCoroutine);
                currentCoroutine = null;
            }
        }
    }
}
