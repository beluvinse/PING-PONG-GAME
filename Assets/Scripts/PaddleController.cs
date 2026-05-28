using UnityEngine;


public class PaddleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private BallController ballController;

    [Header("Movement Areas")]
    [SerializeField] private BoxCollider playerArea;
    [SerializeField] private BoxCollider playerServeArea;

    [Header("Serve")]
    [SerializeField] private bool playerServe = false;

    [Header("Y Follow")]
    [SerializeField] private float yFollowSpeed = 6f;

    [SerializeField] private float yInfluence = 0.35f;

    [Header("Rotation")]
    [SerializeField] private Transform paddleVisual;

    [SerializeField] private float centerX = 90f;

    [SerializeField] private float maxTilt = 50f;
    
    
    private Bounds bounds;

    private Plane movePlane;

    private Vector3 lastPos;

    public Vector3 PaddleVelocity { get; private set; }

    private void Update()
    {
        SetupMovementArea();
        MovePaddle();
        
        PaddleVelocity = (transform.position - lastPos) / Time.deltaTime;
        lastPos = transform.position;
    }
    

    private void OnCollisionEnter(Collision other)
    {
        playerServe = false;
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

        //transform.position = point;
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

    void RotatePaddle()
    {
        float t =
            Mathf.InverseLerp(
                bounds.min.z,
                bounds.max.z,
                transform.position.z
            );

        float xRot =
            Mathf.Lerp(
                centerX + maxTilt,
                centerX - maxTilt,
                t
            );

        float yRot = Mathf.Lerp(
            25f,
            -25f,
            t
        );
        
        
        
        paddleVisual.localRotation =
            Quaternion.Euler(
                xRot,
                180f + yRot,
                90f
            );
    }
    
    private void OnTriggerEnter(Collider other)
    {
        BallController ball =
            other.GetComponent<BallController>();

        if (ball == null)
            return;

        ball.Hit(transform, PaddleVelocity);
    }

    public float PaddleSpin { get; set; }
}