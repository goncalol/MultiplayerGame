using Mirror;
using System;
using UnityEngine;

public abstract class Weapon : NetworkBehaviour
{
    public float Force;

    [ServerCallback]
    void OnTriggerEnter(Collider co)
    {
        if (co.gameObject.tag == "Player" || co.gameObject.tag == "Weapon")
        {
            Vector3 direction = co.gameObject.transform.position - transform.position;
            direction.y *= 0.2f;
            co.GetComponent<Rigidbody>().AddForce(direction.normalized * Force, ForceMode.Impulse);
        }
    }

    public abstract void StartNextSwing();

    public abstract void EndingSwing();

    public abstract void Action();

    //public abstract void Disable();

    public abstract void SetPlayer(PlayerController playerController);
}
