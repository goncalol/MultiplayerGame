using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
    }
}
