using System.Collections;
using UnityEngine;

public class BallController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController _matchController;
    [SerializeField] private Transform playerPaddle;
    [SerializeField] private Transform opponentPaddle;
    [SerializeField] private BoxCollider tableCollider;

    [Header("Movement")]
    [SerializeField] private float speed;
    [SerializeField] private float gravity = 7f;
    [SerializeField] private float maxSpeed = 7f;

    [Header("Bounce")]
    [SerializeField] private float bounceForce = 0.85f;


    public float ballServePos;
    private Vector3 velocity;
    public Vector3 Velocity => velocity;

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
  
    //TODO: DELETE THIS
    //HANDLE OPPONENT SERVE IN AIPADDLECONTROLLER
    private void OpponentServe()
    {
        waitingServe = false;
        transform.position = opponentPaddle.position;
        Vector3 dir = Vector3.right;
        dir.y = _serveY;
        dir.Normalize();
        velocity = dir * _serveSpeed;
    }
    
    private void HandleServe()
    {
        velocity = Vector3.zero;
        transform.position = new Vector3(ballServePos, playerPaddle.position.y, playerPaddle.position.z);
    }

    private void MoveBall()
    {
        velocity.y -= gravity * Time.deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        transform.position += velocity * Time.deltaTime;
    }

    private void CheckTableBounce()
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

        _matchController.RegisterBounce(CheckCurrentSide());
    }

    private MatchController.Side CheckCurrentSide()
    {
        var isPlayerSide = transform.position.x > tableCollider.bounds.center.x;
        
        return isPlayerSide? MatchController.Side.Player : MatchController.Side.AI;
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
    

    [SerializeField] private float paddlePower;
    
    void Serve()
    {
        var dir = Vector3.left;
        dir.y = _serveY;
        dir.Normalize();

        velocity = dir * _serveSpeed;
        waitingServe = false;
        _matchController.SetBallServed();
    }

    public float _serveY = -0.15f;
    public float _serveSpeed = 2f;
    
    
    public float maxZ = 1;

    public void Hit(Transform paddleTransform, Vector3 paddleVelocity)
{
    if (!canHit)
        return;

    if (waitingServe)
    {
        Serve();
        return;
    }

    canHit = false;
    
    var isPlayerSide = CheckCurrentSide() == MatchController.Side.Player;
    
    var hitOffset = isPlayerSide ? -0.2f : 0.2f;

    transform.position = new Vector3(
        paddleTransform.position.x + hitOffset,
        transform.position.y,
        transform.position.z
    );

    // =========================
    // DIRECCION BASE
    // =========================

    Vector3 dir;

    if (isPlayerSide)
    {
        Debug.Log("PLAYER PEGA");
        // player golpea hacia derecha
        dir = Vector3.left;
    }
    else
    {
        Debug.Log("IA PEGA");
        // IA golpea hacia izquierda
        dir = Vector3.right;
    }

    // =========================
    // POWER
    // =========================

    float normalized =
        paddleVelocity.magnitude / maxSpeed;

    normalized =
        Mathf.Clamp01(normalized);

    normalized =
        Mathf.Sqrt(normalized);

    float extraPower =
        Mathf.Lerp(
            0.05f,
            0.5f,
            normalized
        );

    // =========================
    // DISTANCIA AL NET
    // =========================

    var boundsCenter =
        tableCollider.bounds.center;

    float halfWidth =
        (tableCollider.bounds.max.x -
         tableCollider.bounds.min.x) * 0.5f;

    float distanceToNet =
        Mathf.Abs(
            paddleTransform.position.x -
            boundsCenter.x
        );

    float t =
        1f -
        Mathf.Clamp01(
            distanceToNet / halfWidth
        );

    float dynamicMaxZ =
        Mathf.Lerp(
            maxZ / 2f,
            maxZ,
            t
        );

    float side =
        Mathf.Clamp(
            paddleVelocity.z,
            -dynamicMaxZ,
            dynamicMaxZ
        );

    dir.z = side;

    dir.y = Mathf.Lerp(.4f, .6f, t);
    
    var baseSpeed = Mathf.Lerp(.5f, 2.4f, 1f - t);

    var finalSpeed = baseSpeed + extraPower;
    
    dir.Normalize();

    velocity = dir * finalSpeed;

    Debug.Log("IS PLAYER SIDE: " + isPlayerSide);
    Debug.Log("DIR: " + dir);
    Debug.Log("FINAL SPEED: " + finalSpeed);

    StartCoroutine(ResetHit());
}
    
    private IEnumerator ResetHit()
    {
        yield return new WaitForSeconds(.08f);
        canHit = true;
    }

    void ResetBall()
    {
        waitingServe = true;
        velocity = Vector3.zero;
    }
}