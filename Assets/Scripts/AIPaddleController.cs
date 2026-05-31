using UnityEngine;
using Random = UnityEngine.Random;

public class AIPaddleController : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private MatchController _matchController;
    [SerializeField] private BallController ballController;

    [SerializeField] private BoxCollider movementArea;
    [SerializeField] private BoxCollider paddleCollider;

    [SerializeField] private float yFollowSpeed = 8.5f;

    [Header("Hit")] [SerializeField] private float hitDistance = 0.05f;

    [SerializeField] private MatchController.Side _side;
    [SerializeField] private Transform paddleVisual;
    private Bounds bounds;


    [SerializeField] private float centerX = 90f;

    [SerializeField] private float maxTilt = 50f;
    
    private Vector3 lastPos;
    public Vector3 PaddleVelocity { get; private set; }

    private bool canHitBall = true;
    [SerializeField] private float xMoveSpeed = 8f; 
    [SerializeField] private float zMoveSpeed = 5.5f; 
    [SerializeField] private float yMoveSpeed = 10f;

    private void Awake()
    {
        _side = MatchController.Side.AI;
        bounds = movementArea.bounds;
    }

    void Update()
    {
        if (ballController == null)
            return;

        FollowBall();

        CheckBallHit();

        PaddleVelocity =
            (transform.position - lastPos) /
            Time.deltaTime;

        lastPos = transform.position;
        
        RotatePaddle();
    }
    

void FollowBall()
{
    Vector3 ballPos =
        ballController.transform.position;

    Bounds bounds =
        movementArea.bounds;

    Vector3 targetPos =
        transform.position;

    bool isAITurn =
        _matchController.currentTurn ==
        MatchController.Side.AI;
    
    
    bool bouncedOnMySide =
        _matchController.ballBounced &&
        _matchController.lastBounceSide ==
        MatchController.Side.AI;

    var currentSide =
        ballController.CheckCurrentSide();

    bool isBallOnAISide =
        currentSide ==
        MatchController.Side.AI;

    float distanceToBall =
        Mathf.Abs(
            transform.position.x -
            ballPos.x
        );

    float retreatX = bounds.max.x - 2f;

    float currentXSpeed =
        xMoveSpeed;
    
    if (!isAITurn)
    {
        targetPos.x = retreatX;

        attackSpeedChosen = false;
    }
    else if (isBallOnAISide)
    {
        var attackX = ballPos.x + .2f;
        var prepareX = ballPos.x + .8f;
        
        if (!bouncedOnMySide)
        {
            targetPos.x = retreatX;
        }
        else
        {
            if (!attackSpeedChosen)
                ChooseAttackSpeed();

            var shouldAttack = distanceToBall < .2f;
            
            targetPos.x = shouldAttack ? attackX : prepareX;

            currentXSpeed = currentAttackSpeed;
        }
    }

    targetPos.x =
        Mathf.Clamp(
            targetPos.x,
            bounds.min.x,
            bounds.max.x
        );

    targetPos.z =
        Mathf.Clamp(
            ballPos.z,
            bounds.min.z,
            bounds.max.z
        );

    float targetY;

    if (
        ballPos.y >= bounds.min.y &&
        ballPos.y <= bounds.max.y
    )
    {
        targetY =
            Mathf.Lerp(
                bounds.center.y,
                ballPos.y,
                0.35f
            );
    }
    else
    {
        targetY =
            bounds.center.y;
    }

    targetPos.y = targetY;

    Vector3 currentPos =
        transform.position;

    currentPos.x =
        Mathf.Lerp(
            currentPos.x,
            targetPos.x,
            Time.deltaTime * currentXSpeed
        );

    currentPos.z =
        Mathf.Lerp(
            currentPos.z,
            targetPos.z,
            Time.deltaTime * zMoveSpeed
        );

    currentPos.y =
        Mathf.Lerp(
            currentPos.y,
            targetPos.y,
            Time.deltaTime * yMoveSpeed
        );

    transform.position =
        currentPos;
}
    void CheckBallHit()
    {
        if (!canHitBall)
            return;

        Vector3 closestPoint =
            paddleCollider.ClosestPoint(
                ballController.transform.position
            );

        float distance =
            Vector3.Distance(
                closestPoint,
                ballController.transform.position
            );

        if (distance > hitDistance)
            return;

        canHitBall = false;

        ballController.Hit(
            transform,
            PaddleVelocity
        );
        ResetHit();
    }
    [SerializeField] private float minAttackSpeed = 2f;
    [SerializeField] private float maxAttackSpeed = 8f;

    private float currentAttackSpeed;
    private bool attackSpeedChosen;
    void ChooseAttackSpeed()
    {
        currentAttackSpeed =
            Random.Range(
                minAttackSpeed,
                maxAttackSpeed
            );

        attackSpeedChosen = true;
    }
    private void RotatePaddle()
    {
        var t = Mathf.InverseLerp(bounds.min.z, bounds.max.z, transform.position.z);
        
        var centered = (t - 0.5f) * 2f;
        var curved = Mathf.Sign(centered) * Mathf.Sqrt(Mathf.Abs(centered));
        
        var curvedT = (curved + 1f) * 0.5f;
        var xRot = Mathf.Lerp(centerX + maxTilt, centerX - maxTilt, curvedT);

        var yRot = Mathf.Lerp(-15f, 15f, t);
        
        paddleVisual.localRotation =
            Quaternion.Euler(
                xRot,
                180f + yRot,
                90f
            );
    }
    
    void ResetHit()
    {
        _matchController.RegisterHit(_side);
        canHitBall = true;
    }
}