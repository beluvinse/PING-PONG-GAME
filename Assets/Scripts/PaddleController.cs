using UnityEngine;

public class PaddleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController _matchController;
    [SerializeField] private Camera cam;
    [SerializeField] private BallController ballController;
    
    
    [SerializeField] private MatchController.Side _side;

    [Header("Movement Areas")]
    [SerializeField] private BoxCollider playerArea;
    [SerializeField] private BoxCollider playerServeArea;

    [Header("Serve")]
    [SerializeField] private bool playerServe = true;

    [Header("Y Follow")]
    [SerializeField] private float yFollowSpeed = 6f;

    [SerializeField] private float yInfluence = 0.35f;

    [Header("Rotation")]
    [SerializeField] private Transform paddleVisual;

    [SerializeField] private float centerX = 90f;

    [SerializeField] private float maxTilt = 50f;
    [SerializeField] private float hitDistance = 0.35f;
    
    private Bounds bounds;

    private Plane movePlane;

    private Vector3 lastPos;

    public Vector3 PaddleVelocity { get; private set; }

    private void Start()
    {
        playerServe = true;
        _side = MatchController.Side.Player;
    }

    private void Update()
    {
        SetupMovementArea();
        MovePaddle();
        CheckBallHit();
        
        PaddleVelocity = (transform.position - lastPos) / Time.deltaTime;
        lastPos = transform.position;
        
        
        if (Input.GetMouseButtonDown(1))
        {
            playerServe = true;
        }
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            playerServe = false;
        }
    }

    void CheckBallHit()
    {
        if (!_matchController.CanHit(_side))
            return;

        if (ballController == null)
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
        {
            return;
        }   

        ballController.Hit(
            transform,
            PaddleVelocity
        );

        Debug.Log("BALL HIT");

        Invoke(nameof(ResetHit), 0.05f);
    }

    public BoxCollider paddleCollider;

    private void ResetHit()
    {
        _matchController.RegisterHit(_side);
    }

    
    void SetupMovementArea()
    {
        if (!playerServe)
        {
            bounds = playerArea.bounds;

            movePlane =
                new Plane(
                    Vector3.up,
                    playerArea.bounds.center
                );
        }
        else
        {
            bounds = playerServeArea.bounds;

            movePlane =
                new Plane(
                    Vector3.up,
                    playerServeArea.bounds.center
                );
        }
    }

    void MovePaddle()
    {
        Ray ray =
            cam.ScreenPointToRay(
                Input.mousePosition
            );

        if (!movePlane.Raycast(ray, out float enter))
            return;

        Vector3 point =
            ray.GetPoint(enter);

        point.x =
            Mathf.Clamp(
                point.x,
                bounds.min.x,
                bounds.max.x
            );

        point.z =
            Mathf.Clamp(
                point.z,
                bounds.min.z,
                bounds.max.z
            );

        point.y = GetTargetY();

        transform.position =
            Vector3.Lerp(
                transform.position,
                point,
                Time.deltaTime * _paddleMovement
            );

        RotatePaddle();
    }

    [SerializeField] private float _paddleMovement;

    float GetTargetY()
    {
        float ballY = ballController.transform.position.y;

        // si la pelota se fue del rango vertical permitido
        // volver suavemente al centro
        if (ballY < bounds.min.y || ballY > bounds.max.y)
        {
            return Mathf.Lerp(
                transform.position.y,
                bounds.center.y,
                Time.deltaTime * yFollowSpeed
            );
        }

        float targetY =
            Mathf.Lerp(
                bounds.center.y,
                ballY,
                yInfluence
            );

        targetY =
            Mathf.Clamp(
                targetY,
                bounds.min.y,
                bounds.max.y
            );

        return Mathf.Lerp(
            transform.position.y,
            targetY,
            Time.deltaTime * yFollowSpeed
        );
    }

    private void RotatePaddle()
    {
        var t = Mathf.InverseLerp(bounds.min.z, bounds.max.z, transform.position.z);
        
        var centered = (t - 0.5f) * 2f;
        var curved = Mathf.Sign(centered) * Mathf.Sqrt(Mathf.Abs(centered));
        
        var curvedT = (curved + 1f) * 0.5f;
        var xRot = Mathf.Lerp(centerX + maxTilt, centerX - maxTilt, curvedT);

        var yRot = Mathf.Lerp(15f, -15f, t);
        
        paddleVisual.localRotation =
            Quaternion.Euler(
                xRot,
                180f + yRot,
                90f
            );
    }
}