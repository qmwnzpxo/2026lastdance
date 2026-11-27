using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using UnMachine.Core;
using UnMachine.Core.Boss;
using Object = UnityEngine.Object;

namespace UnMachine.Boss
{
    /// <summary>
    /// P1 부스터 점프 내려찍기 (boss.md 6장 P1, BACKLOG T46 ✂️).
    /// 웅크림 = Telegraph(0.5초) → 도약 → 화면 밖(몸체 콜라이더 Off + 피해 거부, 자물쇠 없음) 동안 그림자가 플레이어 x 추적 1.2초
    /// → 고정 0.4초 → 낙하(콜라이더 On) → 착지 범위 피해 8 + 좌우 충격파 → 반격 틈 1.2초(Punishable, Timing).
    /// </summary>
    /// <remarks>
    /// 연출(부스터 불꽃·히트스톱·흔들림, T51)은 범위 밖. Rigidbody2D 는 중력 0·보간 없음이라 Transform 을 직접 옮긴다.
    /// 인터럽트로 취소되면 보스를 현재 x 의 바닥으로 내려놓고 콜라이더·피격을 되돌린다.
    /// </remarks>
    public sealed class JumpSlamPattern : IBossPattern, IDisposable
    {
        private const int ShadowSortingOrder = 5;
        private const int ShockwaveSortingOrder = 6;
        private const float PlaceholderPixels = 4f; // Texture2D.whiteTexture 4×4 → 1 Unit 사각형

        private readonly JumpSlamConfig _config;
        private readonly BossController _boss;
        private readonly Transform _bossTransform;
        private readonly Rigidbody2D _body;
        private readonly Animator _animator;
        private readonly Collider2D[] _bodyColliders;
        private readonly bool[] _colliderWasEnabled;
        private readonly JumpSlamModel _model;
        private readonly BossHitBox _hitBox;
        private readonly ObjectPool<BossShockwave> _shockwavePool;
        private readonly List<BossShockwave> _activeShockwaves = new List<BossShockwave>();
        private readonly Action<BossShockwave> _releaseShockwave;
        private readonly int _launchTriggerHash;
        private readonly int _landTriggerHash;
        private readonly bool _hasLaunchTrigger;
        private readonly bool _hasLandTrigger;

        private BossShadowMarker _shadow;
        private Sprite _centerSprite;
        private Sprite _bottomSprite;
        private bool _isOffscreen;
        private Vector2 _lastLandingCenter;
        private Vector2 _lastLandingSize;

        /// <summary>패턴을 만든다. 그림자는 지금 1개 만들고, 충격파는 풀에서 필요할 때 만든다.</summary>
        public JumpSlamPattern(JumpSlamConfig config, BossController boss, Rigidbody2D body, Animator animator)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (boss == null)
            {
                throw new ArgumentNullException(nameof(boss));
            }

            _config = config;
            _boss = boss;
            _bossTransform = boss.transform;
            _body = body;
            _animator = animator;
            _bodyColliders = boss.GetComponents<Collider2D>();
            _colliderWasEnabled = new bool[_bodyColliders.Length];
            _model = config.CreateModel();
            _hitBox = new BossHitBox(config.PlayerLayer);
            _releaseShockwave = ReleaseShockwave;
            Timing = config.ToTiming();

            _hasLaunchTrigger = TryGetTrigger(animator, config.LaunchTrigger, out _launchTriggerHash);
            _hasLandTrigger = TryGetTrigger(animator, config.LandTrigger, out _landTriggerHash);

            _shockwavePool = new ObjectPool<BossShockwave>(
                CreateShockwave,
                wave => wave.gameObject.SetActive(true),
                wave => wave.gameObject.SetActive(false),
                wave =>
                {
                    if (wave != null)
                    {
                        Object.Destroy(wave.gameObject);
                    }
                },
                collectionCheck: false,
                defaultCapacity: 4);

            _shadow = CreateShadow();
            _shadow.Hide();
        }

        /// <inheritdoc />
        public string DebugName => "JumpSlam";

        /// <inheritdoc />
        public PatternTiming Timing { get; }

        /// <summary>디버그: 현재 단계.</summary>
        public JumpSlamPhase Phase => _model.Phase;

        /// <summary>디버그: 날아가는 충격파 수.</summary>
        public int ActiveShockwaveCount => _activeShockwaves.Count;

        /// <summary>디버그: 마지막 착지 판정 박스 (Gizmo 표시용). 착지 전에는 크기 0.</summary>
        public Vector2 LastLandingCenter => _lastLandingCenter;

        /// <summary>디버그: 마지막 착지 판정 박스 크기.</summary>
        public Vector2 LastLandingSize => _lastLandingSize;

