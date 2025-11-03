using UnityEngine;

public class Draggable2D : MonoBehaviour
{
    private Rigidbody2D rb;
    private bool isDragging = false;
    private Vector3 mouseOffset;
    private float mouseZ;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnMouseDown()
    {
        // Отключаем физику во время перетаскивания
        rb.gravityScale = 0;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0;
        rb.bodyType = RigidbodyType2D.Kinematic; // отключаем физическое движение

        isDragging = true;
        mouseZ = Camera.main.WorldToScreenPoint(transform.position).z;
        mouseOffset = transform.position - GetMouseWorldPos();
    }

    private void OnMouseUp()
    {
        // Возвращаем физику обратно
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1;
        isDragging = false;
    }

    private void Update()
    {
        if (isDragging)
        {
            Vector3 newPos = GetMouseWorldPos() + mouseOffset;
            transform.position = newPos;
        }
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = mouseZ;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }
}