using UnityEngine;

public class CameraController : MonoBehaviour
{
    private GameObject playerToFollow;
    public float smooth = 3f;

    private static CameraController _instance;

    public static CameraController Instance { get { return _instance; } }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }

    void FixedUpdate()
    {
        if (playerToFollow)
        {
            Vector3 upVector = playerToFollow.transform.parent.rotation * Vector3.up;
            float XAngle = FormatDegree(Mathf.Atan2(upVector.y, upVector.z) * Mathf.Rad2Deg);


            if (XAngle >= 315 || XAngle <= 45)
            {
                transform.position = Vector3.Lerp(transform.position, playerToFollow.transform.position, Time.deltaTime * smooth);
                transform.forward = Vector3.Lerp(transform.forward, playerToFollow.transform.forward, Time.deltaTime * smooth);
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, playerToFollow.transform.position, Time.deltaTime );
            }
            transform.LookAt(playerToFollow.transform.parent);
        }
    }

    public void SetPlayerToFollow(GameObject obj)
    {
        playerToFollow = obj;
    }

    /// <summary>
    /// formats the degree to 0-360 clockwise, where 0 is pointing up.
    /// </summary>
    /// <returns></returns>
    private float FormatDegree(float degree)
    {
        if (degree < 0) return -degree + 90;
        else if (degree <= 90) return -(degree - 90);
        else return 360 - (degree - 90);

    }
}
