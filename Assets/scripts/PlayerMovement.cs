using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f; // ความเร็วเดิน

    private CharacterController controller;
    private Vector3 moveDirection;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        float x = Input.GetAxis("Horizontal"); // A/D หรือ ← →
        float z = Input.GetAxis("Vertical");   // W/S หรือ ↑ ↓

        Vector3 move = transform.right * x + transform.forward * z;

        controller.Move(move * speed * Time.deltaTime);
    }
}