using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThisPageAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private List<AnimationScript> animList = new List<AnimationScript>();
    [SerializeField] private float startDelay = 0f; // Time before the first animation starts
    [SerializeField] private float animationInterval = 0.5f; // Time between animations

    /// <summary>
    /// Starts the page animations with delays.
    /// </summary>
    public void StartAnim()
    {
        StartCoroutine(PlayAnimationsRoutine());
    }

    private void Start()
    {
        ResetAnim(); // Ensures all animations are reset when the script starts
    }

    /// <summary>
    /// Coroutine to play animations sequentially with delays.
    /// </summary>
    private IEnumerator PlayAnimationsRoutine()
    {
        ResetAnim(); // Reset animations before starting
        yield return new WaitForSeconds(startDelay); // Wait before starting animations

        foreach (var animation in animList)
        {
            animation.PlayAnimation(); // Trigger the animation
            yield return new WaitForSeconds(animationInterval); // Wait before the next animation
        }
    }

    /// <summary>
    /// Resets all animations in the list.
    /// </summary>
    public void ResetAnim()
    {
        foreach (var animation in animList)
        {
            animation.ResetAnimation();
        }
    }
}
