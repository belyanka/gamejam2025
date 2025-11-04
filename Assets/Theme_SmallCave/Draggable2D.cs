using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Draggable2D : MonoBehaviour
{
    private Rigidbody2D rb;
    private Collider2D col;
    private bool isDragging = false;
    private bool isSticky = false;
    private Vector3 mouseOffset;
    private float mouseZ;

    [Header("Слои окружения")]
    [Tooltip("Слой стен (например, Wall)")]
    public LayerMask wallMask;

    [Tooltip("Слой триггеров-липучек (например, Sticky)")]
    public LayerMask stickyMask;

    [Header("Настройки инерции")]
    [Tooltip("Насколько сильно учитывается движение мыши при броске")]
    public float throwForce = 15f;

    private Vector2 lastMouseWorldPos;
    private Vector2 mouseVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    private void OnMouseDown()
    {
        // Если объект был приклеен — отклеиваем
        if (isSticky)
        {
            rb.gravityScale = 1;
            isSticky = false;
        }

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0;
        isDragging = true;

        mouseZ = Camera.main.WorldToScreenPoint(transform.position).z;
        mouseOffset = transform.position - GetMouseWorldPos();

        lastMouseWorldPos = GetMouseWorldPos();
        mouseVelocity = Vector2.zero;
    }

    private void OnMouseUp()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;

        if (isSticky)
        {
            rb.gravityScale = 0;
            rb.velocity = Vector2.zero; // не бросаем, если "прилип"
        }
        else
        {
            rb.gravityScale = 1;
            rb.velocity = mouseVelocity * throwForce; // применяем инерцию
        }

        isDragging = false;
    }

    private void FixedUpdate()
    {
        if (!isDragging) return;

        Vector3 mouseWorld = GetMouseWorldPos() + mouseOffset;
        Vector2 targetPos = new Vector2(mouseWorld.x, mouseWorld.y);
        Vector2 currentPos = rb.position;
        Vector2 direction = targetPos - currentPos;
        float distance = direction.magnitude;

        // --- Запоминаем скорость движения мыши ---
        Vector2 currentMouseWorld = GetMouseWorldPos();
        mouseVelocity = (currentMouseWorld - lastMouseWorldPos) / Time.fixedDeltaTime;
        lastMouseWorldPos = currentMouseWorld;

        if (distance > 0f)
        {
            direction.Normalize();

            RaycastHit2D hit = Physics2D.BoxCast(
                currentPos,
                col.bounds.size,
                0f,
                direction,
                distance,
                wallMask
            );

            if (hit.collider == null)
            {
                rb.MovePosition(targetPos);
            }
            else
            {
                float safeDistance = hit.distance - 0.01f;
                Vector2 safePos = currentPos + direction * Mathf.Max(safeDistance, 0f);
                rb.MovePosition(safePos);
            }
        }
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = mouseZ;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & stickyMask) != 0)
        {
            rb.gravityScale = 0;
            rb.velocity = Vector2.zero;
            isSticky = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & stickyMask) != 0)
        {
            if (!isDragging)
                rb.gravityScale = 1;
            isSticky = false;
        }
    }
}
