using UnityEngine;

/// <summary>
/// The handshake the action components share with their Animator: resolve a state name to a
/// hash once in Awake, then play by hash — a zero hash means the state is missing and the
/// pose is skipped. Keeps every component's fallback behavior identical.
/// </summary>
public static class AnimatorStates
{
    /// <summary>Zero for a state this Animator has not got, a missing layer, or an empty name.
    /// Pass `warnContext` to log the miss; leave it null when the caller escalates itself.</summary>
    public static int ResolveState(this Animator animator, int layer, string stateName, Object warnContext = null)
    {
        if (layer < 0 || string.IsNullOrEmpty(stateName)) return 0;

        int hash = Animator.StringToHash(stateName);
        if (animator.HasState(layer, hash)) return hash;

        if (warnContext != null)
            Debug.LogWarning($"{warnContext.name}: Animator layer '{animator.GetLayerName(layer)}' has no '{stateName}' state; that pose stays unplayed.", warnContext);

        return 0;
    }

    /// <summary>Crossfades to the state, or does nothing for the zero hash ResolveState hands
    /// back for a missing one.</summary>
    public static void PlayState(this Animator animator, int stateHash, float blend, int layer)
    {
        if (stateHash != 0) animator.CrossFadeInFixedTime(stateHash, blend, layer);
    }
}
