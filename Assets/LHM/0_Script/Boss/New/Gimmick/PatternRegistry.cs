using System;
using System.Collections.Generic;
using UnMachine.Core.Boss;

namespace UnMachine.Boss
{
    /// <summary>
    /// 선택 단위(P1~P6·C1~C4) → 실행할 패턴. 등록되지 않은 단위는 선택기에서 "사용 불가"로 본다. (BACKLOG T38)
    /// </summary>
    public sealed class PatternRegistry
    {
        private readonly Dictionary<SelectionUnit, IBossPattern> _patterns =
            new Dictionary<SelectionUnit, IBossPattern>();

        /// <summary>등록된 단위 수.</summary>
        public int Count => _patterns.Count;

        /// <summary>단위에 패턴을 등록한다. 같은 단위를 두 번 등록하면 ArgumentException.</summary>
        public void Register(SelectionUnit unit, IBossPattern pattern)
        {
            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            if (_patterns.ContainsKey(unit))
            {
                throw new ArgumentException($"단위 {unit} 가 이미 등록되어 있다.", nameof(unit));
            }

            _patterns.Add(unit, pattern);
        }

        /// <summary>단위가 등록되어 있는지.</summary>
        public bool Contains(SelectionUnit unit)
        {
            return _patterns.ContainsKey(unit);
        }

        /// <summary>단위의 패턴을 찾는다.</summary>
        public bool TryGet(SelectionUnit unit, out IBossPattern pattern)
        {
            return _patterns.TryGetValue(unit, out pattern);
        }
    }
}
