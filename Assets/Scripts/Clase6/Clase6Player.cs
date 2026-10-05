using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerRole : byte { Guardian, Explorer }

public class Clase6Player : NetworkBehaviour
{
    [SerializeField] private PlayerRole role;
    public PlayerRole Role => role;
    public float MoveSpeed = 5f;
    public float InteractRange = 3f;
    public float GiftRange = 4f;
    private InputSystem_Actions inputs;
    private InputAction createChest, gift, consume, switchRole;
    private NetworkTickSystem ticks;
    private Vector2 localMove, serverMove;
    private double lastMoveTime;
    private PlayerAssigner assigner;
    private Material bodyMaterial;
    private bool switchingCharacter;

    public static Color ColorForOwner(ulong id) => Color.HSVToRGB(((id % 1000) * 0.271f + 0.55f) % 1f, 0.7f, 0.95f);

    private void Awake()
    {
        assigner = FindFirstObjectByType<PlayerAssigner>();
        bodyMaterial = GetComponent<Renderer>().material;
        inputs = new InputSystem_Actions();
        inputs.Player.Move.performed += OnMove;
        inputs.Player.Move.canceled += OnMove;
        inputs.Player.Interact.started += OnInteract;
        createChest = new InputAction("Crear cofre", InputActionType.Button, "<Keyboard>/b");
        gift = new InputAction("Regalar", InputActionType.Button, "<Keyboard>/g");
        consume = new InputAction("Consumir", InputActionType.Button, "<Keyboard>/x");
        switchRole = new InputAction("Cambiar personaje", InputActionType.Button, "<Keyboard>/v");
        createChest.performed += OnCreateChest;
        gift.performed += OnGift;
        consume.performed += OnConsume;
        switchRole.performed += OnSwitch;
    }

    public override void OnNetworkSpawn()
    {
        bodyMaterial.color = ColorForOwner(OwnerClientId);
        Debug.Log($"OnNetworkSpawn: {Role}, jugador {OwnerClientId}");
        if (!IsOwner) return;
        inputs.Player.Enable();
        createChest.Enable(); gift.Enable(); consume.Enable(); switchRole.Enable();
        ticks = NetworkManager.NetworkTickSystem;
        ticks.Tick += SendMove;
    }

    public override void OnNetworkDespawn()
    {
        if (ticks != null) ticks.Tick -= SendMove;
        ticks = null;
        inputs.Disable();
        createChest.Disable(); gift.Disable(); consume.Disable(); switchRole.Disable();
    }

    public override void OnDestroy()
    {
        inputs?.Dispose();
        createChest?.Dispose(); gift?.Dispose(); consume?.Dispose(); switchRole?.Dispose();
        if (bodyMaterial != null) Destroy(bodyMaterial);
        base.OnDestroy();
    }