        /// <inheritdoc />
        public async UniTask<PatternResult> RunAsync(BossController boss, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            Vector3 start = _bossTransform.position;
            float halfWidth = GetBodyHalfWidth(start, out Vector2 bodyCenterOffset, out Vector2 bodySize);
            float minX = _config.RoomMinX + halfWidth;
            float maxX = _config.RoomMaxX - halfWidth;
            if (maxX < minX)
            {
                minX = maxX = (_config.RoomMinX + _config.RoomMaxX) * 0.5f; // 방이 몸보다 좁음
            }

            _model.Start(start.x, start.y, _config.AirY, minX, maxX);
            StopBody();
            SetTrigger(_hasLaunchTrigger, _launchTriggerHash);
            GameLog.Info($"[JumpSlam] 도약 (x {start.x:F1}, 범위 {minX:F1}~{maxX:F1})", _boss);

            bool isLanded = false;
            JumpSlamPhase lastPhase = _model.Phase;
            try
            {
                while (_model.Phase != JumpSlamPhase.Landed)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);

                    Transform player = _boss.GetPlayer();
                    float playerX = player != null ? player.position.x : float.NaN;
                    _model.Tick(GameClock.World.DeltaTime, playerX);
                    ApplyModel(start.z);

                    if (_model.Phase != lastPhase)
                    {
                        GameLog.Info($"[JumpSlam] {lastPhase} → {_model.Phase} (그림자 x {_model.ShadowX:F1})", _boss);
                        lastPhase = _model.Phase;
                    }
                }

