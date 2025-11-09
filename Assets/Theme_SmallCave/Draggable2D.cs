using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Draggable2D : MonoBehaviour
{
    public enum SquareType
    {
        Normal,
        Blue,
        Red
    }
    
    private Rigidbody2D rb;
    private Collider2D col;
    private bool isDragging = false;
    private bool isSticky = false;
    private bool isGoingToExplode = false;
    private Vector3 mouseOffset;
    private float mouseZ;

    public bool IsStickable = false;
    
    [Header("Тип квадрата")]
    public SquareType type;
    
    [Header("Слои окружения")]
    [Tooltip("Слой стен (например, Wall)")]
    public LayerMask wallMask;

    [Tooltip("Слой триггеров-липучек (например, Sticky)")]
    public LayerMask stickyMask;

    [Header("Настройки инерции")]
    [Tooltip("Насколько сильно учитывается движение мыши при броске")]
    public float throwForce = 15f;
    
    [Tooltip("Скорость вращения при удержании (градусов/сек)")]
    public float rotationSpeed = 120f;
    
    public ItemAnimator animator;

    public float explosionDelay = 3f;
    public float detectionRadius = 2.0f; 

    
    // Маска объектов, которые можно сканировать
    [SerializeField] private LayerMask detectionMask;
    
    private Vector2 lastMouseWorldPos;
    private Vector2 mouseVelocity;
    
    // Храним активные взрывы (чтобы не запускать дубли)
    private static Dictionary<(Draggable2D, Draggable2D), Coroutine> activeExplosions = new();


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        
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
        
        gameObject.layer = LayerMask.NameToLayer("Dragging");

        mouseZ = Camera.main.WorldToScreenPoint(transform.position).z;
        mouseOffset = transform.position - GetMouseWorldPos();

        lastMouseWorldPos = GetMouseWorldPos();
        mouseVelocity = Vector2.zero;
    }

    private void OnMouseUp()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        
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

        gameObject.layer = LayerMask.NameToLayer("Item");
        isDragging = false;
    }

    private void FixedUpdate()
    {
        if (isDragging)
        {
            HandleRotationInput();
        }
        
        CheckForConflictProximity();
        
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

        if (distance <= 0f)
            return;

        direction.Normalize();

        RaycastHit2D[] hits = new RaycastHit2D[4];
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(wallMask);
        filter.useTriggers = false;

        int hitCount = rb.Cast(direction, filter, hits, distance);

        if (hitCount == 0)
        {
            // Свободное движение
            rb.MovePosition(targetPos);
        }
        else
        {
            // Есть препятствие
            float safeDistance = hits[0].distance - 0.01f;
            Vector2 safePos = currentPos + direction * Mathf.Max(safeDistance, 0f);
            rb.MovePosition(safePos);
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
        if ((((1 << other.gameObject.layer) & stickyMask) != 0) && IsStickable)
        {
            rb.gravityScale = 0;
            rb.velocity = Vector2.zero;
            isSticky = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if ((((1 << other.gameObject.layer) & stickyMask) != 0) && IsStickable)
        {
            if (!isDragging)
                rb.gravityScale = 1;
            isSticky = false;
        }
    }
    
        // --- Проверяем близость к несовместимым квадратам ---
    private void CheckForConflictProximity()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, detectionMask);

        foreach (var hit in hits)
        {
            if (hit.gameObject == this.gameObject) continue;

            Draggable2D other = hit.GetComponent<Draggable2D>();
            if (other == null) continue;

            if (!IsIncompatibleWith(other.type)) continue;

            float dist = Vector2.Distance(rb.position, other.rb.position);

            // Если достаточно близко — начинаем отсчет
            if (dist < detectionRadius)
            {
                var pair = GetOrderedPair(this, other);

                if (!activeExplosions.ContainsKey(pair))
                {
                    Coroutine c = StartCoroutine(ExplosionCountdown(other, pair));
                    activeExplosions[pair] = c;

                    // 🎞 Место для запуска анимации зарядки
                    WarningShake();
                    other.WarningShake();
                }
            }
        }
    }

    private IEnumerator ExplosionCountdown(Draggable2D other, (Draggable2D, Draggable2D) pair)
    {
        float timer = 0f;

        isGoingToExplode = true;
        
        while (timer < explosionDelay)
        {
            if (this == null || other == null)
                yield break;

            float dist = Vector2.Distance(rb.position, other.rb.position);

            if (dist > detectionRadius)
            {
                // ❌ Квадраты разошлись — отменяем взрыв
                StopShake();
                other.StopShake();

                isGoingToExplode = false;
                activeExplosions.Remove(pair);
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // 💥 Взрыв через 3 секунды
        Vector2 explosionPoint = (rb.position + other.rb.position) / 2f;
        CreateExplosion(explosionPoint);

        StopShake();
        other.StopShake();

        isGoingToExplode = false;
        activeExplosions.Remove(pair);
    }

    private void CreateExplosion(Vector2 position)
    {
        float explosionRadius = 2f;
        float explosionForce = 8f;
        float upwardModifier = 0.6f; // ← подброс вверх (0.3–0.6 хорошо смотрится)

        Collider2D[] affected = Physics2D.OverlapCircleAll(position, explosionRadius);
        foreach (var hit in affected)
        {
            Rigidbody2D body = hit.attachedRigidbody;
            if (body == null) continue;

            Vector2 dir = (body.position - position);
            float dist = dir.magnitude;
            if (dist < 0.001f) continue;

            float falloff = 1f - Mathf.Clamp01(dist / explosionRadius);

            // Добавляем немного вертикальной силы
            dir.Normalize();
            dir.y += upwardModifier; 
            dir.Normalize();

            Vector2 force = dir * (explosionForce * falloff);
            body.AddForce(force, ForceMode2D.Impulse);
        }
        
        GameManager.Instance.SpawnExplosion(position);

        StartCoroutine(IgnoreRoutine(affected));

        Debug.DrawRay(position, Vector3.up * 0.5f, Color.red, 1f);
    }

    IEnumerator IgnoreRoutine(Collider2D[] affected)
    {
        Collider2D[] allColliders = GameManager.Instance.GetAllColliders();
        
        // Выключаем столкновения
        foreach (var bustedCol in affected)
        {
            foreach (Collider2D col in allColliders)
            {
                if (col.gameObject.layer == LayerMask.NameToLayer("Shelve"))
                {
                    Physics2D.IgnoreCollision(bustedCol, col, true);
                }
            }
        }

        
        Debug.Log($"Отключено столкновение между Shelve и взорванным");

        yield return new WaitForSeconds(0.2f);

        // Возвращаем обратно
        foreach (var bustedCol in affected)
        {
            foreach (Collider2D col in allColliders)
            {
                if (col.gameObject.layer == LayerMask.NameToLayer("Shelve"))
                {
                    Physics2D.IgnoreCollision(bustedCol, col, false);
                }
            }
        }
        Debug.Log($"Включено столкновение между Shelve и взорванным");
    }
    
    private void HandleRotationInput()
    {
        float rotationDelta = 0f;

        if (Input.GetKey(KeyCode.D))
            rotationDelta = -rotationSpeed * Time.fixedDeltaTime; // по часовой вокруг Z

        if (Input.GetKey(KeyCode.A))
            rotationDelta = rotationSpeed * Time.fixedDeltaTime;  // против часовой вокруг Z

        if (Mathf.Abs(rotationDelta) > 0.001f)
        {
            // Явное вращение по оси Z
            float newAngle = rb.rotation + rotationDelta;
            rb.MoveRotation(newAngle);
        }
    }

    private bool IsIncompatibleWith(SquareType other)
    {
        // 🔴 Пример правил: синий не любит синий, красный не любит красный
        if ((type == SquareType.Blue && other == SquareType.Blue) ||
            (type == SquareType.Red && other == SquareType.Red))
            return true;

        return false;
    }
    
    // Вспомогательная функция — уникальный ключ для пары квадратов
    private static (Draggable2D, Draggable2D) GetOrderedPair(Draggable2D a, Draggable2D b)
    {
        return a.GetInstanceID() < b.GetInstanceID() ? (a, b) : (b, a);
    }

    private void OnDrawGizmosSelected()
    {
        if (type == SquareType.Blue)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
        else if (type == SquareType.Red)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }

    public void WarningShake()
    {
        animator.WarningShake();
    }

    public void StopShake()
    {
        animator.StopShake();
    }

    public bool CheckStable()
    {
        if (!GameManager.Instance.GetFloorCollider().OverlapPoint(transform.position)&&!isDragging&&rb.velocity.magnitude<0.1f&&!isGoingToExplode)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
