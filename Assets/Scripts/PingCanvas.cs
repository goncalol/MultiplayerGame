using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class PingCanvas : MonoBehaviour
{
    [SerializeField]
    private Text _text;


    private void FixedUpdate()
    {
        if (NetworkClient.active)
        {
            float ping = Mathf.CeilToInt((float)NetworkTime.rtt * 1000f);
            _text.text =  ping.ToString();
        }
    }
}
