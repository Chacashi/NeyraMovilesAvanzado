using Unity.Netcode;
using UnityEngine;

public class TreeNode : NetworkBehaviour
{
    // Se dispara en cada cliente cuando algo del tronco cambia (dueño o logs),
    // asi el tablero (ScoreboardUI) se refresca sin estar consultando cada frame.
    public static event System.Action TreeStateChanged;

    // Logs que tiene el tronco cuando "nace" (lo configuras en el inspector, ejemplo: 5).
    public int logs = 5;

    // Logs que quedan AHORA. NetworkVariable => se sincroniza a todos los clientes.
    public NetworkVariable<int> Logs = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Marcador explicito de "reclamado". Sin el, el host nunca podria ser "dueno":
    // en Netcode, cuando el host reclama, el dueno queda como el servidor (client 0),
    // igual que un tronco libre. Con Claimed lo distinguimos.
    public NetworkVariable<bool> Claimed = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public Color freeColor = Color.green;
    public Color takenColor = Color.red;

    // true = el tronco tiene dueno. false = esta libre.
    public bool IsTaken => Claimed.Value;

    private void OnEnable()
    {
        Player.PlayerIdentityChanged += RepaintAndNotify;
        Logs.OnValueChanged += OnLogsChanged;
        Claimed.OnValueChanged += OnClaimedChanged;
    }

    private void OnDisable()
    {
        Player.PlayerIdentityChanged -= RepaintAndNotify;
        Logs.OnValueChanged -= OnLogsChanged;
        Claimed.OnValueChanged -= OnClaimedChanged;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Logs.Value = logs;
        }
        ChangeColor();
        TreeStateChanged?.Invoke();
    }

    private void OnLogsChanged(int previous, int current)
    {
        TreeStateChanged?.Invoke();
    }

    private void OnClaimedChanged(bool previous, bool current)
    {
        ChangeColor();
        TreeStateChanged?.Invoke();
    }

    private void RepaintAndNotify()
    {
        ChangeColor();
        TreeStateChanged?.Invoke();
    }

    private void OnMouseDown()
    {
        if (!IsSpawned) return;

        if (!IsTaken)
        {
            //-> lo puedo claimear
            ClaimServerRpc();
        }
        else if (IsOwner)
        {
            ChopServerRpc();
        }
        else
        {
            //-> no chop chop me ganaron
            Debug.Log("Te ganaron, esoy es dueno de este tronco");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClaimServerRpc(ServerRpcParams rpcParams = default)
    {
        if (IsTaken)
        {
            Debug.Log("Te ganaron, este tronco ya tiene dueno");
            return;
        }

        ulong who = rpcParams.Receive.SenderClientId;
        if (NetworkObject.OwnerClientId != who)
            NetworkObject.ChangeOwnership(who);
        Claimed.Value = true;
        Debug.Log("Server: el tronco ahora le pertenece a: " + who);
    }

    [ServerRpc]
    public void ChopServerRpc()
    {
        if (Logs.Value <= 0)
        {
            //-> se agoto: vuelve a estar libre con los logs completos
            NetworkObject.RemoveOwnership();
            Claimed.Value = false;
            Logs.Value = logs;
            Debug.Log("Server: tronco agotado, vuelve a estar libre");
            return;
        }

        Logs.Value -= 1;
        AddLogToOwner();
    }

    private void AddLogToOwner()
    {
        foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (player.IsSpawned && player.OwnerClientId == OwnerClientId)
            {
                player.AddCollectedLogServer(1);
                return;
            }
        }
    }

    // Lo llama el GameManager cuando un jugador se desconecta, para liberar sus troncos.
    public void FreeFromServer()
    {
        if (!IsServer) return;
        NetworkObject.RemoveOwnership();
        Claimed.Value = false;
    }

    protected override void OnOwnershipChanged(ulong previous, ulong current)
    {
        ChangeColor();
        TreeStateChanged?.Invoke();
    }

    private void ChangeColor()
    {
        if (!IsSpawned) return;

        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null) return;

        if (!IsTaken)
        {
            renderer.material.color = freeColor;
            return;
        }

        if (Player.ColorByClientId.TryGetValue(OwnerClientId, out Color ownerColor))
        {
            renderer.material.color = ownerColor;
        }
        else
        {
            renderer.material.color = takenColor;
        }
    }
}