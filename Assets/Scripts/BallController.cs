using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class BallController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController _matchController;
    [SerializeField] private Transform playerPaddle;
    [SerializeField] private Transform opponentPaddle;
    [SerializeField] private BoxCollider tableCollider;
    [SerializeField] private TrailRenderer _trail;
    [SerializeField] private MeshRenderer _renderer;
    
    [Header("Movement")]
    [SerializeField] private float gravity = 3.2f;
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float bounceForce = 0.85f;
    [SerializeField] private float maxZ = .6f;
    [SerializeField] float _serveY = -0.2f;
    [SerializeField] float _serveSpeed = 2f;
    [SerializeField] private float _hitOffsetX = 0.2f; 
    [SerializeField] private float _tableMargin = 0.4f;
    [SerializeField] private float _aimMultiplier = 1.5f;
    [SerializeField] private float _assistBlend = 0.7f;
    [SerializeField] private float _minFinalSpeed = 0.8f;
    [SerializeField] private float _maxFinalSpeed = 2f;
    
    private Vector3 velocity;
    private float ballServePosOpponent;
    private float ballServePosPlayer;
    
    private void Awake()
    {
        var bounds = tableCollider.bounds;
        ballServePosPlayer =  bounds.max.x + .05f;
        ballServePosOpponent = bounds.min.x - .05f;
        _matchController.OnBallServed += SetBallForServe;
        _matchController.OnRallyStarted += RallyStarted;
        _trail.emitting = false;
    }

    private void RallyStarted()
    {
        velocity = Vector3.zero;
        _trail.emitting = false;
        _trail.Clear();
        _renderer.enabled = false;
    }

    private void SetBallForServe(bool ballServed)
    {
        if(ballServed) return;
        _renderer.enabled = true;
    }

    private void Update()
    {
        if (!_matchController.ballServed)
        {
            HandleServe();
            return;
        }

        MoveBall();

        CheckTableBounce();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Floor"))
            return;
        
        _matchController.RegisterBallOut();
    }
    
    private void ServeFrom(Transform paddle, MatchController.Side side)
    {
        transform.position = paddle.position;

        Vector3 dir = side == MatchController.Side.Player ? Vector3.left : Vector3.right;

        dir.y = _serveY;
        dir.Normalize();

        velocity = dir * _serveSpeed;
        _matchController.SetBallServed();
        _trail.emitting = true;
    }
    
    private void HandleServe()
    {
        velocity = Vector3.zero;
        
        transform.position = _matchController.server == MatchController.Side.Player ? 
            new Vector3(ballServePosPlayer, playerPaddle.position.y, playerPaddle.position.z)
            : new Vector3(ballServePosOpponent, opponentPaddle.position.y, opponentPaddle.position.z);
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
        
        var pos = transform.position;
        pos.y = tableY;
        transform.position = pos;
        velocity.y = Mathf.Abs(velocity.y) * bounceForce;

        _matchController.RegisterBounce(CheckCurrentSide());
    }

    private bool IsAboveTable()
    {
        var bounds = tableCollider.bounds;
        var pos = transform.position;

        return
            pos.x > bounds.min.x &&
            pos.x < bounds.max.x &&
            pos.z > bounds.min.z &&
            pos.z < bounds.max.z;
    }
    
    public void Hit(Transform paddleTransform, Vector3 paddleVelocity)
    {
        if (!_matchController.ballServed)
        {
            ServeFrom(paddleTransform, _matchController.server);
            return;
        }

        RepositionBallOnHit(paddleTransform);

        var power01 = CalculatePower(paddleVelocity);
        var dynamicMaxZ = CalculateDynamicMaxZ(paddleTransform);
        var zVelocity = CalculateZVelocity(paddleVelocity, power01, dynamicMaxZ);

        ApplyHitVelocity(paddleTransform, power01, zVelocity);
    }

    private void RepositionBallOnHit(Transform paddleTransform)
    {
        var isPlayerSide = CheckCurrentSide() == MatchController.Side.Player;
        var hitOffset = isPlayerSide ? -_hitOffsetX : _hitOffsetX;

        transform.position = new Vector3(
            paddleTransform.position.x + hitOffset,
            transform.position.y,
            transform.position.z
        );
    }

    private float CalculatePower(Vector3 paddleVelocity)
    {
        var normalized = Mathf.Clamp01(paddleVelocity.magnitude / maxSpeed);
        normalized = Mathf.Sqrt(normalized);
        var extraPower = Mathf.Lerp(0.05f, 0.4f, normalized);
        return Mathf.InverseLerp(0.05f, 0.4f, extraPower);
    }

    private float CalculateDynamicMaxZ(Transform paddleTransform)
    {
        var bounds = tableCollider.bounds;
        var halfWidth = (bounds.max.x - bounds.min.x) * 0.5f;
        var distanceToNet = Mathf.Abs(paddleTransform.position.x - bounds.center.x);
        var t = 1f - Mathf.Clamp01(distanceToNet / halfWidth);
        return Mathf.Lerp(maxZ / 2f, maxZ, t);
    }

    private float CalculateZVelocity(Vector3 paddleVelocity, float power01, float dynamicMaxZ)
    {
        var isPlayerSide = CheckCurrentSide() == MatchController.Side.Player;

        return isPlayerSide ? CalculatePlayerZVelocity(paddleVelocity, power01, dynamicMaxZ) : CalculateAIZVelocity(dynamicMaxZ);
    }

    private float CalculatePlayerZVelocity(Vector3 paddleVelocity, float power01, float dynamicMaxZ)
    {
        var playerSide = Mathf.Clamp(paddleVelocity.z, -dynamicMaxZ, dynamicMaxZ);

        var distanceFromCenter = transform.position.z - tableCollider.bounds.center.z;
        var correction = Mathf.Clamp(-distanceFromCenter, -dynamicMaxZ, dynamicMaxZ);

        var assistAmount = Mathf.Pow(1f - power01, 2f);

        return Mathf.Lerp(playerSide, correction, assistAmount * _assistBlend);
    }

    private float CalculateAIZVelocity(float dynamicMaxZ)
    {
        var bounds = tableCollider.bounds;
        var targetZ = Random.Range(bounds.min.z + _tableMargin, bounds.max.z - _tableMargin);
        var aim = targetZ - transform.position.z;
        return Mathf.Clamp(aim * _aimMultiplier, -dynamicMaxZ, dynamicMaxZ);
    }

    private void ApplyHitVelocity(Transform paddleTransform, float power01, float zVelocity)
    {
        var isPlayerSide = CheckCurrentSide() == MatchController.Side.Player;
        var dir = isPlayerSide ? Vector3.left : Vector3.right;

        dir.z = zVelocity;
        dir.y = Mathf.Lerp(1f, 0.3f, power01);

        var bounds = tableCollider.bounds;
        var halfWidth = (bounds.max.x - bounds.min.x) * 0.5f;
        var distanceToNet = Mathf.Abs(paddleTransform.position.x - bounds.center.x);
        var t = 1f - Mathf.Clamp01(distanceToNet / halfWidth);

        var finalSpeed = Mathf.Lerp(_minFinalSpeed, _maxFinalSpeed, power01);
        finalSpeed += Mathf.Lerp(0f, 0.8f, 1f - t);

        dir.Normalize();
        velocity = dir * finalSpeed;
    }
}