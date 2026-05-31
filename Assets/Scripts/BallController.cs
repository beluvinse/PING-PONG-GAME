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
    [SerializeField] private float gravity = 3.2f;
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float bounceForce = 0.85f;
    [SerializeField] private float maxZ = .6f;
    [SerializeField] private float ballServePos;
    [SerializeField] private float paddlePower;

    
    private Vector3 velocity;
    private bool waitingServe = true;
    private bool canHit = true;
    

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            ResetBall();
            _matchController.StartRally(MatchController.Side.Player);
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

    public MatchController.Side CheckCurrentSide()
    {
        var isPlayerSide = transform.position.x > tableCollider.bounds.center.x;
        
        return isPlayerSide? MatchController.Side.Player : MatchController.Side.AI;
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

    Vector3 dir;

    if (isPlayerSide)
    {
        dir = Vector3.left;
    }
    else
    {
        dir = Vector3.right;
    }


    float normalized = paddleVelocity.magnitude / maxSpeed;

    normalized = Mathf.Clamp01(normalized);

    normalized =
        Mathf.Sqrt(normalized);

    float extraPower = Mathf.Lerp(0.05f, 0.6f, normalized);

    // =========================
    // DISTANCIA AL NET
    // =========================

    var boundsCenter =
        tableCollider.bounds.center;

    float halfWidth =
        (tableCollider.bounds.max.x -
         tableCollider.bounds.min.x) * 0.5f;

    float distanceToNet = Mathf.Abs(paddleTransform.position.x - boundsCenter.x);

    float t = 1f - Mathf.Clamp01(distanceToNet / halfWidth);

    float dynamicMaxZ =
        Mathf.Lerp(
            maxZ / 2f,
            maxZ,
            t
        );

    float side;
    
    float power01 =
        Mathf.InverseLerp(
            0.05f,
            0.5f,
            extraPower
        );
    
    
    if (isPlayerSide)
    {
        float playerSide =
            Mathf.Clamp(
                paddleVelocity.z,
                -dynamicMaxZ,
                dynamicMaxZ
            );

        float centerZ =
            tableCollider.bounds.center.z;

        float distanceFromCenter =
            transform.position.z -
            centerZ;

        float correction =
            -distanceFromCenter;

        correction =
            Mathf.Clamp(
                correction,
                -dynamicMaxZ,
                dynamicMaxZ
            );

        float assistAmount =
            Mathf.Pow(
                1f - power01,
                2f
            );

        side =
            Mathf.Lerp(
                playerSide,
                correction,
                assistAmount * 0.7f
            );
    }
    else
    {
        Bounds bounds =
            tableCollider.bounds;

        float margin = 0.4f;

        float targetZ =
            Random.Range(
                bounds.min.z + margin,
                bounds.max.z - margin
            );

        float aim =
            targetZ -
            transform.position.z;

        side =
            Mathf.Clamp(
                aim * 1.5f,
                -dynamicMaxZ,
                dynamicMaxZ
            );
    }
    
    dir.z = side;

//si le pego fuerte, el arco es menor, si le pego mas despacio el arco es mayor
    float arc =
        Mathf.Lerp(
            1f,
            0.3f,
            power01
        );

    dir.y = arc;
    

    float finalSpeed =
        Mathf.Lerp(
            .8f, 2f,
            power01
        );

// bonus por pegar lejos de la red
    finalSpeed +=
        Mathf.Lerp(0f, .8f, 1f - t);

    dir.Normalize();


    velocity = dir * finalSpeed;
    

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