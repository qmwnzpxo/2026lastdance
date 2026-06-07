using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BossSystem
{
    public static class LaserUtility
    {
        // ── 1. LineRenderer 경고선 페이드인 ──────────────────────────────

        public static async UniTask FadeInWarningLineAsync(
           LineRenderer lr,
           System.Func<Vector3> getOrigin,
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
                HideLine(lr);
            }
        }

        public static void SetLineImmediate(
            LineRenderer lr,
            Vector3 from,
            Vector3 to,
            Color color)
        {
            if (lr == null) return;
            lr.startColor = color;
            lr.endColor = color;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.enabled = true;
        }

        public static void HideLine(LineRenderer lr)
        {
            if (lr != null) lr.enabled = false;
        }


        // ── 2. Physics2D 판정 래퍼 ───────────────────────────────────────

        /// <summary>
        /// CircleCast 단발 판정. 히트 시 IDamageable.TakeDamage 자동 호출.
        /// 반환: 히트 여부 (true = 플레이어 맞음)
        /// </summary>
        public static bool CircleCastAndDamage(
            Vector2 origin,
            Vector2 direction,
            float radius,
            float distance,
            LayerMask playerLayer,
            float damage)
        {
            var hit = Physics2D.CircleCast(origin, radius, direction, distance, playerLayer);
            if (hit.collider == null) return false;

            hit.collider.GetComponent<IDamageable>()?.TakeDamage(damage);
            return true;
        }

        /// <summary>
        /// BoxCast 단발 판정. 두께가 있는 레이저 빔 (좌우 스윕 레이저 등).
        /// </summary>
        public static bool BoxCastAndDamage(
            Vector2 origin,
            Vector2 direction,
            float thickness,
            float distance,
            LayerMask playerLayer,
            float damage)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var size = new Vector2(thickness, thickness);

            var hit = Physics2D.BoxCast(origin, size, angle, direction, distance, playerLayer);
            if (hit.collider == null) return false;

            hit.collider.GetComponent<IDamageable>()?.TakeDamage(damage);
            return true;
        }

        /// <summary>
        /// DPS(초당 데미지) 방식 CircleCast. LateUpdate/루프에서 매 프레임 호출.
        /// damage = dps × Time.deltaTime → 프레임레이트 독립.
        /// </summary>
        public static void CircleCastDPS(
            Vector2 origin,
            Vector2 direction,
            float radius,
            float distance,
            LayerMask playerLayer,
            float dps)
        {
            CircleCastAndDamage(origin, direction, radius, distance, playerLayer,
                                dps * Time.deltaTime);
        }


        // ── 3. 오브젝트 지연 파괴 ────────────────────────────────────────

        public static async UniTaskVoid DestroyAfterDelayAsync(
            GameObject go,
            float delay,
            CancellationToken ct)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delay),
                cancellationToken: ct
            ).SuppressCancellationThrow();

            if (go != null) Object.Destroy(go);
        }


        // ── 4. MoveTowards 이동 ──────────────────────────────────────────

        public static async UniTask MoveTowardsAsync(
            Transform target,
            Vector3 destination,
            float speed,
            CancellationToken ct,
            float arrivalThresholdSqr = 0.0001f)
        {
            while (!ct.IsCancellationRequested)
            {
                target.position = Vector3.MoveTowards(
                    target.position, destination, speed * Time.deltaTime);

                if ((target.position - destination).sqrMagnitude <= arrivalThresholdSqr)
                {
                    target.position = destination;
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }


        // ── 5. 조준 회전 ─────────────────────────────────────────────────

        public static void AimAt2D(Transform self, Vector3 target)
        {
            Vector3 dir = target - self.position;
            if (dir.sqrMagnitude < 0.001f) return;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            self.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
