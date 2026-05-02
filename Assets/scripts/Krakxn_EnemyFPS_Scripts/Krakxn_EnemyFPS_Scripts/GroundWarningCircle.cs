using UnityEngine;

/// <summary>
/// วาดวงเตือนสีแดงบนพื้นด้วย LineRenderer
/// ใช้กับ EnemyMeleePatrol เพื่อให้วงขยายก่อนทำดาเมจ
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class GroundWarningCircle : MonoBehaviour
{
    [Header("=== Visual ===")]
    public Color circleColor = new Color(1f, 0f, 0f, 0.85f);
    public float lineWidth = 0.08f;
    public int segments = 80;
    public float yOffset = 0.04f;

    private LineRenderer line;

    private void Awake()
    {
        SetupLine();
        Hide();
    }

    private void SetupLine()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = Mathf.Max(12, segments);
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = circleColor;
        line.endColor = circleColor;

        if (line.material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            line.material = new Material(shader);
        }
    }

    public void Draw(Vector3 center, float radius)
    {
        if (line == null) SetupLine();

        radius = Mathf.Max(0.01f, radius);
        line.enabled = true;
        line.positionCount = Mathf.Max(12, segments);

        Vector3 drawCenter = center + Vector3.up * yOffset;

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = (i / (float)line.positionCount) * Mathf.PI * 2f;
            Vector3 point = drawCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            line.SetPosition(i, point);
        }
    }

    public void Hide()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        line.enabled = false;
    }
}