                isLanded = true;
                Land(start.z, bodyCenterOffset, bodySize, halfWidth);
                return PatternResult.Default;
            }
            finally
            {
                if (!isLanded && _bossTransform != null)
                {
                    // 인터럽트(전환·사망): 공중에 남지 않게 현재 x 의 바닥으로
                    _bossTransform.position = new Vector3(_model.Clamp(_bossTransform.position.x), start.y, start.z);
                    GameLog.Info($"[JumpSlam] 취소 ({_model.Phase}) → 바닥으로 복귀", _boss);
                }

                SetOffscreen(false);
                if (_shadow != null)
                {
                    _shadow.Hide();
                }
            }
        }

        /// <summary>날아가는 충격파를 전부 회수한다 (페이즈 전환·사망 정리).</summary>
        public void ClearShockwaves()
        {
            for (int i = _activeShockwaves.Count - 1; i >= 0; i--)
            {
                BossShockwave wave = _activeShockwaves[i];
                if (wave != null)
                {
                    wave.Expire(); // → ReleaseShockwave 가 목록에서 뺀다
                }
            }

            _activeShockwaves.Clear();
        }

        /// <summary>풀·그림자·임시 스프라이트 정리 (Driver.OnDestroy).</summary>
        public void Dispose()
        {
            ClearShockwaves();
            _shockwavePool.Clear();
            if (_shadow != null)
            {
                Object.Destroy(_shadow.gameObject);
                _shadow = null;
            }

            DestroySprite(ref _centerSprite);
            DestroySprite(ref _bottomSprite);
        }

        private void ApplyModel(float z)
        {
            _bossTransform.position = new Vector3(_model.X, _model.Y, z);
            SetOffscreen(_model.IsOffscreen);

            if (_model.IsShadowVisible)
            {
                _shadow.Show(_model.ShadowX, _config.GroundY, _model.IsShadowLocked);
            }
            else
            {
                _shadow.Hide();
            }
        }

        private void Land(float z, Vector2 bodyCenterOffset, Vector2 bodySize, float halfWidth)
        {
            _bossTransform.position = new Vector3(_model.X, _model.Y, z);
            _shadow.Hide();
            SetTrigger(_hasLandTrigger, _landTriggerHash);

            // spec: 착지 지점 몸체 범위 피해 (콜라이더 bounds 는 동기화 전일 수 있어 시작 때 잰 오프셋으로 계산)
            var center = new Vector2(_model.X, _model.Y) + bodyCenterOffset;
            Vector2 size = bodySize;

            // 보스는 중력 0 이라 배치 높이에 떠 있을 수 있다 (테스트 씬 y 3) → 몸체 아래 끝이 바닥보다 높으면
            // 판정을 바닥까지 늘린다. 그대로 두면 바닥에 선 플레이어가 그림자 위에 있어도 피해 0 (씬 테스트 2026-11-27)
            float top = center.y + size.y * 0.5f;
            float bottom = center.y - size.y * 0.5f;
            if (bottom > _config.GroundY && top > _config.GroundY)
            {
                size.y = top - _config.GroundY;
                center.y = (top + _config.GroundY) * 0.5f;
            }

            _lastLandingCenter = center;
            _lastLandingSize = size;
            int hits = _hitBox.DamagePlayers(center, size, _config.LandingDamage);

            // spec: 좌우로 퍼지는 충격파 — 몸 양옆에서 출발
            float waveOffset = halfWidth + _config.ShockwaveWidth * 0.5f;
            SpawnShockwave(_model.X - waveOffset, -1f);
            SpawnShockwave(_model.X + waveOffset, 1f);

            GameLog.Info($"[JumpSlam] 착지 x {_model.X:F1}: 몸체 적중 {hits} (피해 {_config.LandingDamage}), 충격파 2", _boss);
        }

        private void SpawnShockwave(float x, float direction)
        {
            if (x <= _config.RoomMinX || x >= _config.RoomMaxX)
            {
                return; // 벽에 붙어 착지 → 그쪽 충격파 없음
            }

            BossShockwave wave = _shockwavePool.Get();
            _activeShockwaves.Add(wave);
            wave.Launch(x, direction, _config, _hitBox, _config.ShockwavePrefab == null, _releaseShockwave);
        }

        private void ReleaseShockwave(BossShockwave wave)
        {
            _activeShockwaves.Remove(wave);
            if (wave != null)
            {
                _shockwavePool.Release(wave);
            }
        }

        /// <summary>화면 밖: 몸체 콜라이더 Off + 피해 거부 (IsInvulnerable·자물쇠는 그대로). spec: boss.md 6장 P1, 17장 #11</summary>
        private void SetOffscreen(bool isOffscreen)
        {
            if (_isOffscreen == isOffscreen)
            {
                return;
            }

            _isOffscreen = isOffscreen;
            for (int i = 0; i < _bodyColliders.Length; i++)
            {
                Collider2D col = _bodyColliders[i];
                if (col == null)
                {
                    continue;
                }

                if (isOffscreen)
                {
                    _colliderWasEnabled[i] = col.enabled;
                    col.enabled = false;
                }
                else
                {
                    col.enabled = _colliderWasEnabled[i];
                }
            }

            if (_boss != null)
            {
                _boss.SetUntargetable(isOffscreen);
            }
        }

        private void StopBody()
        {
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>몸체 콜라이더(켜진 것) bounds 합의 반폭, 보스 위치 기준 중심 오프셋, 크기. 없으면 1×1.</summary>
        private float GetBodyHalfWidth(Vector3 bossPosition, out Vector2 centerOffset, out Vector2 size)
        {
            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < _bodyColliders.Length; i++)
            {
                Collider2D col = _bodyColliders[i];
                if (col == null || !col.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }

            if (!hasBounds)
            {
                centerOffset = new Vector2(0f, 0.5f);
                size = Vector2.one;
                return 0.5f;
            }

            centerOffset = (Vector2)(bounds.center - bossPosition);
            size = bounds.size;
            return bounds.extents.x;
        }

        private BossShadowMarker CreateShadow()
        {
            if (_config.ShadowPrefab != null)
            {
                return Object.Instantiate(_config.ShadowPrefab);
            }

            // 임시: 아래 끝 기준 사각형 (바닥 위에 얇게), 폭 = 몸 폭
            GetBodyHalfWidth(_bossTransform.position, out _, out Vector2 bodySize);
            var go = new GameObject("BossShadowMarker (임시)");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite(ref _bottomSprite, new Vector2(0.5f, 0f));
            renderer.sortingOrder = ShadowSortingOrder;
            go.transform.localScale = new Vector3(bodySize.x, _config.PlaceholderShadowHeight, 1f);
            return go.AddComponent<BossShadowMarker>(); // Awake 에서 renderer 를 찾는다
        }

        private BossShockwave CreateShockwave()
        {
            if (_config.ShockwavePrefab != null)
            {
                return Object.Instantiate(_config.ShockwavePrefab);
            }

            var go = new GameObject("BossShockwave (임시)");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite(ref _centerSprite, new Vector2(0.5f, 0.5f));
            renderer.color = new Color(1f, 0.6f, 0.2f, 0.85f);
            renderer.sortingOrder = ShockwaveSortingOrder;
            return go.AddComponent<BossShockwave>();
        }

        private static Sprite GetPlaceholderSprite(ref Sprite cache, Vector2 pivot)
        {
            if (cache == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                cache = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), pivot, PlaceholderPixels);
            }

            return cache;
        }

        private static void DestroySprite(ref Sprite sprite)
        {
            if (sprite != null)
            {
                Object.Destroy(sprite);
                sprite = null;
            }
        }

        private void SetTrigger(bool hasTrigger, int hash)
        {
            if (hasTrigger && _animator != null)
            {
                _animator.SetTrigger(hash);
            }
        }

        /// <summary>Animator 에 그 이름의 Trigger 가 있으면 해시를 준다 (T54 전에는 없어서 경고 없이 건너뜀).</summary>
        private static bool TryGetTrigger(Animator animator, string name, out int hash)
        {
            hash = 0;
            if (animator == null || string.IsNullOrEmpty(name) || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            int target = Animator.StringToHash(name);
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == target && parameters[i].type == AnimatorControllerParameterType.Trigger)
                {
                    hash = target;
                    return true;
                }
            }

            GameLog.Info($"[JumpSlam] Animator 에 Trigger '{name}' 없음 → 건너뜀 (T54)", animator);
            return false;
        }
    }
}
