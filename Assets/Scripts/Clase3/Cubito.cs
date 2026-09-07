using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;


/* 
 FixedString32Bytes  Holds up to 32 bytes of data.
 FixedString64Bytes
128
512
4096
 */


public class Cubito : NetworkBehaviour
{
    // 1) Declaracion del estado: Color del arbol. Gris = libre.
    //    Permisos: lectura para todos (Everyone), escritura SOLO el server.
    public NetworkVariable<Color> randomColor = new(Color.gray, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 5) Nombre del dueno, replicado como FixedString (seguro y eficiente, sin strings dinamicos por la red).
    public NetworkVariable<FixedString32Bytes> previousOwner = new("Sin Reclamar");

    public NetworkVariable<int> randomValue = new();
    public NetworkVariable<int> counter = new();//->cada click cuenta (1er click reclama, los siguientes talan)

    //->clicks hasta que el arbol desaparezca y reaparezca sin dueno. Ajustable en el prefab.
    public int maxClicks = 5;

    private MeshRenderer meshRenderer; //->el MeshRenderer esta en el hijo
    private TMP_Text ownerLabel;       //->nombre sobre el arbol, autorizado EN EL PREFAB (Canvas world-space + TMP), no por codigo

    private void Awake()
    {
        // Capturo el renderer del modelo (esta en el hijo, no en la raiz).
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        ownerLabel = GetComponentInChildren<TMP_Text>(true); //->busco el TMP que ya esta en el prefab
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            randomValue.Value = Random.Range(0, 10);
        }

        // 2) Logica de pintado: suscribirse a los cambios.
        randomColor.OnValueChanged += OnColorChanged;
        previousOwner.OnValueChanged += OnOwnerChanged;

        // 3) Sincronizacion tardia: pintar/rotular YA con el valor actual (.Value),
        //    asi quien entra tarde ve el estado inicial correcto.
        ApplyColor(randomColor.Value);
        ApplyOwnerLabel(previousOwner.Value);

        Debug.Log(previousOwner.Value);
        Debug.Log(randomValue.Value);
    }

    // 2) Desuscribirse al despawnear.
    public override void OnNetworkDespawn()
    {
        randomColor.OnValueChanged -= OnColorChanged;
        previousOwner.OnValueChanged -= OnOwnerChanged;
    }

    private void OnColorChanged(Color previous, Color current)
    {
        ApplyColor(current);
    }

    private void ApplyColor(Color color)
    {
        if (meshRenderer != null)
            meshRenderer.material.color = color;
    }

    private void OnOwnerChanged(FixedString32Bytes previous, FixedString32Bytes current)
    {
        ApplyOwnerLabel(current);
    }

    private void ApplyOwnerLabel(FixedString32Bytes owner)
    {
        if (ownerLabel != null)
        {
            ownerLabel.text = owner.ToString();
            //->sin reclamar queda en gris; reclamado se ve su dueño
            ownerLabel.color = owner == "Sin Reclamar" ? Color.gray : Color.white;
        }
    }

    void Start()
    {

    }


    void Update()
    {

    }
    private void OnMouseDown()
    {
        if (!IsSpawned) return;
        Debug.Log("Me dieron click");
        ChopRpc();
    }

    // 4) RPC moderno: [Rpc(SendTo.Server)] con permiso para que cualquier cliente lo invoque.
    //    - 1er click en un arbol gris: lo reclama (le asigna dueño y color).
    //    - Clicks siguientes del dueño: cada click "tala" y suma al contador.
    //    - Al llegar a maxClicks el arbol desaparece y el Clase3Manager lo respawnea sin dueño.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ChopRpc(RpcParams rpcParams = default)
    {
        ulong who = rpcParams.Receive.SenderClientId;

        //->1er click en un arbol libre: lo reclamo.
        if (randomColor.Value == Color.gray)
        {
            NetworkObject.ChangeOwnership(who);
            previousOwner.Value = who.ToString();
            randomColor.Value = Random.ColorHSV();
            Debug.Log($"Server: arbol reclamado por {who}");
        }
        else if (OwnerClientId != who)
        {
            Debug.Log("Este arbol no es tuyo, no lo puedes talar");
            return;
        }

        counter.Value++;//->cada click cuenta (corre en el server, asi que es valido editar network variables)

        if (counter.Value >= maxClicks)
        {
            //->se agotan los clicks: desaparece y respawnea sin dueño
            var mgr = FindFirstObjectByType<Clase3Manager>();
            if (mgr != null)
                mgr.ReplaceTree(gameObject);
        }
    }
}