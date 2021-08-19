using Assets.Scripts;
using Mirror;
using UnityEngine;

public class WeaponBox : NetworkBehaviour
{
    public WeaponBoxType weaponBoxType;

    [ServerCallback]
    void OnTriggerEnter(Collider co)
    {
        if (co.gameObject.tag == "Player" && co.GetType() == typeof(CapsuleCollider))
        {
            GameController.Instance.HitWeaponBox(this, co.gameObject);
            NetworkServer.Destroy(gameObject);
        }
    }
}