    private bool CanRequest => IsSpawned && IsOwner && !switchingCharacter;
    private void OnMove(InputAction.CallbackContext context) => localMove = context.ReadValue<Vector2>();
    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!CanRequest) return;
        InventarioDeRed inventory = GetComponent<InventarioDeRed>();
        if (inventory != null) inventory.InteractuarRpc();
        else InteractRpc();
    }
    private void OnCreateChest(InputAction.CallbackContext context) { if (CanRequest) CreateChestRpc(); }
    private void OnGift(InputAction.CallbackContext context) { if (CanRequest) GiftRpc(); }
    private void OnConsume(InputAction.CallbackContext context) { if (CanRequest) ConsumeRpc(); }
    private void OnSwitch(InputAction.CallbackContext context)
    {
        if (!CanRequest) return;
        switchingCharacter = true;
        SwitchCharacterRpc();
    }
    private void SendMove() { if (CanRequest) MoveRpc(localMove); }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
    private void MoveRpc(Vector2 direction)
    {
        if (!IsServer || !assigner.IsCurrentPlayer(this) || float.IsNaN(direction.x) || float.IsNaN(direction.y) ||
            float.IsInfinity(direction.x) || float.IsInfinity(direction.y)) return;
        serverMove = Vector2.ClampMagnitude(direction, 1f);
        lastMoveTime = Time.timeAsDouble;
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer) return;
        if (Time.timeAsDouble - lastMoveTime > 0.5) serverMove = Vector2.zero;
        Vector3 direction = new(serverMove.x, 0f, serverMove.y);
        Vector3 position = transform.position + direction * MoveSpeed * Time.deltaTime;
        position.x = Mathf.Clamp(position.x, -8f, 8f);
        position.z = Mathf.Clamp(position.z, -8f, 8f);
        transform.position = position;
        if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CreateChestRpc()
    {
        if (!IsServer || !assigner.IsCurrentPlayer(this)) return;
        assigner.CreateChestServer(this);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void InteractRpc()
    {
        if (!IsServer || !assigner.IsCurrentPlayer(this)) return;
        Clase6Chest nearestChest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Clase6Chest chest in FindObjectsByType<Clase6Chest>(FindObjectsSortMode.None))
        {
            float distance = (chest.transform.position - transform.position).sqrMagnitude;
            if (!chest.IsSpawned || distance > InteractRange * InteractRange || distance >= nearestDistance) continue;
            nearestChest = chest; nearestDistance = distance;
        }
        if (nearestChest != null && nearestChest.TryOpenServer(this)) { MessageRpc("Abriste el cofre: el botin nacio a tu nombre."); return; }
        Clase6LootCube cube = NearestCube(false);
        if (cube != null && cube.ClaimServer(this)) MessageRpc("Cubo libre reclamado.");
        else MessageRpc("Acercate a un cofre o cubo libre y pulsa E.");
    }

    private Clase6LootCube NearestCube(bool owned)
    {
        Clase6LootCube nearest = null;
        float distanceLimit = InteractRange * InteractRange;
        foreach (Clase6LootCube cube in FindObjectsByType<Clase6LootCube>(FindObjectsSortMode.None))
        {
            if (!cube.IsSpawned || (owned ? !cube.Claimed.Value || cube.OwnerClientId != OwnerClientId : cube.Claimed.Value)) continue;
            float distance = (cube.transform.position - transform.position).sqrMagnitude;
            if (distance > distanceLimit) continue;
            distanceLimit = distance; nearest = cube;
        }
        return nearest;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void GiftRpc()
    {
        if (!IsServer || !assigner.IsCurrentPlayer(this)) return;
        Clase6LootCube cube = NearestCube(true);
        Clase6Player receiver = null;
        float limit = GiftRange * GiftRange;
        foreach (Clase6Player player in FindObjectsByType<Clase6Player>(FindObjectsSortMode.None))
        {
            if (player == this || !assigner.IsCurrentPlayer(player)) continue;
            float distance = (player.transform.position - transform.position).sqrMagnitude;
            if (distance > limit) continue;
            limit = distance; receiver = player;
        }
        if (cube == null || receiver == null || !cube.GiftServer(this, receiver))
            MessageRpc("Necesitas un cubo propio y otro jugador cerca.");
        else MessageRpc($"Regalaste un cubo al jugador {receiver.OwnerClientId}.");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ConsumeRpc()
    {
        if (!IsServer || !assigner.IsCurrentPlayer(this)) return;
        Clase6LootCube cube = NearestCube(true);
        if (cube != null && cube.ConsumeServer(this)) MessageRpc("Cubo consumido (Despawn true).");
        else MessageRpc("No hay un cubo propio al alcance.");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void SwitchCharacterRpc()
    {
        if (IsServer) assigner.SwitchCharacterServer(this);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void MessageRpc(FixedString128Bytes message)
    {
        switchingCharacter = false; // Un cambio rechazado vuelve a habilitar al personaje anterior.
        UIManager ui = FindFirstObjectByType<UIManager>();
        if (ui != null) ui.ShowMessage(message.ToString());
    }
}
