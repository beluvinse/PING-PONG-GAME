using System.Collections;
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
  
    private void OpponentServe()
    {
        waitingServe = false;

        transform.position = opponentPaddle.position;

        Vector3 dir = Vector3.right;
        dir.y = _serveY;
        dir.Normalize();

        velocity = dir * _serveSpeed;
        
        Debug.Log("OPPONENT velocity " + velocity);
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
    

    [SerializeField] private float paddlePower;
    
    void Serve()
    {
        var dir = Vector3.left;
        dir.y = _serveY;
        dir.Normalize();

        velocity = dir * _serveSpeed;
        waitingServe = false;
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

        
        transform.position = new Vector3(
            paddleTransform.position.x - 0.2f,
            transform.position.y,
            transform.position.z
        );

        // TODO: this only works for the player, not the opponent
        Vector3 dir = -paddleTransform.right;

        
        
        
        float normalized = paddleVelocity.magnitude / maxSpeed;

        normalized = Mathf.Sqrt(normalized);

        float extraPower = Mathf.Lerp(0.05f, 0.5f, normalized);
        
        
        var boundsCenter = tableCollider.bounds.center;

        float halfWidth = (tableCollider.bounds.max.x - tableCollider.bounds.min.x) * 0.5f;

        float distanceToNet = Mathf.Abs(paddleTransform.position.x - boundsCenter.x);

      
        float t = 1f - Mathf.Clamp01(distanceToNet / halfWidth);
        
        
        float dynamicMaxZ = Mathf.Lerp(maxZ / 2, maxZ, t);

        float side = Mathf.Clamp(paddleVelocity.z, -dynamicMaxZ, dynamicMaxZ);

        dir.z = side;

        Debug.Log("aaaaaaaaaaa PADDLE VELOCITY EN Z  " + paddleVelocity.z);
        Debug.Log("aaaaaaaaaaa dynamic max z  " + dynamicMaxZ);
        Debug.Log("aaaaaaaaaaa SIDE  " + side);
        
        
        //TODO: explain what this does, 
       //if the hit is closer to the net, the ball goes higher in the y direction, and a bit slower in speed
       //and if the hit is further from the net, the ball has a lower y curve, and goes faster
        dir.y = Mathf.Lerp(.4f, .6f, t);
       
        var baseSpeed = Mathf.Lerp(.5f, 2.4f, 1f - t);

        Debug.Log("BASE SPEED " + baseSpeed);

        var finalSpeed = baseSpeed + extraPower;
        dir.Normalize();
        velocity = dir * finalSpeed;

        Debug.Log("EXTRA POWER: " + extraPower);
        Debug.Log("EXTRA PADDLE VELOCITY MAGNITUDE: " + paddleVelocity.magnitude);
        Debug.Log("FINAL SPEED: " + finalSpeed);
        Debug.Log("VELOCITY FINAL: " + velocity);
        StartCoroutine(ResetHit());
    }
    
    private IEnumerator ResetHit()
    {
        yield return new WaitForSeconds(1f);
        canHit = true;
    }

    void ResetBall()
    {
        waitingServe = true;
        velocity = Vector3.zero;
    }
}