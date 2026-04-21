using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private PlayerCombatStats combatStats;

    private Vector2 moveInput;
    private Vector2 spawnPosition;

    private bool isDead = false;
    private bool isInputLocked = false;

    [Header("Boundary")]
    public float boundaryRadius = 5f;
    public float maxOutsideTime = 5f;

    private float outsideTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        combatStats = GetComponent<PlayerCombatStats>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            spawnPosition = transform.position;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        if (isDead)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                RequestRespawnServerRpc();

            return;
        }

        if (isInputLocked) return;

        HandleInput();
        UpdateAnimator();
        CheckBoundary();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        if (isInputLocked || isDead) return;
        if (combatStats != null && combatStats.isKnockedBack) return;

        rb.linearVelocity = moveInput * moveSpeed;
    }

    void HandleInput()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;
    }

    void UpdateAnimator()
    {
        animator.SetFloat("x", moveInput.x);
        animator.SetFloat("y", moveInput.y);
        animator.SetBool("Moving", moveInput.sqrMagnitude > 0);
    }

    public void SetInputLocked(bool locked)
    {
        isInputLocked = locked;

        if (locked)
        {
            moveInput = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
        }
    }

    // =========================
    // DEATH SYSTEM (SERVER AUTH)
    // =========================

    void CheckBoundary()
    {
        float distanceFromCenter = transform.position.magnitude;

        if (distanceFromCenter > boundaryRadius)
        {
            outsideTimer += Time.deltaTime;

            if (outsideTimer >= maxOutsideTime && !isDead)
            {
                RequestDeathServerRpc();
            }
        }
        else
        {
            outsideTimer = 0f;
        }
    }

    [ServerRpc]
    void RequestDeathServerRpc()
    {
        if (isDead) return;

        isDead = true;
        DieClientRpc();
    }

    [ClientRpc]
    void DieClientRpc()
    {
        isDead = true;

        rb.linearVelocity = Vector2.zero;

        GetComponent<SpriteRenderer>().enabled = false;

        animator.SetBool("Moving", false);
        animator.SetBool("Attacking", false);
    }

    // =========================
    // RESPAWN SYSTEM
    // =========================

    [ServerRpc]
    void RequestRespawnServerRpc()
    {
        if (!isDead) return;

        isDead = false;

        RespawnClientRpc();
    }

    [ClientRpc]
    void RespawnClientRpc()
    {
        isDead = false;

        rb.position = spawnPosition;
        rb.linearVelocity = Vector2.zero;

        GetComponent<SpriteRenderer>().enabled = true;

        outsideTimer = 0f;
    }
}