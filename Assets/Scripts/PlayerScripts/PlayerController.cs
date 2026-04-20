using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : NetworkBehaviour
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

    private bool isDead = false;

    private Vector2 spawnPosition;
    private float currentHealth;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        spawnPosition = transform.position;
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (!IsOwner) return;
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
        if (isDead || isAttacking) return;
        rb.linearVelocity = moveInput * moveSpeed;

    }

    void HandleInput()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;


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
        Debug.Log(moveInput);
        animator.SetFloat("x", moveInput[0]);
        animator.SetFloat("y", moveInput[1]);
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
        }else{
            if (currentHealth < maxHealth)
            {
                currentHealth = maxHealth;
                Debug.Log($"Back inside boundary! HP: {currentHealth:F1}");
            }
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(new Vector3(circleCenter.x, circleCenter.y, 0f), circleRadius);
    }
}