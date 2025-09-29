using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationScript : MonoBehaviour
{
    [HideInInspector]
    public bool playAnim,isPlaying,reverse,reset;
    public void PlayAnimation() 
    {
        playAnim = true;
    }
    public void ReverseAnimation() 
    {
        reverse = true;
        playAnim = true;
    }
    public void ResetAnimation() 
    {
        reset = true;
    }
}
