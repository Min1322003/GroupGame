using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Health")]
    public float maxHealth = 100f;
    public float damagePerSecond = 10f;


    private Rigidbody2D rb;
    private Animator animator;

    private Vector2 moveInput;

    private bool isDead = false;

    private Vector2 spawnPosition;
    private float currentHealth;

    private bool isInputLocked = false;
    private PlayerCombatStats combatStats;

    void Awake()
{
    rb = GetComponent<Rigidbody2D>();
    animator = GetComponent<Animator>();
    combatStats = GetComponent<PlayerCombatStats>();
}

    void Start()
    {
        spawnPosition = transform.position;
        currentHealth = maxHealth;
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
        currentHealth = maxHealth;

        transform.position = spawnPosition;
        rb.linearVelocity = Vector2.zero;

        GetComponent<SpriteRenderer>().enabled = true;

        Debug.Log("Player respawned!");
    }
}