using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGemCo2DControl
{
    /// <summary>
    /// 상호작용 입력 처리 핸들러
    /// - Scanner 후보 중 사용 가능 & 우선순위가 가장 높은 대상을 선택
    /// - Begin/End 토글
    /// </summary>
    internal sealed class InteractionInputHandler
    {
        private readonly InteractionScanner2D _scanner;
        private IInteraction _current;

        public InteractionInputHandler(InteractionScanner2D scanner)
        {
            _scanner = scanner;
        }

        public IInteraction Current => _current;

        public void ClearIfEnded(IInteraction ended)
        {
            if (_current == ended) _current = null;
        }

        public void ClearIfSame(Object obj)
        {
            if (_current == obj) _current = null;
        }

        public void Handle(
            InputAction.CallbackContext ctx,
            GameObject interactor,
            CharacterBase character,
            System.Func<bool> isWallLocked,
            System.Func<bool> isBlockedByState)
        {
            if (character.IsStatusDead()) return;
            if (isWallLocked != null && isWallLocked()) return;
            if (isBlockedByState != null && isBlockedByState()) return;

            if (_scanner == null)
            {
                GcLogger.Log("scanner가 없습니다.");
                return;
            }

            // 1) 이미 상호작용 중이면 종료
            if (_current != null)
            {
                _current.EndInteract(interactor);
                _current = null;
                return;
            }

            // 2) 후보 중 사용 가능 & 최상위 우선순위 선택 (LINQ 제거)
            IInteraction best = null;
            int bestPriority = int.MaxValue;

            // Candidates 타입이 List일 수도, IEnumerable일 수도 있으므로 foreach 기반으로 순회합니다.
            IEnumerable<IInteraction> candidates = _scanner.Candidates;
            if (candidates == null)
            {
                GcLogger.Log("scanner에 object가 없습니다.");
                return;
            }

            foreach (var c in candidates)
            {
                if (c == null) continue;
                if (!c.IsAvailable(interactor)) continue;

                // Priority가 낮을수록 우선
                if (c.Priority < bestPriority)
                {
                    bestPriority = c.Priority;
                    best = c;
                }
            }

            if (best == null)
            {
                GcLogger.Log("scanner에 object가 없습니다.");
                return;
            }

            // 3) BeginInteract 성공 시 현재 상호작용으로 고정
            if (best.BeginInteract(interactor))
            {
                _current = best;
            }
        }
    }
}
