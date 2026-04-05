using UnityEngine;

public class Player : MonoBehaviour
{
    public float moveSpeed = 5f;
    Vector2 movement;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        transform.Translate(movement * moveSpeed * Time.deltaTime);
    }

}
