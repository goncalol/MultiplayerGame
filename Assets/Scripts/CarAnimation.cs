using UnityEngine;

public class CarAnimation : MonoBehaviour
{
    public void EndingSwing()
    {
        GetComponentInParent<PlayerController>().GetWeapon()?.EndingSwing();
    }

    public void StartNextSwing()
    {
        GetComponentInParent<PlayerController>().GetWeapon()?.StartNextSwing();
    }
    
}
