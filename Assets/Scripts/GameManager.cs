using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public class GameManager : NetworkBehaviour
{
    //public GameObject gunPrefab;
    //// Start is called once before the first execution of Update after the MonoBehaviour is created
    //void Start()
    //{
    //    print("Simple");
    //}
    public int count = 0;

    public int X;

    private void Start()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private new void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // Si un jugador se desconecta, sus troncos vuelven a quedar libres.
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        foreach (TreeNode tree in FindObjectsByType<TreeNode>(FindObjectsSortMode.None))
        {
            if (tree.OwnerClientId == clientId)
            {
                tree.FreeFromServer();
                Debug.Log("Server: el jugador " + clientId + " se desconecto, se liberaron sus troncos");
            }
        }
    }
    public override void OnNetworkSpawn()
    {
        //print("Red");
        //if (IsServer)
        //    TryToSpawn();

        ////DataShow();
        //SaludarServerRpc();
        TrySumServerRpc(X);
    }

    [ServerRpc(RequireOwnership = false)]
    public void TrySumServerRpc(int value)
    {
        count += value;

        DeployOnClientRpc(count);
    }
    [ClientRpc]
    public void DeployOnClientRpc(int value)
    {
        count = value;
    }


    //[Button("Button")]
    //public void TryToSpawn()
    //{
    //    GameObject go = Instantiate(gunPrefab, transform.position, Quaternion.identity);

    //    go.GetComponent<NetworkObject>().Spawn();

    //}


    ////-> Solo puedes llamar server rpc si eres DUEÑO del objeto
    //[ServerRpc(RequireOwnership = false)]
    //public void SaludarServerRpc()
    //{
    //    //-> solo se ejecutaria 2 veces en el servidor
    //    Debug.Log("Saludos...");
    //}


    //public void DataShow()
    //{
    //    Debug.Log("Isserver" + IsServer);//T   F
    //    Debug.Log("IsClient" + IsClient);//T   T

    //    Debug.Log("isHost" + IsHost);//T  F
    //    Debug.Log("IsOwner" + IsOwner);// T  F

    //    Debug.Log("Has Authority" + HasAuthority);//T F

    //    Debug.Log("OnwenerCLientID" + OwnerClientId);// 0   0
    //}

}