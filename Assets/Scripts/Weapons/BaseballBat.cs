using Mirror;
using UnityEngine;

public class BaseballBat : Weapon
{
    private PlayerController player;

    private bool isSwinging;
    private bool isInSwingInterval;
    private bool nextSwing;
    private Collider batCollider;

    private void Awake()
    {
        batCollider = gameObject.transform.GetComponent<Collider>();
    }

    [Server]
    public override void Action()
    {
        if (player == null) return;

        if (!isSwinging)
        {
            isSwinging = true;
            batCollider.enabled = true;
            player.PlaySwingAnimation();
        }

        nextSwing = isInSwingInterval;
    }

    [Server]
    public override void StartNextSwing()
    {
        isInSwingInterval = true;
    }

    [Server]
    public override void EndingSwing()
    {
        isInSwingInterval = false;
        if (!nextSwing)
        {
            isSwinging = false;
            batCollider.enabled = false;
            player.ActivateAnimatorSwing(false);
        }
        nextSwing = false;
    }

    //[Server]
    //public override void Disable()
    //{
    //    batCollider.enabled = false;
    //    isInSwingInterval = false;
    //    nextSwing = false;
    //    isSwinging = false;
    //    gameObject.SetActive(false);
    //    player.ActivateAnimatorSwing(false);
    //}

    [Server]
    public override void SetPlayer(PlayerController playerController)
    {
        player = playerController;
    }
}

