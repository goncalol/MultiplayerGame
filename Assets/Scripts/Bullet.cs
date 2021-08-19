using Mirror;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    public float destroyAfter;
    public Rigidbody rigidBody;
    public float force ;

    public override void OnStartServer()
    {
        Invoke(nameof(DestroySelf), destroyAfter);
    }

    // set velocity for server and client. this way we don't have to sync the
    // position, because both the server and the client simulate it.
    void Start()
    {
        rigidBody.AddForce(transform.forward * force);
    }

    // destroy for everyone on the server
    [Server]
    void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }

    // ServerCallback because we don't want a warning if OnTriggerEnter is
    // called on the client
    [ServerCallback]
    void OnTriggerEnter(Collider co)
    {
        //var player = co.gameObject.GetComponent<NetworkIdentity>();
        //co.gameObject.GetComponent<Player>().PlayerShot(player.connectionToClient);

        NetworkServer.Destroy(gameObject);
    }
}
