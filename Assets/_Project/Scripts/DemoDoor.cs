using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ScifiOffice {
    public class DemoDoor : MonoBehaviour {
        private const string PlayerTag = "Player";
        private static readonly int OpenTriggerHash = Animator.StringToHash("Open");

        private Animator anim;

        private void Awake() {
            TryGetComponent(out anim);
        }

        private void OnTriggerEnter(Collider other) {
            if (anim != null && other != null && other.CompareTag(PlayerTag)) {
                anim.SetTrigger(OpenTriggerHash);
            }
        }
    }
}
