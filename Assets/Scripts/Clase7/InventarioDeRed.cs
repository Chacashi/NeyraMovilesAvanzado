using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventarioDeRed : NetworkBehaviour
{
    public NetworkList<Ranura> Ranuras;
    [Min(1)] public int Capacidad = 30;
    private Clase6Player player;
    private PlayerAssigner assigner;
    private PoolDeRed pool;
    private InventarioUI ui;
    private InputAction soltar;
    private List<Ranura> pendiente;
    private bool transferido;
    public int TipoSeleccionado { get; private set; } = -1;
    public int Total { get { int total = 0; foreach (Ranura r in Ranuras) total += r.cantidad; return total; } }

    private void Awake()
    {
        // Como en el ejemplo del profesor: en Awake, no en la declaracion del campo.
        Ranuras = new NetworkList<Ranura>(readPerm: NetworkVariableReadPermission.Owner, writePerm: NetworkVariableWritePermission.Server);
        player = GetComponent<Clase6Player>();
        assigner = FindFirstObjectByType<PlayerAssigner>();
        pool = FindFirstObjectByType<PoolDeRed>();
        soltar = new InputAction("Soltar inventario", InputActionType.Button, "<Keyboard>/r");
        soltar.performed += OnSoltar;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && pendiente != null)
        {
            foreach (Ranura r in pendiente) Ranuras.Add(r);
            pendiente = null;
        }
        if (!IsOwner) return;
        Ranuras.OnListChanged += AlCambiarInventario;
        soltar.Enable();
        ui = FindFirstObjectByType<InventarioUI>();
        if (ui != null) ui.Vincular(this);
        RefrescarUI(); // La lista inicial ya fue sincronizada antes de OnNetworkSpawn.
    }

    public override void OnNetworkDespawn()
    {
        Ranuras.OnListChanged -= AlCambiarInventario;
        soltar.Disable();
        if (ui != null) ui.Desvincular(this);
        if (IsServer && !transferido && !NetworkManager.ShutdownInProgress && pool != null && Total > 0)
            pool.DejarTrasDesconexion(Copiar(), transform.position);
    }

    public override void OnDestroy()
    {
        soltar?.Dispose();
        // NetworkBehaviour dispone sus NetworkVariables, incluida la NetworkList.
        base.OnDestroy();
    }

    private void OnSoltar(InputAction.CallbackContext context)
    {
        if (IsSpawned && IsOwner && TipoSeleccionado > 0) SoltarRpc(TipoSeleccionado);
    }

    private void AlCambiarInventario(NetworkListEvent<Ranura> change)
    {
        switch (change.Type)
        {
            case NetworkListEvent<Ranura>.EventType.Add:
            case NetworkListEvent<Ranura>.EventType.Value:
            case NetworkListEvent<Ranura>.EventType.RemoveAt:
            case NetworkListEvent<Ranura>.EventType.Clear:
            case NetworkListEvent<Ranura>.EventType.Full:
                Debug.Log($"[Inventario] {change.Type} en {change.Index}: tipo={change.Value.tipo}, cantidad={change.Value.cantidad}");
                break;
        }
        RefrescarUI();
    }

    private int Buscar(int tipo)
    {
        for (int i = 0; i < Ranuras.Count; i++) if (Ranuras[i].tipo == tipo) return i;
        return -1;
    }

    public void SeleccionarSiguiente(int direction)
    {
        if (!IsSpawned || !IsOwner || Ranuras.Count == 0) return;
        int index = Buscar(TipoSeleccionado);
        index = (Mathf.Max(0, index) + direction + Ranuras.Count) % Ranuras.Count;
        TipoSeleccionado = Ranuras[index].tipo;
        RefrescarUI();
    }

    private void RefrescarUI()
    {
        if (!IsOwner) return;
        if (Buscar(TipoSeleccionado) < 0) TipoSeleccionado = Ranuras.Count > 0 ? Ranuras[0].tipo : -1;
        if (ui != null) ui.Refrescar();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void InteractuarRpc()
    {
        if (!IsServer || !assigner.IsCurrentPlayer(player)) return;
        Clase6LootCube nearest = null;
        float limit = player.InteractRange * player.InteractRange;
        foreach (Clase6LootCube cube in FindObjectsByType<Clase6LootCube>(FindObjectsSortMode.None))
        {
            if (!cube.IsSpawned || (cube.Claimed.Value && cube.OwnerClientId != OwnerClientId)) continue;
            float distance = (cube.transform.position - transform.position).sqrMagnitude;
            if (distance > limit) continue;
            limit = distance; nearest = cube;
        }
        if (nearest != null)
        {
            CuboReciclado item = nearest.GetComponent<CuboReciclado>();
            if (Total >= Capacidad) { player.MessageRpc("Inventario lleno."); return; }
            if (item == null || !item.MarcarGuardadoServidor()) return;
            int index = Buscar(item.Tipo.Value);
            if (index < 0) Ranuras.Add(new Ranura(item.Tipo.Value, 1));
            else
            {
                Ranura r = Ranuras[index]; r.cantidad++;
                Ranuras[index] = r; // Value: asignar la copia del struct a la casilla.
            }
            nearest.NetworkObject.Despawn(true); // El handler devuelve el GameObject al pool.
            player.MessageRpc("Cubo guardado como datos en el inventario.");
            return;
        }
        foreach (Clase6Chest chest in FindObjectsByType<Clase6Chest>(FindObjectsSortMode.None))
            if (chest.IsSpawned && chest.TryOpenServer(player)) { player.MessageRpc("Cofre abierto. Pulsa E junto al botin para guardarlo."); return; }
        player.MessageRpc("Acercate a un cofre o a un cubo propio/libre.");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void SoltarRpc(int tipo)
    {
        if (!IsServer || !assigner.IsCurrentPlayer(player)) return;
        int index = Buscar(tipo);
        if (index < 0 || Ranuras[index].cantidad <= 0) return;
        Vector3 position = transform.position + transform.forward * 1.5f;
        position.x = Mathf.Clamp(position.x, -9f, 9f);
        position.z = Mathf.Clamp(position.z, -9f, 9f);
        position.y = 0.8f;
        NetworkObject spawned = pool.SpawnServidor(position, Quaternion.identity, Unity.Netcode.NetworkManager.ServerClientId, tipo, false);
        if (spawned == null) return;
        QuitarUno(index); // Solo quitar el dato si el objeto aparecio correctamente.
        player.MessageRpc("Soltaste una unidad. Ahora esta libre en el suelo.");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void ConsumirRpc(int tipo)
    {
        if (!IsServer || !assigner.IsCurrentPlayer(player)) return;
        int index = Buscar(tipo);
        if (index < 0 || Ranuras[index].cantidad <= 0) return;
        QuitarUno(index);
        player.MessageRpc("Consumiste una unidad del inventario.");
    }

    private void QuitarUno(int index)
    {
        Ranura r = Ranuras[index];
        if (--r.cantidad == 0) Ranuras.RemoveAt(index);
        else Ranuras[index] = r;
    }

    private List<Ranura> Copiar()
    {
        var data = new List<Ranura>();
        foreach (Ranura r in Ranuras) data.Add(r);
        return data;
    }

    public List<Ranura> ExtraerParaCambioServidor()
    {
        if (!IsServer) return null;
        transferido = true;
        List<Ranura> data = Copiar();
        Ranuras.Clear();
        return data;
    }

    public void RestaurarAlAparecer(List<Ranura> datos) => pendiente = datos;
}
