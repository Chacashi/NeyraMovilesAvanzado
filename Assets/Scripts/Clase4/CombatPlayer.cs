using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CombatPlayer : NetworkBehaviour
{
    [Header("Estado y reglas del servidor")]
    [Min(1)] public int MaxHealth = 100;
    [Min(1)] public int AttackDamage = 20;
    [Min(0.05f)] public float AttackCooldown = 0.6f;
    [Min(0.1f)] public float AttackRange = 2.8f;
    [Min(0.1f)] public float MoveSpeed = 5f;

    public NetworkVariable<int> Health = new(100,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // Estado derivado, sin duplicar la vida en otra variable de red.
    public bool IsDead => Health.Value <= 0;

    [Header("Referencias visuales guardadas en el prefab")]
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Canvas healthCanvas;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private ParticleSystem hitParticles;
    [SerializeField] private AudioSource hitAudio;
    [SerializeField] private AudioClip hitClip;

    private static AudioClip fallbackHitClip;
    private static readonly Color[] PlayerColors = { Color.cyan, Color.green, new(1f, 0.45f, 0.15f) };
    private InputSystem_Actions inputs;
    private NetworkTickSystem tickSystem;
    private NetworkTransform networkTransform;
    private Material bodyMaterial;
    private Vector2 localMove;
    private Vector2 serverMove;
    private double lastMoveTime;
    private double nextAttackTime;
    private Vector3 spawnPoint;
    private Camera viewCamera;
    private float damageExpires;
    private Vector2 damageLabelOrigin;

    private void Awake()
    {
        networkTransform = GetComponent<NetworkTransform>();
        bodyMaterial = bodyRenderer.material;
        damageLabelOrigin = damageText.rectTransform.anchoredPosition;
        damageText.gameObject.SetActive(false);
        inputs = new InputSystem_Actions();
        inputs.Player.Move.performed += OnMove;
        inputs.Player.Move.canceled += OnMove;
        inputs.Player.Attack.performed += OnAttack;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            spawnPoint = (OwnerClientId % 3) switch
            {
                0 => new Vector3(-3f, 1f, 0f),
                1 => new Vector3(3f, 1f, 0f),
                _ => new Vector3(0f, 1f, 3f)
            };
            transform.position = spawnPoint;
            transform.rotation = Quaternion.LookRotation(new Vector3(-spawnPoint.x, 0f, -spawnPoint.z));
            Health.Value = Mathf.Max(1, MaxHealth);
        }

        Health.OnValueChanged += OnHealthChanged;
        viewCamera = Camera.main;
        RefreshHealth(); // Cliente tardio: pinta el estado recibido, incluidos los muertos.
        if (IsOwner)
        {
            tickSystem = NetworkManager.NetworkTickSystem;
            tickSystem.Tick += SendMovement;
        }
    }

    public override void OnNetworkDespawn()
    {
        Health.OnValueChanged -= OnHealthChanged;
        if (tickSystem != null) tickSystem.Tick -= SendMovement;
        tickSystem = null;
        inputs.Disable();
        localMove = serverMove = Vector2.zero;
    }

    public override void OnDestroy()
    {
        inputs?.Dispose();
        if (bodyMaterial != null) Destroy(bodyMaterial);
        base.OnDestroy();
    }

    private void OnMove(InputAction.CallbackContext context) => localMove = context.ReadValue<Vector2>();

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (IsSpawned && IsOwner && !IsDead) RequestAttackRpc();
    }

    private void SendMovement()
    {
        if (IsSpawned && IsOwner && !IsDead) SubmitMovementRpc(localMove);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
    private void SubmitMovementRpc(Vector2 movement)
    {
        if (!IsServer || !IsSpawned || IsDead ||
            float.IsNaN(movement.x) || float.IsNaN(movement.y) ||
            float.IsInfinity(movement.x) || float.IsInfinity(movement.y)) return;
        serverMove = Vector2.ClampMagnitude(movement, 1f);
        lastMoveTime = Time.timeAsDouble;
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer || IsDead) return;
        if (Time.timeAsDouble - lastMoveTime > 0.5) serverMove = Vector2.zero;
        Vector3 direction = new(serverMove.x, 0f, serverMove.y);
        Vector3 position = transform.position + direction * MoveSpeed * Time.deltaTime;
        position.x = Mathf.Clamp(position.x, -8f, 8f);
        position.z = Mathf.Clamp(position.z, -8f, 8f);
        transform.position = position;
        if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
    }

    // El cliente NO envia objetivo, dano ni posicion de impacto.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestAttackRpc()
    {
        if (!IsServer || !IsSpawned || IsDead || Time.timeAsDouble < nextAttackTime) return;
        nextAttackTime = Time.timeAsDouble + Mathf.Max(0.05f, AttackCooldown);
        Physics.SyncTransforms();

        CombatPlayer target = null;
        float nearest = float.PositiveInfinity;
        foreach (Collider hit in Physics.OverlapSphere(transform.position, AttackRange, ~0, QueryTriggerInteraction.Ignore))
        {
            CombatPlayer candidate = hit.GetComponentInParent<CombatPlayer>();
            if (candidate == null || candidate == this || !candidate.IsSpawned || candidate.IsDead) continue;
            Vector3 offset = candidate.transform.position - transform.position;
            float distance = offset.magnitude;
            if (distance > AttackRange || distance >= nearest || distance < 0.001f) continue;
            if (Vector3.Dot(transform.forward, offset / distance) < 0.25f) continue;
            // Un obstaculo o un tercer jugador puede bloquear la linea de impacto.
            if (Physics.Raycast(transform.position, offset / distance, out RaycastHit obstruction,
                    distance, ~0, QueryTriggerInteraction.Ignore) &&
                obstruction.collider.GetComponentInParent<CombatPlayer>() != candidate) continue;
            target = candidate;
            nearest = distance;
        }

        if (target == null) return;
        int dealt = target.ApplyDamageServer(AttackDamage);
        if (dealt <= 0) return;
        target.PlayHitSoundRpc();
        target.ImpactFeedbackRpc(dealt, target.transform.position + Vector3.up * 0.3f);
    }

    public int ApplyDamageServer(int damage)
    {
        if (!IsServer || !IsSpawned || IsDead || damage <= 0) return 0;
        int dealt = Mathf.Min(Health.Value, damage);
        Health.Value -= dealt;
        return dealt;
    }

    private void OnHealthChanged(int previous, int current) => RefreshHealth();

    private void RefreshHealth()
    {
        healthFill.fillAmount = Mathf.Clamp01((float)Health.Value / Mathf.Max(1, MaxHealth));
        // La barra es un Image sin sprite: reducir sus anclajes tambien evita depender de un sprite Filled.
        healthFill.rectTransform.anchorMax = new Vector2(healthFill.fillAmount, 1f);
        healthText.text = IsDead ? "MUERTO  |  0 HP" : $"Jugador {OwnerClientId}  |  {Health.Value}/{MaxHealth} HP";
        bodyMaterial.color = IsDead ? Color.gray : PlayerColors[OwnerClientId % (ulong)PlayerColors.Length];
        healthFill.color = IsDead ? Color.gray : Color.green;
        if (!IsOwner) return;
        if (IsDead)
        {
            inputs.Disable();
            localMove = Vector2.zero;
        }
        else if (!inputs.Player.enabled) inputs.Player.Enable();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PlayHitSoundRpc()
    {
        if (hitClip == null && fallbackHitClip == null) fallbackHitClip = CreateHitSound();
        hitAudio.PlayOneShot(hitClip != null ? hitClip : fallbackHitClip);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server, Delivery = RpcDelivery.Unreliable)]
    private void ImpactFeedbackRpc(int damage, Vector3 point)
    {
        damageText.text = $"-{damage}";
        damageExpires = Time.time + 0.8f;
        damageText.gameObject.SetActive(true);
        hitParticles.transform.position = point;
        hitParticles.Play();
        for (int i = 0; i < 12; i++)
        {
            var particle = new ParticleSystem.EmitParams
            {
                position = point,
                velocity = Random.onUnitSphere * 2f + Vector3.up,
                startLifetime = 0.35f,
                startSize = 0.1f,
                startColor = new Color(1f, 0.8f, 0.1f)
            };
            hitParticles.Emit(particle, 1);
        }
    }

    private void LateUpdate()
    {
        if (!IsSpawned) return;
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null) healthCanvas.transform.rotation = viewCamera.transform.rotation;
        if (!damageText.gameObject.activeSelf) return;
        float remaining = damageExpires - Time.time;
        damageText.rectTransform.anchoredPosition = damageLabelOrigin + Vector2.up * (0.8f - remaining) * 45f;
        if (remaining <= 0f) damageText.gameObject.SetActive(false);
    }

    public void ResetServer()
    {
        if (!IsServer || !IsSpawned) return;
        serverMove = Vector2.zero;
        nextAttackTime = 0;
        Health.Value = Mathf.Max(1, MaxHealth);
        networkTransform.Teleport(spawnPoint,
            Quaternion.LookRotation(new Vector3(-spawnPoint.x, 0f, -spawnPoint.z)), transform.localScale);
    }

    private static AudioClip CreateHitSound()
    {
        // Sustituible por un AudioClip del Inspector; no se transmite audio por red.
        const int sampleRate = 22050;
        float[] samples = new float[3307];
        var noise = new System.Random(4);
        for (int i = 0; i < samples.Length; i++)
        {
            float time = (float)i / sampleRate;
            float envelope = Mathf.Exp(-time * 35f);
            samples[i] = (Mathf.Sin(2f * Mathf.PI * 180f * time) * 0.5f +
                ((float)noise.NextDouble() * 2f - 1f) * 0.3f) * envelope;
        }
        AudioClip clip = AudioClip.Create("CombatHit", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
