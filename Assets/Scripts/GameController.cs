using Mirror;
using System.Collections;
using UnityEngine;

public class GameController : NetworkBehaviour
{
    public GameObject WeaponBoxGroupSpawn;
    public GameObject WeaponBoxPrefab;

    private static GameController _instance;

    [HideInInspector]
    public static GameController Instance { get { return _instance; } }

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

    public override void OnStartServer()
    {
        base.OnStartServer();

        for (var i = 0; i < WeaponBoxGroupSpawn.transform.childCount; i++)
        {
            var t = WeaponBoxGroupSpawn.transform.GetChild(i);
            SpawnNewWeaponBox(t.position, t.rotation);
        }
    }

    public void HitWeaponBox(WeaponBox weaponBox, GameObject playerObj)
    {
        StartCoroutine(RestoreWeaponBox(weaponBox.transform.position, weaponBox.transform.rotation, 5f));

        playerObj.GetComponent<PlayerController>().ChangeToWeapon(weaponBox.weaponBoxType);
    }

    IEnumerator RestoreWeaponBox(Vector3 position, Quaternion rotation, float delayTime)
    {
        yield return new WaitForSeconds(delayTime);
        SpawnNewWeaponBox(position, rotation);
    }

    private void SpawnNewWeaponBox(Vector3 position, Quaternion rotation)
    {
        GameObject weaponBox = Instantiate(WeaponBoxPrefab, position, rotation);
        NetworkServer.Spawn(weaponBox);

    }

}
