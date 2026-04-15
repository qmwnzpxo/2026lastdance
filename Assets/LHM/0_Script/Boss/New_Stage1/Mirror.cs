using System;
using UnityEngine;

namespace BossSystem
{
    /// <summary>
    /// 2페이즈 반사 레이저 기믹의 거울 컴포넌트.
    ///
    /// ── 45° 스냅 회전 알고리즘 ──────────────────────────────────────────
    ///   공격 방향(로컬 X 부호)으로 회전 방향 결정.
    ///   스냅 수식: snapped = Round(current / 45) × 45 ± 45
    ///   정수 연산으로 float 누적 오차 완전 제거.
    ///
    /// ── 반사 판정 수학 ───────────────────────────────────────────────────
    ///   입사: incidentDir = (mirrorPos - bossPos).normalized
    ///   법선: normal = transform.up  (거울의 월드 up 벡터)
    ///   반사: r = Vector2.Reflect(incidentDir, normal)
    ///           = incidentDir - 2(incidentDir·normal) × normal
    ///   판정: Vector2.Angle(r, dirToBoss) ≤ _angleTolerance
    ///
    /// ── Dirty Flag ──────────────────────────────────────────────────────
    ///   CurrentAngle 프로퍼티 → 상위 패턴이 이전 각도와 비교해 변화 시에만
    ///   레이캐스트 재계산. 매 프레임 Physics2D 호출을 줄임.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Mirror : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Mirror")]
        [Tooltip("플레이어 공격 1회당 회전 단위 (도). 기획: 45°")]
        [SerializeField] private float _snapDegrees      = 45f;

        [Tooltip("반사 성공 허용 각도 오차 (도)")]
        [SerializeField] private float _angleTolerance   = 12f;

        [Header("VFX")]
        [SerializeField] private ParticleSystem _hitVfx;

        // ── 이벤트 ────────────────────────────────────────────────────────
        /// <summary>반사 레이저가 보스에게 도달하면 발생. Phase2MirrorLaserPattern 구독.</summary>
        public event Action OnReflectedToBoss;

        // ── 내부 상태 ─────────────────────────────────────────────────────
        private Transform _bossTransform;
        private bool      _isReflecting;

        /// <summary>
        /// 현재 Z축 회전각 (도).
        /// 상위 패턴의 Dirty Flag 비교에 사용.
        /// </summary>
        public float CurrentAngle => transform.eulerAngles.z;

        // ─────────────────────────────────────────────────────────────────
        // 초기화
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Phase2MirrorLaserPattern에서 Instantiate 직후 호출.</summary>
        public void Initialize(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            AlignToBoss();
        }

        /// <summary>레이저 발사 단계 진입 시 호출. Update 반사 판정 루프 시작.</summary>
        public void StartReflecting() => _isReflecting = true;

        /// <summary>외부(패턴 레이캐스트 로직)에서 반사 성공 통보.</summary>
        public void NotifyReflectedToBoss()
        {
            if (!_isReflecting) return;
            _isReflecting = false;
            OnReflectedToBoss?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_isReflecting) CheckReflection();
        }

        // ─────────────────────────────────────────────────────────────────
        // 플레이어 공격 처리
        // ─────────────────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("PlayerProjectile")) return;

            // 공격 방향을 로컬 공간으로 변환 → x 부호로 회전 방향 결정
            Vector3 worldHitDir  = (transform.position - other.transform.position).normalized;
            Vector3 localHitDir  = transform.InverseTransformDirection(worldHitDir);
            float   rotDir       = localHitDir.x >= 0f ? 1f : -1f;

            SnapRotate(rotDir);
            _hitVfx?.Play();
        }

        // ─────────────────────────────────────────────────────────────────
        // 회전 로직
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 45° 단위 스냅 회전.
        ///
        /// 예) 현재 32° → Round(32/45)×45 = 0° → 우회전 → 0+45 = 45°
        ///     현재 47° → Round(47/45)×45 = 45° → 좌회전 → 45-45 = 0°
        ///
        /// Mathf.RoundToInt로 float 누적 오차 제거.
        /// ((n % 360) + 360) % 360: 음수 모듈러 안전 처리.
        /// </summary>
        private void SnapRotate(float rotDir)
        {
            float current    = transform.eulerAngles.z;
            int   snapBase   = Mathf.RoundToInt(current / _snapDegrees) * (int)_snapDegrees;
            int   newAngle   = snapBase + (int)(rotDir * _snapDegrees);

            newAngle = ((newAngle % 360) + 360) % 360; // 0~359 정규화

            transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }

        /// <summary>초기화 시 보스 방향으로 45° 스냅 정렬.</summary>
        private void AlignToBoss()
        {
            if (_bossTransform == null) return;

            Vector3 dir     = _bossTransform.position - transform.position;
            float   raw     = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            int     snapped = Mathf.RoundToInt(raw / _snapDegrees) * (int)_snapDegrees;

            transform.rotation = Quaternion.Euler(0f, 0f, snapped);
        }

        // ─────────────────────────────────────────────────────────────────
        // 반사 판정
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 매 Update 호출 (반사 활성 시).
        ///
        /// 반사 벡터가 보스 방향과 _angleTolerance 이내이면 파훼 이벤트.
        /// Vector2.Reflect: 수식 r = d - 2(d·n)n (Unity 내장 최적화).
        /// </summary>
        private void CheckReflection()
        {
            if (_bossTransform == null) return;

            // 입사 방향: 보스 → 거울
            Vector2 incident   = ((Vector2)transform.position
                                  - (Vector2)_bossTransform.position).normalized;
            // 거울 법선: 로컬 up → 월드
            Vector2 normal     = transform.up;
            // 반사 벡터
            Vector2 reflected  = Vector2.Reflect(incident, normal);
            // 보스 방향 벡터
            Vector2 toBoss     = ((Vector2)_bossTransform.position
                                  - (Vector2)transform.position).normalized;

            float angle = Vector2.Angle(reflected, toBoss);

            if (angle <= _angleTolerance)
            {
                _isReflecting = false;
                OnReflectedToBoss?.Invoke();
                Debug.Log($"[Mirror] Reflection success! Angle={angle:F1}° / Tolerance={_angleTolerance}°");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 에디터 시각화
        // ─────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 거울 법선
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.up * 1.5f);

            if (_bossTransform == null) return;

            // 반사 벡터
            Vector2 inc = ((Vector2)transform.position - (Vector2)_bossTransform.position).normalized;
            Vector2 ref2 = Vector2.Reflect(inc, transform.up);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, (Vector3)ref2 * 3f);

            // 허용 각도 아크
            Vector2 toBoss = ((Vector2)_bossTransform.position - (Vector2)transform.position).normalized;
            UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.2f);
            UnityEditor.Handles.DrawSolidArc(transform.position, Vector3.forward,
                                              toBoss,  _angleTolerance, 1.5f);
            UnityEditor.Handles.DrawSolidArc(transform.position, Vector3.forward,
                                              toBoss, -_angleTolerance, 1.5f);
        }
#endif
    }
}