using TMPro;
using Unity.Netcode;
using UnityEngine;

public class Chest : NetworkBehaviour
{
    public NetworkObject cubePrefab;
    [Min(0.1f)] public float TimeToOpenChest = 3f;
    [Min(0.1f)] public float OpenRadius = 2.5f;
    [Min(1)] public int CubeCount = 5;

    [Header("Feedback visual del prefab")]
    [SerializeField] private Canvas progressCanvas;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private bool faceCamera = true;

    public NetworkVariable<ulong> UserOpening = new(ulong.MaxValue,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> Progress = new(0f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private PlayerAvatar openingPlayer;
    private bool opened;
    private Camera viewCamera;

    private void Awake()
    {
        if (progressCanvas != null) progressCanvas.gameObject.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        UserOpening.OnValueChanged += OnOpeningChanged;
        Progress.OnValueChanged += OnProgressChanged;
        viewCamera = Camera.main;
        RefreshFeedback(); // Incluye a los clientes que entran durante la apertura.
    }

    public override void OnNetworkDespawn()
    {
        UserOpening.OnValueChanged -= OnOpeningChanged;
        Progress.OnValueChanged -= OnProgressChanged;
        if (progressCanvas != null) progressCanvas.gameObject.SetActive(false);
        // Despawn(false) conserva el objeto de escena: ocultarlo tambien en cada cliente.
        gameObject.SetActive(false);
    }

    private void OnOpeningChanged(ulong previous, ulong current) => RefreshFeedback();
    private void OnProgressChanged(float previous, float current) => RefreshFeedback();

    private void RefreshFeedback()
    {
        bool visible = IsSpawned && UserOpening.Value != ulong.MaxValue;
        if (progressCanvas != null) progressCanvas.gameObject.SetActive(visible);
        if (!visible || countdownText == null) return;

        float progress = Mathf.Clamp01(Progress.Value);
        float remaining = Mathf.Max(0.1f, TimeToOpenChest) * (1f - progress);
        countdownText.text = $"ABRIENDO...\n{remaining:0.0} s  |  {Mathf.RoundToInt(progress * 100f)}%";
    }

    private void LateUpdate()
    {
        if (!faceCamera || progressCanvas == null || !progressCanvas.gameObject.activeSelf) return;
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null) progressCanvas.transform.rotation = viewCamera.transform.rotation;
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer || UserOpening.Value == ulong.MaxValue) return;

        // Alejarse o desconectarse libera el bloqueo, igual que soltar E.
        if (openingPlayer == null || !openingPlayer.IsSpawned ||
            !NetworkManager.ConnectedClients.ContainsKey(UserOpening.Value) ||
            !IsInRange(openingPlayer.transform.position))
        {
            CancelOpeningServer(UserOpening.Value);
            return;
        }

        Progress.Value = Mathf.Clamp01(Progress.Value + Time.deltaTime / Mathf.Max(0.1f, TimeToOpenChest));
        if (Progress.Value >= 1f) OpenChest();
    }

    public bool IsInRange(Vector3 position)
    {
        return (position - transform.position).sqrMagnitude <= OpenRadius * OpenRadius;
    }

    // Llamado por el RPC del avatar: conserva la identidad del jugador original.
    public bool TryStartOpeningServer(PlayerAvatar player)
    {
        if (!IsServer || !IsSpawned || opened || player == null || !player.IsSpawned ||
            !IsInRange(player.transform.position)) return false;
        if (UserOpening.Value != ulong.MaxValue && UserOpening.Value != player.OwnerClientId) return false;

        openingPlayer = player;
        UserOpening.Value = player.OwnerClientId;
        return true;
    }

    public void CancelOpeningServer(ulong clientId)
    {
        if (!IsServer || !IsSpawned || opened || UserOpening.Value != clientId) return;
        UserOpening.Value = ulong.MaxValue;
        Progress.Value = 0f;
        openingPlayer = null;
    }

    public void OpenChest()
    {
        if (!IsServer || !IsSpawned || opened || Progress.Value < 1f || cubePrefab == null) return;
        opened = true;
        int count = Mathf.Max(1, CubeCount);
        for (int i = 0; i < count; i++)
        {
            // Distribucion uniforme: el quinto cubo no se superpone al primero.
            Vector3 spawnPoint = transform.position +
                Quaternion.Euler(0f, i * 360f / count, 0f) * new Vector3(2f, 1.5f, 0f);
            NetworkObject cube = Instantiate(cubePrefab, spawnPoint, Random.rotation);
            cube.Spawn();
        }
        // El cofre es un objeto colocado en escena, no un prefab instanciado en runtime.
        NetworkObject.Despawn(false);
    }
}
