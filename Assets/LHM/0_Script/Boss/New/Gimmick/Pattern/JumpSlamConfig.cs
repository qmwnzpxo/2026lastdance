using UnityEngine;
using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// P1 부스터 점프 내려찍기 수치 (boss.md 6장 P1, BACKLOG T46 ✂️). 방 경계는 Director 없이 여기 x 범위로 정한다.
    /// 초기값은 Test_Boss 아레나(가로 40·세로 30, 바닥 y 0) 기준.
    /// </summary>
    [CreateAssetMenu(menuName = "UnMachine/Config/Jump Slam (P1)", fileName = "JumpSlamConfig")]
    public sealed class JumpSlamConfig : ScriptableObject
    {
        [Header("예고·반격 틈")]
        [Tooltip("예고(초) = 웅크림. spec: boss.md 6장 P1 0.5초")]
        [SerializeField, Min(0f)] private float _telegraphSeconds = 0.5f;

        [Tooltip("착지 후 반격 틈(초, 1페이즈). spec: boss.md 6장 P1 1.2초")]
        [SerializeField, Min(0f)] private float _punishableSeconds = 1.2f;

        [Header("단계 시간 (초, World 채널)")]
        [Tooltip("도약 시간. 〔임시값〕 기획서 '빠르게'")]
        [SerializeField, Min(0f)] private float _riseSeconds = 0.3f;

        [Tooltip("그림자 추적. spec: boss.md 6장 P1 1.2초")]
        [SerializeField, Min(0f)] private float _trackSeconds = 1.2f;

        [Tooltip("그림자 고정. spec: boss.md 6장 P1 0.4초")]
        [SerializeField, Min(0f)] private float _lockSeconds = 0.4f;

        [Tooltip("낙하 시간. 〔임시값〕 기획서 값 없음")]
        [SerializeField, Min(0f)] private float _fallSeconds = 0.2f;

        [Header("방 (Director 없음)")]
        [Tooltip("방 왼쪽 벽 x. 보스 x 는 여기 + 몸 반폭까지, 충격파는 여기서 소멸")]
        [SerializeField] private float _roomMinX = -20f;

        [Tooltip("방 오른쪽 벽 x")]
        [SerializeField] private float _roomMaxX = 20f;

        [Tooltip("바닥 y (그림자·충격파 아래 끝)")]
        [SerializeField] private float _groundY;

        [Tooltip("천장 y")]
        [SerializeField] private float _ceilingY = 30f;

        [Tooltip("천장 위로 올라가는 높이. spec: boss.md 4장 천장 + 5 Unit")]
        [SerializeField, Min(0f)] private float _aboveCeiling = 5f;

        [Header("판정")]
        [Tooltip("플레이어 레이어. 비우면 'player'")]
        [SerializeField] private LayerMask _playerLayer;

        [Tooltip("착지 몸체 범위 피해. spec: boss.md 6장 P1 8")]
        [SerializeField, Min(0f)] private float _landingDamage = 8f;

        [Tooltip("충격파 피해. spec: boss.md 6장 P1 4")]
        [SerializeField, Min(0f)] private float _shockwaveDamage = 4f;

        [Tooltip("충격파 속도 (Unit/초). spec: boss.md 6장 P1 12")]
        [SerializeField, Min(0f)] private float _shockwaveSpeed = 12f;

        [Tooltip("충격파 높이 (바닥부터). 플레이어 점프로 넘을 수 있게. spec: boss.md 6장 P1 1")]
        [SerializeField, Min(0f)] private float _shockwaveHeight = 1f;

        [Tooltip("충격파 폭. 〔임시값〕")]
        [SerializeField, Min(0f)] private float _shockwaveWidth = 1f;

        [Header("표시 (비우면 임시 사각형)")]
        [SerializeField] private BossShadowMarker _shadowPrefab;
        [SerializeField] private BossShockwave _shockwavePrefab;

        [Tooltip("임시 그림자 높이")]
        [SerializeField, Min(0.01f)] private float _placeholderShadowHeight = 0.3f;

        [Header("Animator 트리거 (없으면 건너뜀, T54)")]
        [SerializeField] private string _launchTrigger = "JumpSlamLaunch";
        [SerializeField] private string _landTrigger = "JumpSlamLand";

        public float TelegraphSeconds => _telegraphSeconds;
        public float PunishableSeconds => _punishableSeconds;
        public float RoomMinX => _roomMinX;
        public float RoomMaxX => _roomMaxX;
        public float GroundY => _groundY;
        public float AirY => _ceilingY + _aboveCeiling;
        public float LandingDamage => _landingDamage;
        public float ShockwaveDamage => _shockwaveDamage;
        public float ShockwaveSpeed => _shockwaveSpeed;
        public float ShockwaveHeight => _shockwaveHeight;
        public float ShockwaveWidth => _shockwaveWidth;
        public BossShadowMarker ShadowPrefab => _shadowPrefab;
        public BossShockwave ShockwavePrefab => _shockwavePrefab;
        public float PlaceholderShadowHeight => _placeholderShadowHeight;
        public string LaunchTrigger => _launchTrigger;
        public string LandTrigger => _landTrigger;

        /// <summary>플레이어 레이어 (비어 있으면 'player').</summary>
        public LayerMask PlayerLayer => _playerLayer.value != 0 ? _playerLayer : (LayerMask)LayerMask.GetMask("player");

        /// <summary>예고·반격 틈.</summary>
        public PatternTiming ToTiming()
        {
            return new PatternTiming(_telegraphSeconds, _punishableSeconds);
        }

        /// <summary>이 값으로 Core 모델을 만든다.</summary>
        public JumpSlamModel CreateModel()
        {
            return new JumpSlamModel(_riseSeconds, _trackSeconds, _lockSeconds, _fallSeconds);
        }
    }
}
