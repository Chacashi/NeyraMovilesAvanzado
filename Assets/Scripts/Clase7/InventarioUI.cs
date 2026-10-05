using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventarioUI : MonoBehaviour
{
    [SerializeField] private TMP_Text contenido;
    [SerializeField] private Button anterior;
    [SerializeField] private Button siguiente;
    [SerializeField] private Button soltar;
    [SerializeField] private Button consumir;
    private InventarioDeRed inventory;

    private void Awake()
    {
        anterior.onClick.AddListener(Anterior);
        siguiente.onClick.AddListener(Siguiente);
        soltar.onClick.AddListener(Soltar);
        consumir.onClick.AddListener(Consumir);
        Refrescar();
    }

    public void Vincular(InventarioDeRed ownerInventory) { inventory = ownerInventory; Refrescar(); }
    public void Desvincular(InventarioDeRed old) { if (inventory == old) { inventory = null; Refrescar(); } }
    private void Anterior() { if (inventory != null) inventory.SeleccionarSiguiente(-1); }
    private void Siguiente() { if (inventory != null) inventory.SeleccionarSiguiente(1); }
    private void Soltar() { if (inventory != null) inventory.SoltarRpc(inventory.TipoSeleccionado); }
    private void Consumir() { if (inventory != null) inventory.ConsumirRpc(inventory.TipoSeleccionado); }

    public void Refrescar()
    {
        bool ready = inventory != null && inventory.IsSpawned && inventory.IsOwner && inventory.Ranuras.Count > 0;
        anterior.interactable = siguiente.interactable = ready && inventory.Ranuras.Count > 1;
        soltar.interactable = consumir.interactable = ready;
        if (!ready) { contenido.text = "INVENTARIO\nVacio\n\nE: recoger / abrir cofre\nR: soltar una unidad"; return; }
        var text = new StringBuilder();
        text.AppendLine($"INVENTARIO  {inventory.Total}/{inventory.Capacidad}");
        foreach (Ranura r in inventory.Ranuras)
            text.AppendLine($"{(r.tipo == inventory.TipoSeleccionado ? ">" : " ")} Cubo (ID {r.tipo})  x{r.cantidad}");
        text.AppendLine("\nE: recoger | R: soltar seleccion");
        contenido.text = text.ToString();
    }

    private void OnDestroy()
    {
        anterior.onClick.RemoveListener(Anterior);
        siguiente.onClick.RemoveListener(Siguiente);
        soltar.onClick.RemoveListener(Soltar);
        consumir.onClick.RemoveListener(Consumir);
    }
}
