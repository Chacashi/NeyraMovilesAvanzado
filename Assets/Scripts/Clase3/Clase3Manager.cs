using Sirenix.OdinInspector;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public class Clase3Manager : NetworkBehaviour
{
    //->prefab a instanciar (Arbol_Seco)
    public GameObject ObjectPrefab;
    //->tiempo que tarda en reaparecer un arbol desgastado
    public float RespawnDelay = 2f;

    void Start()
    {

    }
    public override void OnNetworkSpawn()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    //->lo invoca el UnityEvent del objeto "Test" (boton del Inspector)
    [ServerRpc(RequireOwnership = false)]
    public void InstanciarCuboServerRpc()
    {
        SpawnArbol();
    }

    public void SpawnArbol()
    {
        if (!IsServer) return;

        GameObject obj = Instantiate(ObjectPrefab);
        obj.transform.position = new Vector3(Random.value * 5, 0, Random.value * 5);

        obj.GetComponent<NetworkObject>().Spawn();
    }

    //->lo llama el arbol (Cubito) cuando se agota: lo despawna y lo respawnea sin dueño luego de un rato
    public void ReplaceTree(GameObject treeToReplace)
    {
        if (!IsServer) return;

        if (treeToReplace != null)
        {
            NetworkObject no = treeToReplace.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned)
                no.Despawn(); //->destruye el arbol en todos lados
        }

        StartCoroutine(RespawnCo());
    }

    private IEnumerator RespawnCo()
    {
        yield return new WaitForSeconds(RespawnDelay);
        if (IsServer)
            SpawnArbol();
    }
}