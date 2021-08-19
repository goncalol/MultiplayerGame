using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public Text Lifetext;

    private static UIController _instance;

    [HideInInspector]
    public static UIController Instance { get { return _instance; } }

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

    public void SetLifeText(int life)
    {
        Lifetext.text = life.ToString();
    }

}
