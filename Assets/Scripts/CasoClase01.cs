using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;
using Sirenix.OdinInspector;
public class CasoClase01 : NetworkBehaviour
{
    //->Caso 2 ciclo de vida


    //->No utilizen Start
    void Start()
    {

    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("El componente se creo en el server: " + IsServer);
        Debug.Log("El componente se a creado: " + IsSpawned);
    }
    public override void OnNetworkDespawn()
    {
        Debug.Log("is owner: " + IsOwner);
        Debug.Log("Cliend ID: " + OwnerClientId);
        Debug.Log("Network Object ID: " + NetworkObjectId);
    }


    private void Update()
    {

    }
    [Button]
    public void TestButton()
    {

    }



}