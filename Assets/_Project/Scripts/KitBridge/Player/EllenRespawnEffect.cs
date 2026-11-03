// Cópia do kit: usa o PlayerController cooperativo.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameYunit.KitBridge
{
    public class EllenRespawnEffect : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.GetComponent<PlayerController>().Respawn();
        }
    } 
}