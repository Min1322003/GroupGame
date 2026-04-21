using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;


    private Rigidbody2D rb;
    private Animator animator;

    private Vector2 moveInput;

    private bool isDead = false;

    private Vector2 spawnPosition;
    private float currentHealth;

    private bool isInputLocked = false;
    private PlayerCombatStats combatStats;

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

    void Start()
    {
        spawnPosition = transform.position;
    }

    void Update()
    {
        if (!IsOwner) return;
        if (isInputLocked) return;
        if (isDead)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                Respawn();
            return;
        }
        HandleInput();
        UpdateAnimator();
        CheckBoundary();
    }
    void FixedUpdate()
    {
        if (!IsOwner) return;
        if (isInputLocked) return;
        if (combatStats.isKnockedBack) return;
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
        animator.SetFloat("x", moveInput[0]);
        animator.SetFloat("y", moveInput[1]);
        animator.SetBool("Moving", moveInput.sqrMagnitude > 0);
    }

    public void SetInputLocked(bool locked)
    {
        isInputLocked = locked;
        if (locked)
        {
            moveInput = Vector2.zero;
        }
    }
    void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        animator.SetBool("Moving", false);
        animator.SetBool("Attacking", false);

        GetComponent<SpriteRenderer>().enabled = false;

        Debug.Log("Player died! Press Space to respawn.");
    }

    void Respawn()
    {
        isDead = false;
        combatStats.ResetDamage();

        transform.position = spawnPosition;
        rb.linearVelocity = Vector2.zero;

        GetComponent<SpriteRenderer>().enabled = true;

        Debug.Log("Player respawned!");
    }
    void CheckBoundary()
    {
        float distanceFromCenter = transform.position.magnitude;
        Debug.Log($"Distance from center: {distanceFromCenter:F2}");

        if (distanceFromCenter > boundaryRadius)
        {
            outsideTimer += Time.deltaTime;

            Debug.Log($"Player is outside the boundary! Time outside: {outsideTimer:F2} seconds");

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

        DieClientRpc();
    }
    [ClientRpc]
    void DieClientRpc()
    {
        Die();
    }
}