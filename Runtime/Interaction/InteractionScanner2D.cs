using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DControl
{
    /// 플레이어에 부착. Trigger로 들어온 IInteractable들을 추적
    [RequireComponent(typeof(Collider2D))]
    public class InteractionScanner2D : MonoBehaviour
    {
        private readonly HashSet<IInteraction> _candidates = new();
        public IEnumerable<IInteraction> Candidates => _candidates;

        private void OnTriggerEnter2D(Collider2D other)
        {
            var interactable = other.GetComponent<IInteraction>();
            if (interactable != null) _candidates.Add(interactable);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var interactable = other.GetComponent<IInteraction>();
            if (interactable != null) _candidates.Remove(interactable);
        }
    }
}