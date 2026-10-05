
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Caso01_ListaDentroDeVariable : NetworkBehaviour
{
    public NetworkVariable<List<int>> enVariable = new(new List<int>());
    public NetworkList<int> enLista;

    void Awake() => enLista = new NetworkList<int>();   // en Awake, no en la declaracion

    public override void OnNetworkSpawn()
    {
        enVariable.OnValueChanged += AlCambiarVariable;
        enLista.OnListChanged += AlCambiarLista;
    }

    public override void OnNetworkDespawn()
    {
        enVariable.OnValueChanged -= AlCambiarVariable;
        enLista.OnListChanged -= AlCambiarLista;
    }

    void Update()
    {
        if (!IsSpawned) return;
        if (Keyboard.current.digit1Key.wasPressedThisFrame) AnadirALaVariableRpc();
        if (Keyboard.current.digit2Key.wasPressedThisFrame) AnadirALaListaRpc();
    }

    [Rpc(SendTo.Server)]
    private void AnadirALaVariableRpc()
    {
        enVariable.Value.Add(enVariable.Value.Count);
        Debug.Log("[Caso01] servidor: la List tiene " + enVariable.Value.Count + " elementos (y aqui no salta ningun evento)");
    }

    [Rpc(SendTo.Server)]
    private void AnadirALaListaRpc()
    {
        enLista.Add(enLista.Count);
    }

    private void AlCambiarVariable(List<int> antes, List<int> ahora)
    {
        Debug.Log("[Caso01] OnValueChanged: antes [" + string.Join(",", antes) + "]  ahora [" + string.Join(",", ahora) + "]  ¿que cambio?");
    }

    private void AlCambiarLista(NetworkListEvent<int> e)
    {
        Debug.Log("[Caso01] OnListChanged: " + e.Type + " en la casilla " + e.Index + " = " + e.Value);
    }
}