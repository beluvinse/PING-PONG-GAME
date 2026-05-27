using UnityEngine;

public class BallController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerPaddle;
    [SerializeField] private Transform opponentPaddle;
    [SerializeField] private BoxCollider tableCollider;

    [Header("Movement")]
    [SerializeField] private float speed;
    [SerializeField] private float gravity = 7f;
    [SerializeField] private float maxSpeed = 7f;

    [Header("Bounce")]
    [SerializeField] private float bounceForce = 0.85f;

    [Header("Arcade Feel")]
    [SerializeField] private float upwardForce = 0.35f;
    [SerializeField] private float sideForce = 2f;


    public float ballServePos;
    private Vector3 velocity;

    private bool waitingServe = true;
    private bool canHit = true;

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            ResetBall();
        }

        if (waitingServe)
        {
            HandleServe();
            return;
        }

        MoveBall();

        CheckTableBounce();
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OpponentServe();
        }
    }

    private void OpponentServe()
    {
        waitingServe = false;

        transform.position = opponentPaddle.position;

        Vector3 dir = Vector3.right;
        dir.y = upwardForce;

        dir.Normalize();

        velocity = dir * speed;
    }
    
    void HandleServe()
    {
        velocity = Vector3.zero;
        transform.position =
            new Vector3(
                ballServePos, // NO seguir X
                playerPaddle.position.y,
                playerPaddle.position.z
            );
    }

    void MoveBall()
    {
        velocity.y -= gravity * Time.deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        transform.position += velocity * Time.deltaTime;
    }

    void CheckTableBounce()
    {
        if (!IsAboveTable())
            return;

        var bounds = tableCollider.bounds;
        var tableY = bounds.max.y;

        if (!(transform.position.y <= tableY)) return;
        
        Vector3 pos = transform.position;
        pos.y = tableY;
        transform.position = pos;
        velocity.y = Mathf.Abs(velocity.y) * bounceForce;
    }

    bool IsAboveTable()
    {
        Bounds bounds = tableCollider.bounds;

        Vector3 pos = transform.position;

        return
            pos.x > bounds.min.x &&
            pos.x < bounds.max.x &&
            pos.z > bounds.min.z &&
            pos.z < bounds.max.z;
    }

    private void OnTriggerEnter(Collider other)
    {
        PaddleController paddle =
            other.GetComponent<PaddleController>();

        if (paddle == null)
            return;

        if (!canHit)
            return;

        if (waitingServe)
            waitingServe = false;
        
        Hit(other.transform);
    }

    [SerializeField] private float paddlePower = 0.15f;
    [SerializeField] private float minHitSpeed = 4f;
    [SerializeField] private float maxHitSpeed = 10f;
    
    void Hit(Transform paddleTransform)
    {
        canHit = false;

        Invoke(nameof(ResetHit), 0.08f);

        PaddleController paddle =
            paddleTransform.GetComponent<PaddleController>();

        // snap adelante de la paleta
        transform.position =
            new Vector3(
                paddleTransform.position.x - 0.2f,
                transform.position.y,
                transform.position.z
            );

        // direccion base
        Vector3 dir = -paddleTransform.right;

        // // influencia lateral
        // float zOffset =
        //     transform.position.z -
        //     paddleTransform.position.z;

        // dir += Vector3.forward *
        //        zOffset *
        //        sideForce;

        // velocidad REAL de la paleta
        Vector3 paddleVel =
            paddle.PaddleVelocity;

        // fuerza basada en movimiento
        var extraPower = paddleVel.magnitude * paddlePower;

        Debug.Log("extra power: " + extraPower);
        Debug.Log("speed: " + speed);
        // velocidad final
        float finalSpeed =
            Mathf.Clamp(
                speed + extraPower,
                minHitSpeed,
                maxHitSpeed
            );
        

        // lift basado en movimiento vertical
        dir.y =
            upwardForce +
            (paddleVel.y * 0.003f);

        // direccion final
        dir.Normalize();

        // aplicar velocidad
        velocity = dir * finalSpeed;
        
        Debug.Log("VELOCITY  " + velocity);
        Debug.Log("DIR  " + dir);
        Debug.Log("FINAL SPEED  " + finalSpeed);
    }
    

    void ResetHit()
    {
        canHit = true;
    }

    void ResetBall()
    {
        waitingServe = true;
        velocity = Vector3.zero;
    }
}