#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

// Axes for every CharacterSocket under this character, so sockets stay visible while posing them.
public class SocketGizmo : MonoBehaviour
{
    [SerializeField] private float axisLength = 0.05f;

    private readonly List<CharacterSocket> sockets = new();

    private void OnDrawGizmos()
    {
        GetComponentsInChildren(sockets);

        foreach (CharacterSocket socket in sockets)
        {
            Transform t = socket.transform;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(t.position, t.position + t.right * axisLength);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(t.position, t.position + t.up * axisLength);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(t.position, t.position + t.forward * axisLength);
        }
    }
}
#endif
