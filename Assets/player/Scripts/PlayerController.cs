using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Attack")]
    public float attackCooldown = 0.5f;

    [Header("Health")]
    public float maxHealth = 100f;
    public float damagePerSecond = 10f;

    [Header("Boundary")]
    public Vector2 circleCenter = Vector2.zero;
    public float circleRadius = 5f;

    private Rigidbody2D rb;
    private Animator animator;

    private Vector2 moveInput;
    private bool isAttacking = false;
    private float attackTimer = 0f;

    private float currentHealth;
    private bool isDead = false;

    private Vector2 spawnPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        currentHealth = maxHealth;
        spawnPosition = transform.position;
    }

    void Update()
    {
        if (isDead)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                Respawn();
            return;
        }

        HandleAttackTimer();
        HandleInput();
        UpdateAnimator();
        CheckBoundary();
    }

    void FixedUpdate()
    {
        if (isDead) return;

        if (!isAttacking)
            rb.linearVelocity = moveInput * moveSpeed;
        else
            rb.linearVelocity = Vector2.zero;
    }

    void HandleInput()
    {
        if (!isAttacking)
        {
            moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            ).normalized;
        }
        else
        {
            moveInput = Vector2.zero;
        }

        if (Input.GetKeyDown(KeyCode.J) && !isAttacking)
            StartAttack();
    }

    void StartAttack()
    {
        isAttacking = true;
        attackTimer = attackCooldown;
        animator.SetBool("Attacking", true);
    }

    void HandleAttackTimer()
    {
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                isAttacking = false;
                animator.SetBool("Attacking", false);
            }
        }
    }

    void UpdateAnimator()
    {
        animator.SetFloat("x", moveInput.x);
        animator.SetFloat("y", moveInput.y);
        animator.SetBool("Moving", moveInput.sqrMagnitude > 0);
    }

    void CheckBoundary()
    {
        float distanceFromCenter = Vector2.Distance(transform.position, circleCenter);

        if (distanceFromCenter > circleRadius)
        {
            currentHealth -= damagePerSecond * Time.deltaTime;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

            Debug.Log($"Outside boundary! HP: {currentHealth:F1}");

            if (currentHealth <= 0f)
                Die();
        }
    }

    void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        animator.SetBool("Moving", false);
        animator.SetBool("Attacking", false);

        // Disable the sprite so the player visually disappears
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

    // Draws the boundary in the Scene view for easy visualization
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(new Vector3(circleCenter.x, circleCenter.y, 0f), circleRadius);
    }
}