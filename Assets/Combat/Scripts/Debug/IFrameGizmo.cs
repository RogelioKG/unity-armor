#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

// While the dodge is invulnerable, the whole body is tinted translucent red.
[RequireComponent(typeof(PlayerDodge))]
public class IFrameGizmo : MonoBehaviour
{
    [Tooltip("Body tint while invulnerable. Keep the alpha low so the pose stays readable under it.")]
    [SerializeField] private Color tint = new(1f, 0f, 0f, 0.35f);

    private readonly List<SkinnedMeshRenderer> skinnedRenderers = new();
    private readonly List<Mesh> bakedPool = new();

    private void OnDestroy()
    {
        foreach (Mesh baked in bakedPool) DestroyImmediate(baked);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !TryGetComponent(out PlayerDodge dodge) || !dodge.IsInvulnerable) return;

        Gizmos.color = tint;

        GetComponentsInChildren(skinnedRenderers);

        for (int i = 0; i < skinnedRenderers.Count; i++)
        {
            SkinnedMeshRenderer skinned = skinnedRenderers[i];
            if (!skinned.enabled) continue;

            // A skipped disabled renderer leaves a gap, so top up rather than add one.
            while (bakedPool.Count <= i) bakedPool.Add(new Mesh { hideFlags = HideFlags.HideAndDontSave });

            skinned.BakeMesh(bakedPool[i]);
            Gizmos.DrawMesh(bakedPool[i], skinned.transform.position, skinned.transform.rotation);
        }
    }
}
#endif
