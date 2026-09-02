using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

// Unico script del tablero: lee el canvas (creado en la escena) y llena el TMP_Text
// con los troncos controlados por CADA jugador y los logs que lleva cada uno.
// Se refresca solo cuando algo cambia (eventos), no cada frame.
public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text boardText;

    private bool dirty = true;

    private void Start()
    {
        if (titleText != null)
        {
            titleText.raycastTarget = false;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Top;
            titleText.text = "TABLERO DE TRONCOS Y LOGS";
        }

        if (boardText != null)
        {
            boardText.raycastTarget = false;
            boardText.alignment = TextAlignmentOptions.TopLeft;
            boardText.lineSpacing = 2;
            boardText.color = Color.white;
        }
    }

    private void OnEnable()
    {
        Player.PlayerIdentityChanged += MarkDirty;
        TreeNode.TreeStateChanged += MarkDirty;
    }

    private void OnDisable()
    {
        Player.PlayerIdentityChanged -= MarkDirty;
        TreeNode.TreeStateChanged -= MarkDirty;
    }

    private void LateUpdate()
    {
        if (!dirty) return;
        dirty = false;
        Refresh();
    }

    private void MarkDirty() => dirty = true;

    private void Refresh()
    {
        if (boardText == null) return;

        var players = new List<Player>();
        foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (player.IsSpawned && player.NetworkObject != null)
                players.Add(player);
        }
        players.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        var trees = new List<TreeNode>();
        foreach (TreeNode tree in FindObjectsByType<TreeNode>(FindObjectsSortMode.None))
        {
            if (tree.IsSpawned && tree.NetworkObject != null)
                trees.Add(tree);
        }
        trees.Sort((a, b) => a.NetworkObjectId.CompareTo(b.NetworkObjectId));

        ulong me = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
            ? NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        var sb = new StringBuilder();
        sb.AppendLine($"Jugadores: {players.Count}   Troncos: {trees.Count}");
        sb.AppendLine("------------------------------------");

        foreach (Player player in players)
        {
            string name = Player.NameByClientId.TryGetValue(player.OwnerClientId, out string n)
                ? n
                : $"Jugador {player.OwnerClientId}";

            Color color = Player.ColorByClientId.TryGetValue(player.OwnerClientId, out Color c)
                ? c
                : Color.gray;

            bool isMe = player.OwnerClientId == me;
            string marker = isMe ? "  <color=#9A9A9A>(Tu)</color>" : "";

            sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{name}</b></color>{marker}");

            int owned = 0;
            foreach (TreeNode tree in trees)
            {
                if (!tree.IsTaken || tree.OwnerClientId != player.OwnerClientId) continue;

                owned++;
                sb.AppendLine($"    <color=#C8C8C8>Tronco #{tree.NetworkObjectId}: {tree.Logs.Value} logs restantes</color>");
            }

            if (owned == 0)
            {
                sb.AppendLine("    <color=#666666>(sin troncos)</color>");
            }
            else
            {
                sb.AppendLine($"    <b>>>> Lleva: {player.LogsCollected.Value} logs</b>");
            }
            sb.AppendLine();
        }

        int freeCount = 0;
        foreach (TreeNode tree in trees)
        {
            if (!tree.IsTaken) freeCount++;
        }

        if (freeCount > 0)
        {
            sb.AppendLine($"<color=#7CFC00><b>Libres: {freeCount}</b></color>");
            foreach (TreeNode tree in trees)
            {
                if (tree.IsTaken) continue;
                sb.AppendLine($"    <color=#7CFC00>Tronco #{tree.NetworkObjectId}: {tree.Logs.Value} logs</color>");
            }
        }
        else
        {
            sb.AppendLine("<color=#FF8F8F>No hay troncos libres</color>");
        }

        boardText.text = sb.ToString();
    }
}