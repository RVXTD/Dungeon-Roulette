using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayFromEnd : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Jump to the end of this state and play backwards (state speed must be -1)
        animator.Play(stateInfo.shortNameHash, layerIndex, 1f);
        animator.Update(0f); // apply immediately
    }
}