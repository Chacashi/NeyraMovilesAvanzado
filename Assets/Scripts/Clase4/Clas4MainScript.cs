using Unity.Netcode;
using UnityEngine;


public class Clas4MainScript : NetworkBehaviour
{
    public NetworkVariable<int> enviados = new(0);

    [SerializeField] private int RelibleCount;
    [SerializeField] private int UnRelibleCount;

    public override void OnNetworkSpawn()
    {

    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)] //->confiable
    public void FiableRpc(int indice)
    {
        RelibleCount++;
        print(RelibleCount);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server, Delivery = RpcDelivery.Unreliable)] //-> no confiable
    public void NofiableRpc(int indice)
    {
        UnRelibleCount++;
        print(UnRelibleCount);
    }

    [ContextMenu("Reiniciar ronda (solo servidor)")]
    public void ResetRoundServer()
    {
        if (!IsSpawned || !IsServer) return;
        foreach (CombatPlayer player in FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None))
            if (player.IsSpawned) player.ResetServer();
    }



    //public NetworkVariable<int> vida = new(20);



    //public override void OnNetworkSpawn()
    //{
    //    vida.OnValueChanged += OnChangeLife;
    //}

    //public override void OnNetworkDespawn()
    //{
    //    vida.OnValueChanged -= OnChangeLife;
    //}



    //private void OnChangeLife(int previousValue, int newValue)
    //{
    //    print("Alguien cambio la vida omg" + previousValue + " , " + newValue);
    //}

    //[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    //public void TakeDamageRpc()
    //{
    //    vida.Value--;
    //}

}



