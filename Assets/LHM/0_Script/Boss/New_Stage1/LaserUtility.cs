using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BossSystem
{
    
    public static class LaserUtility
    {
        // ── 1. LineRenderer 경고선 페이드인 ──────────────────────────────

        /// <param name="lr">대상 LineRenderer</param>
        /// <param name="from">시작 위치</param>
        /// <param name="to">끝 위치 (매 프레임 갱신됨)</param>
        /// <param name="fullColor">완전 불투명 목표 색상</param>
        /// <param name="colorBuffer">재사용 Color 버퍼 (호출자 필드로 유지)</param>
        /// <param name="duration">페이드인 총 시간 (초)</param>
        /// <param name="trackTarget">true 이면 매 프레임 to 위치를 갱신하는 델리게이트</param>
        public static async UniTask FadeInWarningLineAsync(
           LineRenderer lr,
           System.Func<Vector3> getOrigin, // 시작점도 동적으로!
           System.Func<Vector3> getTarget,
           Color fullColor,
           float duration,
           CancellationToken ct)
        {
            if (lr == null) return;

            try
            {
                lr.enabled = true;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    // cancellationToken을 Yield에 전달하면 알아서 Exception을 던짐
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);

                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;

                    Color c = fullColor;
                    c.a = Mathf.Lerp(0f, fullColor.a, t);
                    lr.startColor = lr.endColor = c;

                    lr.SetPosition(0, getOrigin());
                    lr.SetPosition(1, getTarget());
                }

                lr.startColor = lr.endColor = fullColor;
            }
            catch (System.OperationCanceledException)
            {
                // 취소되었을 때 깔끔하게 지워주는 게 시니어의 품격
                HideLine(lr);
            }
        }

        /// <summary>
        /// LineRenderer 색상과 위치를 즉시 설정 (발사 단계 전환용).
        /// </summary>
        public static void SetLineImmediate(
            LineRenderer lr,
            Vector3 from,
            Vector3 to,
            Color color)
        {
            if (lr == null) return;
            lr.startColor = color;
            lr.endColor   = color;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.enabled = true;
        }

        /// <summary>LineRenderer 비활성화. null 안전.</summary>
        public static void HideLine(LineRenderer lr)
        {
            if (lr != null) lr.enabled = false;
        }


        // ── 2. Physics2D 판정 래퍼 ───────────────────────────────────────

        /// <summary>
        /// CircleCast 단발 판정. 히트 시 IPlayerHealth.TakeDamage 자동 호출.
        ///
        /// CircleCast 선택 근거:
        ///   점에서 한 방향으로 발사되는 얇은 레이저 (판넬, 난사 등).
        ///   반경 하나로 두께 제어가 직관적이며 터널링 방지.
        ///
        /// 반환: 히트 여부 (true = 플레이어 맞음)
        /// </summary>
        public static bool CircleCastAndDamage(
            Vector2   origin,
            Vector2   direction,
            float     radius,
            float     distance,
            LayerMask playerLayer,
            float     damage)
        {
            var hit = Physics2D.CircleCast(origin, radius, direction, distance, playerLayer);
            if (hit.collider == null) return false;

            //hit.collider.GetComponent<PlayerMove>()?.TakeDamage(damage);
            return true;
        }

        /// <summary>
        /// BoxCast 단발 판정. 두께가 있는 레이저 빔 (좌우 스윕 레이저 등).
        ///
        /// BoxCast 선택 근거:
        ///   가로/세로 폭이 있는 '면형' 레이저. size.x = 두께, distance = 사거리.
        ///   각도를 Atan2로 방향에서 자동 계산.
        /// </summary>
        public static bool BoxCastAndDamage(
            Vector2   origin,
            Vector2   direction,
            float     thickness,
            float     distance,
            LayerMask playerLayer,
            float     damage)
        {
            float angle   = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var   size    = new Vector2(thickness, thickness);

            var hit = Physics2D.BoxCast(origin, size, angle, direction, distance, playerLayer);
            if (hit.collider == null) return false;

          //  hit.collider.GetComponent<IPlayerHealth>()?.TakeDamage(damage);
            return true;
        }

        /// <summary>
        /// DPS(초당 데미지) 방식 CircleCast. LateUpdate/루프에서 매 프레임 호출.
        /// damage = dps × Time.deltaTime → 프레임레이트 독립.
        /// </summary>
        public static void CircleCastDPS(
            Vector2   origin,
            Vector2   direction,
            float     radius,
            float     distance,
            LayerMask playerLayer,
            float     dps)
        {
            CircleCastAndDamage(origin, direction, radius, distance, playerLayer,
                                dps * Time.deltaTime);
        }


        // ── 3. 오브젝트 지연 파괴 ────────────────────────────────────────

        /// <summary>
        /// GameObject를 delay 초 후에 파괴하는 Fire-and-Forget UniTask.
        ///
        /// Forget() 호출 → 메인 루프 블로킹 없이 병렬 처리.
        /// SuppressCancellationThrow → ct 취소 시 예외 없이 정리 후 파괴.
        /// </summary>
        public static async UniTaskVoid DestroyAfterDelayAsync(
            GameObject    go,
            float         delay,
            CancellationToken ct)
        {
            // SuppressCancellationThrow: 취소돼도 예외를 던지지 않고 진행
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delay),
                cancellationToken: ct
            ).SuppressCancellationThrow();

            if (go != null) Object.Destroy(go);
        }


        // ── 4. MoveTowards 이동 ──────────────────────────────────────────

        /// <summary>
        /// Transform을 Vector3.MoveTowards로 target 위치까지 이동.
        ///
        /// Lerp 대신 MoveTowards를 선택한 이유:
        ///   최대 이동 속도(speed)가 보장되므로 먼 거리도 일정 속도로 도달.
        ///   Lerp는 지수적 감속이 발생해 목표 도달이 보장되지 않음.
        ///
        /// SqrMagnitude: sqrt 연산 없는 거리 비교 (매 프레임 최적화).
        /// </summary>
        public static async UniTask MoveTowardsAsync(
            Transform         target,
            Vector3           destination,
            float             speed,
            CancellationToken ct,
            float             arrivalThresholdSqr = 0.0001f) // 0.01f²
        {
            while (!ct.IsCancellationRequested)
            {
                target.position = Vector3.MoveTowards(
                    target.position, destination, speed * Time.deltaTime);

                if ((target.position - destination).sqrMagnitude <= arrivalThresholdSqr)
                {
                    target.position = destination; // 최종 스냅
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }


        // ── 5. 조준 회전 ─────────────────────────────────────────────────

        /// <summary>
        /// Transform을 target 방향으로 즉시 Z축 회전 (2D 게임).
        ///
        /// Atan2(y, x) → 라디안 → 도 변환.
        /// sqrMagnitude 0 체크로 방향 벡터 zero 예외 방지.
        /// </summary>
        public static void AimAt2D(Transform self, Vector3 target)
        {
            Vector3 dir = target - self.position;
            if (dir.sqrMagnitude < 0.001f) return;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            self.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
