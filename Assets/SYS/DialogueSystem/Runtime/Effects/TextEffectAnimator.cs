using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// TMP_Text(TextMeshPro) 컴포넌트의 버텍스(정점) 데이터를 매 프레임 직접 조작하여
    /// Shake·Wave·BigPop·Break·Rainbow 등의 텍스트 애니메이션 이펙트를 구현함
    ///
    /// # 동작 원리
    ///   TMP는 텍스트를 렌더링할 때 각 글자를 사각형(Quad) 단위로 관리한다.
    ///   하나의 사각형은 4개의 버텍스(좌하·좌상·우상·우하)로 구성되며,
    ///   이 버텍스 좌표를 런타임에 수정하면 글자의 위치·크기·회전 등을 바꿀 수 있다.
    ///   이 클래스는 그 원리를 이용해 이펙트 구간(EffectRegion) 별로 다른 변환을 적용한다.
    ///
    /// # 사용 방법
    ///   DialogueUI가 자동으로 추가 받도록 설정해둠 
    ///   SetEffects(List&lt;EffectRegion&gt;) 로 이펙트 목록을 전달하면 즉시 애니메이션이 시작됨
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]  //tmp 자동추가
    public class TextEffectAnimator : MonoBehaviour
    {
        // 이팩트 파라미터

        [Header("Shake (흔들기)")]
        [Tooltip("흔들림 진폭 (픽셀 단위). 값이 클수록 더 크게 흔들린다.")]
        public float shakeAmplitude = 2.5f;

        [Tooltip("흔들림 속도 (Hz 단위). 값이 클수록 더 빠르게 흔들린다.")]
        public float shakeFrequency = 35f;

        [Header("Wave (물결)")]
        [Tooltip("물결의 최대 상하 변위 (픽셀 단위).")]
        public float waveAmplitude = 5f;

        [Tooltip("이웃 글자 간 사인파의 위상 차(라디안). 값이 클수록 물결이 촘촘해진다.")]
        public float wavePhaseOffset = 0.45f;

        [Tooltip("물결이 흘러가는 속도 (사이클/초).")]
        public float waveSpeed = 2f;

        [Header("BigPop (크기 변화)")]
        [Tooltip("가장 크게 확대됐을 때의 스케일 배율. 1.0이면 원본 크기, 1.5면 1.5배.")]
        public float bigPopMaxScale = 1.5f;

        [Tooltip("확대·축소를 반복하는 속도 (사이클/초).")]
        public float bigPopSpeed = 2.5f;

        [Header("Break (산산조각)")]
        [Tooltip("글자가 중심에서 흩어지는 최대 반경 (픽셀 단위).")]
        public float breakScatterRadius = 6f;

        [Tooltip("글자 자체가 스스로 회전하는 각속도 (도/초).")]
        public float breakRotationSpeed = 180f;

        [Tooltip("글자가 궤도를 따라 진동하는 주기 속도 (사이클/초).")]
        public float breakVibrateSpeed = 5f;

        [Header("Rainbow (무지개)")]
        [Tooltip("HSV 색상환을 순환하는 속도 (사이클/초). 0이면 정지.")]
        public float rainbowSpeed = 0.8f;

        [Tooltip("이웃 글자 간 HSV 색상(Hue) 오프셋. 값이 클수록 색이 더 많이 퍼진다.")]
        public float rainbowOffset = 0.12f;

        // ────────────────────────────────────────────────────────────────────────
        // 런타임 상태 필드
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>TMP_Text 컴포넌트 캐시. Awake에서 1회 할당 후 재사용.</summary>
        private TMP_Text _tmp;

        /// <summary>
        /// 현재 적용 중인 이펙트 구간 목록.
        /// EffectRegion은 (시작 글자 인덱스, 끝 글자 인덱스, 이펙트 종류) 를 담는 구조체
        /// </summary>
        private List<EffectRegion> _effects = new();

        /// <summary>Update에서 누적되는 경과 시간. 사인파 등의 시간 입력에 사용</summary>
        private float _time;

        /// <summary>이펙트가 하나라도 존재할 때 true. false면 Update 루프를 건너뜀</summary>
        private bool _active;

        // ────────────────────────────────────────────────────────────────────────
        // Unity 생명주기
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [Unity 생명주기] 오브젝트 생성 시 1회 호출
        /// </summary>
        /// 
        private void Awake()
        {
            _tmp = GetComponent<TMP_Text>();
        }

        // ────────────────────────────────────────────────────────────────────────
        // 공개 API
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 애니메이션할 이펙트 목록을 설정하고 재생을 시작한다
        ///   null 또는 빈 목록을 넘기면 애니메이션이 중지된다
        /// </summary>

        public void SetEffects(List<EffectRegion> effects)
        {
            _effects = effects ?? new List<EffectRegion>(); // null 방어
            _active = _effects.Count > 0;                 // 목록이 비어 있으면 비활성
            _time = 0f;                                  // 시간 리셋 → 애니메이션 처음부터 재생
        }

        /// <summary>
        /// 모든 이펙트를 제거하고 애니메이션을 즉시 중지한다.
        /// </summary>
        public void ClearEffects()
        {
            _effects.Clear();
            _active = false;
        }

        // ────────────────────────────────────────────────────────────────────────
        // 업데이트 & 애니메이션 메인 루프
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [Unity 생명주기] 매 프레임 호출.
        /// _active가 false이거나 이펙트 목록이 비어 있으면 즉시 반환하여 성능 낭비를 방지한다.
        /// </summary>
        private void Update()
        {
            if (!_active || _effects.Count == 0) return;
            _time += Time.deltaTime;  // 프레임 간격만큼 시간 누적
            Animate();
        }

        /// <summary>
        /// TMP 메시 데이터를 직접 수정하여 모든 이펙트 구간에 애니메이션을 적용한다.
        ///
        /// ■ 처리 순서
        ///   1. ForceMeshUpdate()  → TMP가 최신 레이아웃으로 메시를 재계산하도록 강제.
        ///   2. CopyMeshInfoVertexData() → 원본 버텍스를 작업용 배열에 복사.
        ///      (Dynamic 폰트 아틀라스 갱신 시 원본이 덮어써지는 문제를 방지)
        ///   3. 각 이펙트 구간을 순회하며 글자별로 이펙트 함수 호출.
        ///   4. UpdateVertexData() → 수정된 버텍스·컬러 데이터를 GPU로 업로드.
        /// </summary>
        private void Animate()
        {
            // TMP에게 레이아웃을 즉시 재계산하도록 요청
            _tmp.ForceMeshUpdate();

            var info = _tmp.textInfo;

            // 아직 렌더링할 글자가 없으면 처리 중단
            if (info == null || info.characterCount == 0) return;

            // Dynamic 폰트 아틀라스 재구성 시 원본 버텍스가 덮어써질 수 있으므로
            // 작업 전에 모든 materialInfo의 버텍스를 별도 배열로 복사해 둔다.
            info.CopyMeshInfoVertexData();

            foreach (var region in _effects)
            {
                // Typefast·Typeslow는 버텍스 조작이 아닌 타이핑 속도 제어 이펙트이므로 건너뜀
                if (region.type is TextEffectType.Typefast or TextEffectType.Typeslow)
                    continue;

                // 이펙트 구간의 끝 인덱스가 실제 글자 수를 초과하지 않도록 클램프
                int end = Mathf.Min(region.endChar, info.characterCount - 1);

                for (int ci = region.startChar; ci <= end; ci++)
                {
                    var charInfo = info.characterInfo[ci];

                    // isVisible == false : 공백, 줄바꿈 등 실제로 그려지지 않는 글자 → 건너뜀
                    if (!charInfo.isVisible) continue;

                    // TMP는 하나의 텍스트 컴포넌트가 여러 Material(폰트 아틀라스)을 가질 수 있으므로
                    // 각 글자가 속한 Material 인덱스를 확인해야 한다.
                    int mat = charInfo.materialReferenceIndex;
                    // vertexIndex: 이 글자의 첫 번째 버텍스 위치 (0, 1, 2, 3 = 사각형 4꼭짓점)
                    int vi = charInfo.vertexIndex;

                    // 배열 범위 초과 방어 (폰트 아틀라스 갱신 타이밍 등으로 발생 가능)
                    if (mat >= info.meshInfo.Length) continue;
                    var verts = info.meshInfo[mat].vertices;
                    var colors = info.meshInfo[mat].colors32;
                    if (vi + 3 >= verts.Length) continue;  // 4개 버텍스 모두 유효한지 확인

                    // 글자 사각형의 기하학적 중심 (스케일·회전 기준점으로 사용)
                    Vector3 center = (verts[vi] + verts[vi + 1] + verts[vi + 2] + verts[vi + 3]) * 0.25f;

                    // 이펙트 타입에 따라 해당 처리 함수로 분기
                    switch (region.type)
                    {
                        case TextEffectType.Shake: Shake(verts, vi, ci); break;
                        case TextEffectType.Wave: Wave(verts, vi, ci); break;
                        case TextEffectType.BigPop: BigPop(verts, vi, ci, center); break;
                        case TextEffectType.Break: Break(verts, vi, ci, center); break;
                        case TextEffectType.Rainbow: Rainbow(colors, vi, ci); break;
                    }
                }
            }

            // 수정된 버텍스 위치와 컬러 데이터만 GPU에 재업로드 (Unity 6 TMP 권장 방식)
            // 전체 메시를 재생성하지 않으므로 성능 오버헤드가 작다.
            _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices |
                                  TMP_VertexDataUpdateFlags.Colors32);
        }

        // ────────────────────────────────────────────────────────────────────────
        // 개별 이펙트 구현 메서드
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [Shake] 글자를 무작위처럼 보이는 패턴으로 상하좌우 진동시킨다.
        ///
        /// 구현:
        ///   sin/cos에 글자 인덱스(ci)를 시드로 넣어 글자마다 다른 위상을 만든다.
        ///   → 모든 글자가 같은 방향으로 움직이지 않아 '흔들리는' 느낌이 생긴다.
        /// </summary>
        /// <param name="v">현재 materialReferenceIndex의 버텍스 배열</param>
        /// <param name="vi">이 글자의 첫 번째 버텍스 인덱스</param>
        /// <param name="ci">텍스트 내 글자 인덱스 (위상 시드로 사용)</param>
        private void Shake(Vector3[] v, int vi, int ci)
        {
            float seed = ci * 1.91f;  // 글자마다 고유한 위상 시드 (소수 배율 → 반복 패턴 최소화)
            float dx = Mathf.Sin(_time * shakeFrequency + seed) * shakeAmplitude;  // X축 변위
            float dy = Mathf.Cos(_time * shakeFrequency * 1.4f + seed) * shakeAmplitude; // Y축 변위 (배율 1.4: X와 주기 차별화)
            var d = new Vector3(dx, dy, 0f);

            // 사각형의 4개 버텍스 모두 동일하게 이동 (글자 형태 유지)
            for (int k = 0; k < 4; k++) v[vi + k] += d;
        }

        /// <summary>
        /// [Wave] 글자들이 좌→우 방향으로 흐르는 사인파 물결처럼 상하로 움직인다.
        ///
        /// 구현:
        ///   각 글자의 Y 오프셋을 sin(시간 + 글자인덱스 × 위상차) 로 계산한다.
        ///   wavePhaseOffset이 크면 한 화면에 더 많은 파동 마루/골이 보인다.
        /// </summary>
        private void Wave(Vector3[] v, int vi, int ci)
        {
            float phase = ci * wavePhaseOffset;                                            // 글자 간 위상 차
            float dy = Mathf.Sin(_time * waveSpeed * Mathf.PI * 2f + phase) * waveAmplitude; // Y 변위
            var d = new Vector3(0f, dy, 0f);
            for (int k = 0; k < 4; k++) v[vi + k] += d;
        }

        /// <summary>
        /// [BigPop] 글자가 사인파 곡선을 따라 주기적으로 확대·축소를 반복한다.
        ///
        /// 구현:
        ///   sin 값(-1~1)을 0~1 범위로 정규화한 뒤 Lerp로 1.0(원본)~bigPopMaxScale 사이를 보간.
        ///   각 버텍스를 중심점 기준으로 scale 배율만큼 방사형으로 이동시켜 크기 변화를 표현한다.
        /// </summary>
        /// <param name="center">글자 사각형의 기하학적 중심 (스케일 기준점)</param>
        private void BigPop(Vector3[] v, int vi, int ci, Vector3 center)
        {
            float phase = ci * 0.7f;                                           // 글자 간 위상 차 (물결처럼 순서대로 팝)
            float t = Mathf.Sin(_time * bigPopSpeed * Mathf.PI + phase)    // -1 ~ 1
                          * 0.5f + 0.5f;                                        // → 0,1 정규화
            float scale = Mathf.Lerp(1f, bigPopMaxScale, t);                   // 1.0 ~ bigPopMaxScale 보간 애니메이션~

            // 각 버텍스를 중심에서의 방향으로 scale 배율만큼 이동
            for (int k = 0; k < 4; k++)
                v[vi + k] = center + (v[vi + k] - center) * scale;
        }

        /// <summary>
        /// [Break] 글자가 중심 주위를 불규칙하게 공전하면서 스스로 회전한다.
        ///
        /// 구현 (2단계):
        ///   1. 공전 산란: sin으로 반경을 변화시키고 cos/sin으로 타원 궤도를 계산 → scatter 벡터
        ///   2. 자전: 중심 기준으로 2D 회전 행렬을 직접 계산하여 각 버텍스에 적용
        ///   두 변환을 합산하여 '파괴된' 느낌을 연출한다.
        /// </summary>
        private void Break(Vector3[] v, int vi, int ci, Vector3 center)
        {
            float seed = ci * 2.37f;  // 글자마다 다른 위상 시드

            // ── 1단계: 공전 산란 ──────────────────────────────────────────────
            float angle = (_time * breakVibrateSpeed + seed) * Mathf.PI * 2f; // 공전 각도 (라디안)
            // Abs(sin(...))으로 반경을 0~breakScatterRadius 사이에서 맥동(pulsate)시킴
            float radius = Mathf.Abs(Mathf.Sin(_time * 3.1f + seed)) * breakScatterRadius;
            var scatter = new Vector3(Mathf.Cos(angle) * radius,
                                        Mathf.Sin(angle) * radius, 0f);

            // ── 2단계: 자전 (2D 회전 행렬) ───────────────────────────────────
            float rotDeg = _time * breakRotationSpeed + seed * 60f; // 현재 회전 각도 (도)
            float rotRad = rotDeg * Mathf.Deg2Rad;                  // 라디안 변환
            float cos = Mathf.Cos(rotRad);
            float sin = Mathf.Sin(rotRad);

            for (int k = 0; k < 4; k++)
            {
                var local = v[vi + k] - center;  // 중심 기준 로컬 좌표

                // 2D 회전 행렬 적용: [cos -sin] [x]
                //                    [sin  cos] [y]
                var rot = new Vector3(local.x * cos - local.y * sin,
                                      local.x * sin + local.y * cos, 0f);

                // 자전 결과 + 공전 산란을 합산하여 최종 위치 결정
                v[vi + k] = center + rot + scatter;
            }
        }

        /// <summary>
        /// [Rainbow] 글자의 색상을 HSV 색상환을 따라 무한히 순환시킨다.
        ///
        /// 구현:
        ///   시간과 글자 인덱스를 이용해 Hue(색조) 값을 계산하고,
        ///   HSV → RGB 변환으로 Color32를 만들어 4개 버텍스에 적용한다.
        ///   색조는 0~1 범위로 모듈로(%) 연산 후 음수 보정하여 항상 유효한 값을 유지한다.
        /// </summary>
        /// <param name="colors">현재 materialReferenceIndex의 컬러 배열</param>
        private void Rainbow(Color32[] colors, int vi, int ci)
        {
            // 시간 × 속도 + 글자 인덱스 × 오프셋 → HSV Hue (0~1 범위로 정규화)
            float hue = (_time * rainbowSpeed + ci * rainbowOffset) % 1f;
            if (hue < 0f) hue += 1f;                          // 음수 모듈로 보정

            // HSV(hue, 채도=1, 명도=1) → RGB → Color32 변환
            var c32 = (Color32)Color.HSVToRGB(hue, 1f, 1f);

            // 4개 버텍스 모두 같은 색 적용 (글자 전체가 단색)
            for (int k = 0; k < 4; k++) colors[vi + k] = c32;
        }
    }
}