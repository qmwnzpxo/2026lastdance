using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// TMP_Text 버텍스를 매 프레임 조작해 애니메이션 이펙트를 구현.
    /// DialogueUI가 자동으로 추가하므로 수동 추가 불필요.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextEffectAnimator : MonoBehaviour
    {
        // ── 이펙트 파라미터 (인스펙터에서 튜닝 가능) ─────────────────────────

        [Header("Shake (흔들기)")]
        [Tooltip("흔들림 진폭 (픽셀)")]
        public float shakeAmplitude = 2.5f;
        [Tooltip("흔들림 속도")]
        public float shakeFrequency = 35f;

        [Header("Wave (물결)")]
        [Tooltip("물결 높이 (픽셀)")]
        public float waveAmplitude = 5f;
        [Tooltip("글자 간 위상 차")]
        public float wavePhaseOffset = 0.45f;
        [Tooltip("물결 속도")]
        public float waveSpeed = 2f;

        [Header("BigPop (크기 변화)")]
        [Tooltip("최대 확대 비율 (1.0 = 원본)")]
        public float bigPopMaxScale = 1.5f;
        [Tooltip("팝 애니메이션 속도")]
        public float bigPopSpeed = 2.5f;

        [Header("Break (산산조각)")]
        [Tooltip("글자 흩어지는 반경")]
        public float breakScatterRadius = 6f;
        [Tooltip("글자 회전 속도 (도/초)")]
        public float breakRotationSpeed = 180f;
        [Tooltip("글자 진동 속도")]
        public float breakVibrateSpeed = 5f;

        [Header("Rainbow (무지개)")]
        [Tooltip("색상 순환 속도")]
        public float rainbowSpeed = 0.8f;
        [Tooltip("글자 간 색상 오프셋")]
        public float rainbowOffset = 0.12f;

        // ── 런타임 상태 ────────────────────────────────────────────────────────

        private TMP_Text _tmp;
        private List<EffectRegion> _effects = new();
        private float _time;
        private bool _active;

        // ── 초기화 ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _tmp = GetComponent<TMP_Text>();
        }

        // ── 공개 API ───────────────────────────────────────────────────────────

        /// <summary>적용할 이펙트 목록 설정. 빈 목록이면 애니메이션 중지.</summary>
        public void SetEffects(List<EffectRegion> effects)
        {
            _effects = effects ?? new List<EffectRegion>();
            _active  = _effects.Count > 0;
            _time    = 0f;
        }

        public void ClearEffects()
        {
            _effects.Clear();
            _active = false;
        }

        // ── 업데이트 ───────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_active || _effects.Count == 0) return;
            _time += Time.deltaTime;
            Animate();
        }

        private void Animate()
        {
            _tmp.ForceMeshUpdate();
            var info = _tmp.textInfo;
            if (info == null || info.characterCount == 0) return;

            // 원본 버텍스 데이터를 작업용 배열에 복사 (Dynamic 폰트 안전 처리)
            info.CopyMeshInfoVertexData();

            foreach (var region in _effects)
            {
                if (region.type is TextEffectType.Typefast or TextEffectType.Typeslow)
                    continue;

                int end = Mathf.Min(region.endChar, info.characterCount - 1);
                for (int ci = region.startChar; ci <= end; ci++)
                {
                    var charInfo = info.characterInfo[ci];

                    // isVisible: TMP가 실제로 렌더링하는 글자만 처리
                    if (!charInfo.isVisible) continue;

                    int mat = charInfo.materialReferenceIndex;
                    int vi  = charInfo.vertexIndex;

                    // meshInfo 배열 범위 체크
                    if (mat >= info.meshInfo.Length) continue;
                    var verts  = info.meshInfo[mat].vertices;
                    var colors = info.meshInfo[mat].colors32;
                    if (vi + 3 >= verts.Length) continue;

                    Vector3 center = (verts[vi] + verts[vi + 1] + verts[vi + 2] + verts[vi + 3]) * 0.25f;

                    switch (region.type)
                    {
                        case TextEffectType.Shake:   Shake(verts, vi, ci);           break;
                        case TextEffectType.Wave:    Wave(verts, vi, ci);            break;
                        case TextEffectType.BigPop:  BigPop(verts, vi, ci, center);  break;
                        case TextEffectType.Break:   Break(verts, vi, ci, center);   break;
                        case TextEffectType.Rainbow: Rainbow(colors, vi, ci);        break;
                    }
                }
            }

            // Unity 6 TMP 권장 방식으로 메시 업데이트
            _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices |
                                  TMP_VertexDataUpdateFlags.Colors32);
        }

        // ── 각 이펙트 구현 ─────────────────────────────────────────────────────

        private void Shake(Vector3[] v, int vi, int ci)
        {
            float seed = ci * 1.91f;
            float dx = Mathf.Sin(_time * shakeFrequency + seed)        * shakeAmplitude;
            float dy = Mathf.Cos(_time * shakeFrequency * 1.4f + seed) * shakeAmplitude;
            var   d  = new Vector3(dx, dy, 0f);
            for (int k = 0; k < 4; k++) v[vi + k] += d;
        }

        private void Wave(Vector3[] v, int vi, int ci)
        {
            float phase = ci * wavePhaseOffset;
            float dy    = Mathf.Sin(_time * waveSpeed * Mathf.PI * 2f + phase) * waveAmplitude;
            var   d     = new Vector3(0f, dy, 0f);
            for (int k = 0; k < 4; k++) v[vi + k] += d;
        }

        private void BigPop(Vector3[] v, int vi, int ci, Vector3 center)
        {
            float phase  = ci * 0.7f;
            float t      = Mathf.Sin(_time * bigPopSpeed * Mathf.PI + phase) * 0.5f + 0.5f;
            float scale  = Mathf.Lerp(1f, bigPopMaxScale, t);
            for (int k = 0; k < 4; k++)
                v[vi + k] = center + (v[vi + k] - center) * scale;
        }

        private void Break(Vector3[] v, int vi, int ci, Vector3 center)
        {
            float seed   = ci * 2.37f;

            // 공전 산란
            float angle  = (_time * breakVibrateSpeed + seed) * Mathf.PI * 2f;
            float radius = Mathf.Abs(Mathf.Sin(_time * 3.1f + seed)) * breakScatterRadius;
            var   scatter = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            // 글자 자체 회전
            float rotDeg = _time * breakRotationSpeed + seed * 60f;
            float rotRad = rotDeg * Mathf.Deg2Rad;
            float cos    = Mathf.Cos(rotRad);
            float sin    = Mathf.Sin(rotRad);

            for (int k = 0; k < 4; k++)
            {
                var local = v[vi + k] - center;
                var rot   = new Vector3(local.x * cos - local.y * sin,
                                        local.x * sin + local.y * cos, 0f);
                v[vi + k] = center + rot + scatter;
            }
        }

        private void Rainbow(Color32[] colors, int vi, int ci)
        {
            float hue = (_time * rainbowSpeed + ci * rainbowOffset) % 1f;
            if (hue < 0f) hue += 1f;
            var c32 = (Color32)Color.HSVToRGB(hue, 1f, 1f);
            for (int k = 0; k < 4; k++) colors[vi + k] = c32;
        }
    }
}
