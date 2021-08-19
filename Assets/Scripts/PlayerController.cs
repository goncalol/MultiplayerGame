using Assets.Scripts;
using Cinemachine;
using FirstGearGames.Mirrors.Assets.FlexNetworkAnimators;
using Mirror;
using System.Collections;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    public float moveSpeed = 15;
    public float rotationSpeed = 190;
    public float HrotationSpeed;
    public Rigidbody rb;
    public Transform CarCenter;
    public CinemachineVirtualCamera virtualCamera;
    public GameObject projectilePrefab;
    public Transform gun;
    public Animator animator;
    public Transform WeaponPlace;
    public GameObject baseballBatPrefab;
    public GameObject[] ObjectsToRotate;
    public GameObject FollowPlayer;

    public float GripX = 12.0f;          // In meters/second2
    public float GripZ = 30f;          // In meters/second2
    public float TopSpeed = 15.0f;      // In meters/second
    public float RotVel = 0.8f;         // Ratio of forward velocity transfered on rotation
    private int Life = 30;
    private Weapon currentWeapon;
    private int ActiveWeaponTime = 10;
    private bool isAccelerating;
    private bool isDeAccelerating;
    private FlexNetworkAnimator fna;
    private GameObject baseballBatClient;
    private FollowPlayer follower;

    bool isFlying = false;
    bool leaveFloor = false;
    public int forceHight;
    public int forceVector;


    private void Awake()
    {
        fna = GetComponent<FlexNetworkAnimator>();
    }

    #region Server


    [Command]
    private void CmdProcessInput(float forward, float turn, float jump)
    {
        if (turn != 0 || forward != 0)
        {
            foreach (var objectToRotate in ObjectsToRotate)
                objectToRotate.transform.Rotate(Time.deltaTime * HrotationSpeed, 0, 0, Space.Self);
        }

        animator.SetFloat("Direction", turn);
        animator.SetBool("IsMoving", forward != 0f);

        var accel = moveSpeed;
        var rotate = rotationSpeed;
        var gripX = GripX;
        var gripZ = GripZ;
        var rotVel = RotVel;

        accel = accel * Mathf.Cos(transform.eulerAngles.x * Mathf.Deg2Rad);
        accel = accel > 0f ? accel : 0f;
        gripZ = gripZ * Mathf.Cos(transform.eulerAngles.x * Mathf.Deg2Rad);
        gripX = gripX * Mathf.Cos(transform.eulerAngles.z * Mathf.Deg2Rad);

        //On the air 
        // alternatives to raycasting - calculating distances over 2 objects with sqr root
        //!!!!!!!!!!!LAYER PARA GROUND!!!!!!!!!!!!!!!!!!!
        var hit = Physics.Raycast(CarCenter.position, -transform.up, 1, LayerMask.GetMask("Floor"));
        if (!hit && leaveFloor)
        {
            isFlying = true;
            leaveFloor = false;
        }
        else if (hit && isFlying)
        {
            isFlying = false;
            rb.angularVelocity = Vector3.zero;
        }

        if (isFlying || leaveFloor)
        {
            rotate = 0f;
            accel = 0f;
        }

        if (jump == 1 && !isFlying && !leaveFloor)
        {
            leaveFloor = true;
            rb.velocity = Vector3.zero;//dar reset da velocidade para dar impulso independentemente do peso - clicar fixo turn jump 
            if (forward != 0)
            {
                rb.AddRelativeForce(new Vector3(0, forceHight, forceVector), ForceMode.VelocityChange);
                animator.SetTrigger("Flip");
            }
            else if (turn != 0)
            {
                if (turn > 0)
                {
                    rb.AddRelativeForce(new Vector3(forceVector, forceHight, 0), ForceMode.VelocityChange);
                    animator.SetTrigger("SideFlipRight");
                }
                else
                {
                    rb.AddRelativeForce(new Vector3(-forceVector, forceHight, 0), ForceMode.VelocityChange);
                    animator.SetTrigger("SideFlipLeft");
                }
            }
            else
            {
                rb.AddForce(new Vector3(0, 1000, 0), ForceMode.Impulse);
            }

            return;
        }

       

        if (forward != 0 && !isFlying)
        {
            rb.velocity += transform.forward * forward * accel * Time.deltaTime;
            gripZ = 0f;     // Remove straight grip if wheel is rotating           
        }
        var isRotating = false;

        // Get the local-axis velocity before new input (+x, +y, and +z = right, up, and forward)
        var pvel = transform.InverseTransformDirection(rb.velocity);
        animator.SetBool("MoveFront", pvel.z >= 0);

        if (!isAccelerating && forward > 0 && pvel.z > 0)
        {
            fna.SetTrigger("accelerate");
            isAccelerating = true;
        }
        if (!isDeAccelerating && forward < 0 && pvel.z < 0)
        {
            fna.SetTrigger("deaccelerate");
            isDeAccelerating = true;
        }
        if (forward == 0)
        {
            isAccelerating = false;
            isDeAccelerating = false;
        }

        if (turn != 0)
        {
            transform.rotation *= Quaternion.AngleAxis(turn * Time.deltaTime * rotate, transform.up);
            isRotating = true;
        }

        var vel = transform.InverseTransformDirection(rb.velocity);

        // Rotate the velocity vector
        // vel = pvel => Transfer all (full grip)
        if (isRotating)
        {
            vel = vel * (1 - rotVel) + pvel * rotVel; // Partial transfer
            //vel = vel.normalized * speed;
        }

        // Sideway grip
        var isRight = vel.x > 0f ? 1f : -1f;
        if (gripX < 0)
            vel.x += isRight * gripX * Time.deltaTime;  // Accelerate in opposing direction
        else
            vel.x -= isRight * gripX * Time.deltaTime;  // Accelerate in opposing direction
        if (vel.x * isRight < 0f) vel.x = 0f;       // Check if changed polarity

        // Straight grip
        var isForward = vel.z > 0f ? 1f : -1f;
        if (gripZ < 0)
            vel.z += isForward * gripZ * Time.deltaTime;
        else
            vel.z -= isForward * gripZ * Time.deltaTime;
        if (vel.z * isForward < 0f) vel.z = 0f;

        // Top speed
        if (vel.z > TopSpeed) vel.z = TopSpeed;
        else if (vel.z < -TopSpeed) vel.z = -TopSpeed;

        rb.velocity = transform.TransformDirection(vel);
    }

    [Command]
    void CmdClick()
    {
        if (currentWeapon != null)
            currentWeapon.Action();
        else
            Fire();
    }

    [Server]
    void Fire()
    {
        GameObject projectile = Instantiate(projectilePrefab, gun.position, transform.rotation);
        NetworkServer.Spawn(projectile);
    }

    [Server]
    public void PlaySwingAnimation()
    {
        ActivateAnimatorSwing(true);
    }

    [Server]
    public void ActivateAnimatorSwing(bool activate)
    {
        animator.SetLayerWeight(2, activate ? 1 : 0);
        animator.SetBool("Swing", activate);
    }

    [Server]
    public Weapon GetWeapon()
    {
        return currentWeapon;
    }

    // ainda da para por mais genrico e abstrair server to client calls - nao haver current weapon e ter uma função/classe que activa bastao no server e client
    [Server]
    public void ChangeToWeapon(WeaponBoxType weaponType)
    {
        if (currentWeapon != null) return;

        animator.SetLayerWeight(1, 1);
        if (weaponType == WeaponBoxType.BaseballBat)
        {
            GameObject baseballBat = Instantiate(baseballBatPrefab, WeaponPlace.position, WeaponPlace.rotation);
            baseballBat.transform.parent = WeaponPlace;
            var weapon = baseballBat.transform.GetChild(0).GetComponent<Weapon>();
            weapon.SetPlayer(this);
            currentWeapon = weapon;
            RpcActivateClientWeapon(true);
            StartCoroutine(RemoveWeapon(weaponType));
        }
    }

    [Server]
    IEnumerator RemoveWeapon(WeaponBoxType weaponType)
    {
        yield return new WaitForSeconds(ActiveWeaponTime);
        animator.SetLayerWeight(1, 0);
        //currentWeapon?.Disable();
        ActivateAnimatorSwing(false);
        Destroy(currentWeapon.transform.parent.gameObject);
        currentWeapon = null;
        RpcActivateClientWeapon(false);
    }

    #endregion

    #region Client - THIS


    public override void OnStartAuthority()
    {
        //follower = Instantiate(FollowPlayer, transform.position, transform.rotation).GetComponent<FollowPlayer>();
        //virtualCamera.gameObject.SetActive(true);
        //virtualCamera.LookAt = follower.transform;
        //virtualCamera.Follow = follower.transform;
        CameraController.Instance.SetPlayerToFollow(virtualCamera.gameObject);
        rb = GetComponent<Rigidbody>();
        enabled = true;
    }

    private void UpdateClient()
    {
        if (Input.GetMouseButtonDown(0))
            CmdClick();
    }

    private void FixedUpdateClient()
    {
        var a = Input.GetAxis("Horizontal");
        //animator.SetFloat("Blend", a);
        //THIS CAN CAUSE TOO MUCH THROUGHPUT!!!!!!!!!!!!!!!!
        CmdProcessInput(Input.GetAxis("Vertical"), a, Input.GetAxis("Jump"));
    }

    [TargetRpc]
    public void PlayerShot(NetworkConnection target)
    {
        Life -= 10;
        UIController.Instance.SetLifeText(Life);
    }


    #endregion

    #region Client - ALL

    private void Update()
    {
        if (hasAuthority)
        {
            UpdateClient();
        }
    }

    private void FixedUpdate()
    {
        if (hasAuthority)
        {
            //Debug.Log(transform.position);
            //Debug.Log(transform.localPosition);
            //Debug.Log(rb.position);
            //follower.SetPositionAndRotation(transform.position, transform.rotation * Quaternion.Euler(0, 1, 0));
            FixedUpdateClient();
        }
    }

    [ClientRpc]
    public void RpcActivateClientWeapon(bool activate)
    {
        if (activate)
        {
            baseballBatClient = Instantiate(baseballBatPrefab, WeaponPlace.position, WeaponPlace.rotation);
            baseballBatClient.transform.parent = WeaponPlace;
        }
        else
        {
            Destroy(baseballBatClient);
        }
    }

    #endregion

}
