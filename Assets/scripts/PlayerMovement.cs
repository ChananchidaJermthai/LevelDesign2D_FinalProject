using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;         // ความเร็วเดิน
    public float gravity = -9.81f;  // แรงโน้มถ่วง
    public float jumpHeight = 2f;   // ความสูงของการกระโดด

    private CharacterController controller;
    private Vector3 velocity;       // เก็บค่าความเร็วแนวดิ่ง (ตกจากที่สูง/กระโดด)
    private bool isGrounded;        // เช็คว่าติดพื้นไหม

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // 1. เช็คว่าตัวละครอยู่บนพื้นหรือไม่
        isGrounded = controller.isGrounded;

        // ถ้าอยู่บนพื้นแล้ว velocity.y เป็นลบ ให้เซ็ตกลับเป็นค่าต่ำๆ เพื่อให้ตัวละครติดพื้นสนิท
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // 2. การเคลื่อนที่แนวราบ (เดิน)
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * speed * Time.deltaTime);

        // 3. การกระโดด (จะทำได้เมื่ออยู่บนพื้นเท่านั้น)
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // สูตรคำนวณแรงกระโดด: v = sqrt(h * -2 * g)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // 4. คำนวณแรงโน้มถ่วง (ดึงตัวละครลงมาตลอดเวลา)
        velocity.y += gravity * Time.deltaTime;

        // สั่งให้ Controller เคลื่อนที่ตามแรงโน้มถ่วง/แรงกระโดด
        controller.Move(velocity * Time.deltaTime);
    }
}