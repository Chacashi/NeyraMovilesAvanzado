using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI local, disponible ANTES de iniciar la red. Canvas/TMP/Buttons viven en el prefab.
public class UIManager : MonoBehaviour
{
    [SerializeField] private Button guardianButton;
    [SerializeField] private Button explorerButton;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text feedbackText;
    private PlayerAssigner assigner;
    private PlayerRole selectedRole;
    private bool hasSelection;
    private float nextRefresh;

    private void Awake()
    {
        assigner = FindFirstObjectByType<PlayerAssigner>();
        guardianButton.onClick.AddListener(SelectGuardian);
        explorerButton.onClick.AddListener(SelectExplorer);
        hostButton.onClick.AddListener(StartServer);
        clientButton.onClick.AddListener(StartClient);
        disconnectButton.onClick.AddListener(Disconnect);
        ShowMessage("Elige un personaje antes de conectarte.");
        RefreshUI();
    }

    private void Start()
    {
        assigner.Manager.OnClientDisconnectCallback += OnDisconnected;
        assigner.Manager.OnClientConnectedCallback += OnConnected;
    }

    public void SelectGuardian() => Select(PlayerRole.Guardian);
    public void SelectExplorer() => Select(PlayerRole.Explorer);
    private void Select(PlayerRole role)
    {
        if (assigner.Manager.IsListening || assigner.Manager.ShutdownInProgress) return;
        selectedRole = role;
        hasSelection = true;
        ShowMessage(role == PlayerRole.Guardian ? "Guardian: puedes crear cofres con B." : "Explorador: abre cofres y recoge el botin.");
        RefreshUI();
    }

    // Se conservan los metodos de arranque del profesor.
    public void StartServer() => Connect(true);
    public void StartClient() => Connect(false);
    private void Connect(bool host)
    {
        if (!hasSelection || assigner.Manager.IsListening || assigner.Manager.ShutdownInProgress) return;
        assigner.PrepareConnection(selectedRole, host);
        ShowMessage("Esperando aprobacion del servidor...");
        bool started = host ? assigner.Manager.StartHost() : assigner.Manager.StartClient();
        if (!started) ShowMessage("No se pudo iniciar la conexion.");
        RefreshUI();
    }

    public void Disconnect()
    {
        assigner.Manager.Shutdown();
        ShowMessage("Desconectado. Puedes elegir otro personaje.");
    }

    private void OnDisconnected(ulong id)
    {
        if (id != assigner.Manager.LocalClientId) return;
        string reason = assigner.Manager.DisconnectReason;
        if (!string.IsNullOrEmpty(reason)) ShowMessage(reason);
    }

    private void OnConnected(ulong id)
    {
        if (id == assigner.Manager.LocalClientId) ShowMessage("Conectado. Usa los controles indicados abajo.");
    }

    public void ShowMessage(string message) => feedbackText.text = message;

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.2f;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (assigner == null) return;
        var manager = assigner.Manager;
        bool listening = manager.IsListening;
        bool busy = listening || manager.ShutdownInProgress;
        guardianButton.interactable = explorerButton.interactable = !busy;
        hostButton.interactable = clientButton.interactable = hasSelection && !busy;
        disconnectButton.interactable = listening && !manager.ShutdownInProgress;
        var playerObject = manager.IsConnectedClient ? manager.LocalClient.PlayerObject : null;
        if (manager.IsConnectedClient && playerObject != null)
        {
            Clase6Player player = playerObject.GetComponent<Clase6Player>();
            int owned = 0;
            foreach (Clase6LootCube cube in FindObjectsByType<Clase6LootCube>(FindObjectsSortMode.None))
                if (cube.IsSpawned && cube.Claimed.Value && cube.OwnerClientId == manager.LocalClientId) owned++;
            statusText.text = $"{(manager.IsHost ? "HOST" : "CLIENTE")} | {player.Role}\nJugador {manager.LocalClientId} | Cubos propios: {owned}";
            InventarioDeRed inventory = playerObject.GetComponent<InventarioDeRed>();
            if (inventory != null && inventory.IsSpawned) statusText.text += $" | Guardados: {inventory.Total}";
        }
        else statusText.text = listening ? "Conectando..." : hasSelection ? $"Seleccionado: {selectedRole}" : "Selecciona Guardian o Explorador";
    }

    private void OnDestroy()
    {
        if (assigner != null && assigner.Manager != null) assigner.Manager.OnClientDisconnectCallback -= OnDisconnected;
        if (assigner != null && assigner.Manager != null) assigner.Manager.OnClientConnectedCallback -= OnConnected;
        guardianButton.onClick.RemoveListener(SelectGuardian);
        explorerButton.onClick.RemoveListener(SelectExplorer);
        hostButton.onClick.RemoveListener(StartServer);
        clientButton.onClick.RemoveListener(StartClient);
        disconnectButton.onClick.RemoveListener(Disconnect);
    }
}
