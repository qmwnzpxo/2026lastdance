using System;
using UnityEngine;
using UnMachine.Core;
using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// P1 착지 충격파 (풀링). 바닥을 따라 한 방향으로 이동, 플레이어에게 1회 피해, 방 경계에서 풀로 돌아간다.
    /// spec: boss.md 6장 P1 (피해 4, 속도 12, 높이 1 — 점프로 넘으면 0)
    /// </summary>
    /// <remarks>시간은 GameClock World 채널 (히트스톱·슬로우를 따름). 풀 반환은 Launch 때 받은 콜백으로만 한다.</remarks>
    [DisallowMultipleComponent]
    public sealed class BossShockwave : MonoBehaviour
    {
        [Tooltip("비우면 자신·자식에서 찾는다 (임시 사각형은 크기를 폭×높이로 맞춘다)")]
        [SerializeField] private SpriteRenderer _renderer;

        private readonly ShockwaveModel _model = new ShockwaveModel();
        private BossHitBox _hitBox;
        private Action<BossShockwave> _onExpired;
        private float _groundY;
        private float _height;
        private float _width;
        private float _damage;
        private bool _hasHit;
        private bool _isActive;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private void OnDisable()
        {
            // 풀 반환·씬 정리 모두 다음 생애에 남지 않게 초기화
            _isActive = false;
            _onExpired = null;
            _hasHit = false;
        }

        /// <summary>출발시킨다 (풀에서 꺼낸 직후).</summary>
        /// <param name="startX">시작 중심 x.</param>
        /// <param name="direction">+1 오른쪽 / -1 왼쪽.</param>
        /// <param name="config">P1 수치.</param>
        /// <param name="hitBox">공용 판정 (패턴이 소유).</param>
        /// <param name="isPlaceholder">임시 사각형이면 스케일로 크기를 맞춘다.</param>
        /// <param name="onExpired">경계 도달 시 호출 (풀 반환).</param>
        public void Launch(
            float startX,
            float direction,
            JumpSlamConfig config,
            BossHitBox hitBox,
            bool isPlaceholder,
            Action<BossShockwave> onExpired)
        {
            _width = config.ShockwaveWidth;
            _height = config.ShockwaveHeight;
            _damage = config.ShockwaveDamage;
            _groundY = config.GroundY;
            _hitBox = hitBox;
            _onExpired = onExpired;
            _hasHit = false;
            _model.Launch(startX, direction, config.ShockwaveSpeed, _width, config.RoomMinX, config.RoomMaxX);

            Vector3 scale = transform.localScale;
            if (isPlaceholder)
            {
                scale = new Vector3(_width, _height, 1f);
            }

            // 진행 방향으로 뒤집기 (프리팹 그림이 오른쪽 기준이라고 가정)
            scale.x = Mathf.Abs(scale.x) * _model.Direction;
            transform.localScale = scale;
            ApplyPosition();
            _isActive = true;
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            _model.Tick(GameClock.World.DeltaTime);
            ApplyPosition();

            if (!_hasHit && _hitBox != null)
            {
                var center = new Vector2(_model.X, _groundY + _height * 0.5f);
                _hasHit = _hitBox.DamagePlayers(center, new Vector2(_width, _height), _damage) > 0;
                if (_hasHit)
                {
                    GameLog.Info($"[BossShockwave] 플레이어 적중 (피해 {_damage})", this);
                }
            }

            if (_model.IsExpired)
            {
                Expire();
            }
        }

        /// <summary>즉시 소멸 (페이즈 전환·사망 정리).</summary>
        public void Expire()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            Action<BossShockwave> onExpired = _onExpired;
            _onExpired = null;
            onExpired?.Invoke(this);
        }

        private void ApplyPosition()
        {
            transform.position = new Vector3(_model.X, _groundY + _height * 0.5f, transform.position.z);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_isActive)
            {
                return;
            }

            Gizmos.color = _hasHit ? Color.gray : Color.red;
            Gizmos.DrawWireCube(
                new Vector3(_model.X, _groundY + _height * 0.5f, 0f), new Vector3(_width, _height, 0f));
        }
#endif
    }
}
