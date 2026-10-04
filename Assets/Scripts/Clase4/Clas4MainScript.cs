using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;


public class S04MainScript : NetworkBehaviour
{
    public NetworkVariable<int> enviados = new(0);

    [SerializeField] private int RelibleCount;
    [SerializeField] private int UnRelibleCount;

    public override void OnNetworkSpawn()
    {

    }

    [Rpc(SendTo.Everyone)] //->confiable
    public void FiableRpc(int indice)
    {
        RelibleCount++;
        print(RelibleCount);
    }

    [Rpc(SendTo.Everyone, Delivery = RpcDelivery.Unreliable)] //-> no confiable
    public void NofiableRpc(int indice)
    {
        UnRelibleCount++;
        print(UnRelibleCount);
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



