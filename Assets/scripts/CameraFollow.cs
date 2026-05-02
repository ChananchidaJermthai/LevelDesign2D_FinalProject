using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // ตัวละคร
    public Vector3 offset = new Vector3(0, 5, -10); // ระยะห่างกล้อง
    public float smoothSpeed = 0.125f; // ความนุ่มนวล

    void LateUpdate()
    {
        // คำนวณตำแหน่งที่กล้องควรจะไป
        Vector3 desiredPosition = target.position + offset;
        // ทำให้การเคลื่อนที่นุ่มนวลขึ้น
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        transform.LookAt(target); // ให้กล้องหันมองตัวละครเสมอ
    }
}