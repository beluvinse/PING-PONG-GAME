using UnityEngine;

public class AIPaddleController : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private BallController ballController;

    [SerializeField] private BoxCollider movementArea;
    [SerializeField] private BoxCollider paddleCollider;

    [Header("Movement")] [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float yFollowSpeed = 6f;

    [Header("AI")] [SerializeField] private float accuracy = 0.15f;
    [SerializeField] private float reactionDistance = 4f;

    [Header("Hit")] [SerializeField] private float hitDistance = 0.35f;
    [SerializeField] private float hitCooldown = 0.15f;

    private Vector3 lastPos;

    public Vector3 PaddleVelocity { get; private set; }

    private bool canHitBall = true;

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
    }

    void FollowBall()
    {
        Vector3 ballPos =
            ballController.transform.position;

        Bounds bounds =
            movementArea.bounds;

        Vector3 targetPos =
            transform.position;

        float distanceToBall =
            Mathf.Abs(
                transform.position.x -
                ballPos.x
            );

        bool shouldAttack =
            distanceToBall <
            reactionDistance;

        // =========================
        // X
        // =========================

        if (shouldAttack)
        {
            // acercarse para pegar
            targetPos.x =
                Mathf.Clamp(
                    ballPos.x + 0.35f,
                    bounds.min.x,
                    bounds.max.x
                );
        }
        else
        {
            // quedarse atras
            targetPos.x =
                Mathf.Lerp(
                    transform.position.x,
                    bounds.max.x - 0.8f,
                    Time.deltaTime * moveSpeed
                );
        }

        // =========================
        // Z
        // =========================

        float randomOffset =
            Random.Range(
                -accuracy,
                accuracy
            );

        targetPos.z =
            Mathf.Clamp(
                ballPos.z + randomOffset,
                bounds.min.z,
                bounds.max.z
            );

        // =========================
        // Y
        // =========================

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

        targetPos.y =
            Mathf.Lerp(
                transform.position.y,
                targetY,
                Time.deltaTime * yFollowSpeed
            );

        // =========================
        // MOVE
        // =========================

        transform.position =
            Vector3.Lerp(
                transform.position,
                targetPos,
                Time.deltaTime * moveSpeed
            );
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

        // verificar que venga hacia la IA
        // if (ballController.Velocity.x <= 0f)
        //     return;

        canHitBall = false;

        ballController.Hit(
            transform,
            PaddleVelocity
        );

        Invoke(
            nameof(ResetHit),
            hitCooldown
        );
    }

    void ResetHit()
    {
        canHitBall = true;
    }
}