using System.Collections.Generic;
using UnityEngine;

public class CharacterRig : MonoBehaviour
{
    [SerializeField] SkinnedMeshRenderer baseBody;  // Assign "Base Character Mesh"

    readonly Dictionary<SocketId, Transform> socketMap = new();
    Transform[] bones;

    public SkinnedMeshRenderer BaseBody => baseBody;

    /// <summary>Skeleton shared by every skinned part. Cached — the getter allocates.</summary>
    public Transform[] Bones => bones ??= baseBody.bones;

    void Awake()
    {
        foreach (var s in GetComponentsInChildren<CharacterSocket>(true))
        {
            if (socketMap.ContainsKey(s.Id))
                Debug.LogWarning($"Duplicate socket '{s.Id}'; keeping the first one found.", s);
            else
                socketMap[s.Id] = s.transform;
        }
    }

    public bool TryGetSocket(SocketId id, out Transform socket)
        => socketMap.TryGetValue(id, out socket);

    public Transform ResolveSocketOrWarn(SocketId id, string context)
    {
        if (socketMap.TryGetValue(id, out var socket))
            return socket;
        Debug.LogWarning($"Socket '{id}' not found on the character (from '{context}').", this);
        return null;
    }
}
