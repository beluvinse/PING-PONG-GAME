using UnityEngine;
using Random = UnityEngine.Random;

public class BallController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController _matchController;
    [SerializeField] private Transform _playerPaddle;
    [SerializeField] private Transform _opponentPaddle;
    [SerializeField] private BoxCollider _tableCollider;
    [SerializeField] private TrailRenderer _trail;
    [SerializeField] private MeshRenderer _renderer;
    
    [Header("Movement")]
    [SerializeField] private float _gravity = 3.2f;
    [SerializeField] private float _maxSpeed = 3f;
    [SerializeField] private float _bounceForce = 0.85f;
    [SerializeField] private float _maxZ = .6f;
    [SerializeField] float _serveY = -0.2f;
    [SerializeField] float _serveSpeed = 2f;
    [SerializeField] private float _hitOffsetX = 0.2f; 
    [SerializeField] private float _tableMargin = 0.4f;
    [SerializeField] private float _aimMultiplier = 1.5f;
    [SerializeField] private float _assistBlend = 0.7f;
    [SerializeField] private float _minFinalSpeed = 0.8f;
    [SerializeField] private float _maxFinalSpeed = 2f;
    
    private Vector3 _velocity;
    private float _ballServePosOpponent;
    private float _ballServePosPlayer;
    
    private void Awake()
    {
        var bounds = _tableCollider.bounds;
        _ballServePosPlayer =  bounds.max.x + .05f;
        _ballServePosOpponent = bounds.min.x - .05f;
        _matchController.OnBallServed += SetBallForServe;
        _matchController.OnRallyStarted += RallyStarted;
        _trail.emitting = false;
    }

    private void RallyStarted()
    {
        _velocity = Vector3.zero;
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

        var dir = side == MatchController.Side.Player ? Vector3.left : Vector3.right;

        dir.y = _serveY;
        dir.Normalize();

        _velocity = dir * _serveSpeed;
        _matchController.SetBallServed();
        _trail.emitting = true;
    }
    
    private void HandleServe()
    {
        _velocity = Vector3.zero;
        
        transform.position = _matchController.server == MatchController.Side.Player ? 
            new Vector3(_ballServePosPlayer, _playerPaddle.position.y, _playerPaddle.position.z)
            : new Vector3(_ballServePosOpponent, _opponentPaddle.position.y, _opponentPaddle.position.z);
    }

    private void MoveBall()
    {
        _velocity.y -= _gravity * Time.deltaTime;
        _velocity = Vector3.ClampMagnitude(_velocity, _maxSpeed);
        transform.position += _velocity * Time.deltaTime;
    }

    public MatchController.Side CheckCurrentSide()
    {
        var isPlayerSide = transform.position.x > _tableCollider.bounds.center.x;
        
        return isPlayerSide? MatchController.Side.Player : MatchController.Side.AI;
    }
    
    private void CheckTableBounce()
    {
        if (!IsAboveTable())
            return;

        var bounds = _tableCollider.bounds;
        var tableY = bounds.max.y;

        if (!(transform.position.y <= tableY)) return;
        
        var pos = transform.position;
        pos.y = tableY;
        transform.position = pos;
        _velocity.y = Mathf.Abs(_velocity.y) * _bounceForce;

        _matchController.RegisterBounce(CheckCurrentSide());
    }

    private bool IsAboveTable()
    {
        var bounds = _tableCollider.bounds;
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
        var normalized = Mathf.Clamp01(paddleVelocity.magnitude / _maxSpeed);
        normalized = Mathf.Sqrt(normalized);
        var extraPower = Mathf.Lerp(0.05f, 0.4f, normalized);
        return Mathf.InverseLerp(0.05f, 0.4f, extraPower);
    }

    private float CalculateDynamicMaxZ(Transform paddleTransform)
    {
        var bounds = _tableCollider.bounds;
        var halfWidth = (bounds.max.x - bounds.min.x) * 0.5f;
        var distanceToNet = Mathf.Abs(paddleTransform.position.x - bounds.center.x);
        var t = 1f - Mathf.Clamp01(distanceToNet / halfWidth);
        return Mathf.Lerp(_maxZ / 2f, _maxZ, t);
    }

    private float CalculateZVelocity(Vector3 paddleVelocity, float power01, float dynamicMaxZ)
    {
        var isPlayerSide = CheckCurrentSide() == MatchController.Side.Player;

        return isPlayerSide ? CalculatePlayerZVelocity(paddleVelocity, power01, dynamicMaxZ) : CalculateAIZVelocity(dynamicMaxZ);
    }

    private float CalculatePlayerZVelocity(Vector3 paddleVelocity, float power01, float dynamicMaxZ)
    {
        var playerSide = Mathf.Clamp(paddleVelocity.z, -dynamicMaxZ, dynamicMaxZ);

        var distanceFromCenter = transform.position.z - _tableCollider.bounds.center.z;
        var correction = Mathf.Clamp(-distanceFromCenter, -dynamicMaxZ, dynamicMaxZ);

        var assistAmount = Mathf.Pow(1f - power01, 2f);

        return Mathf.Lerp(playerSide, correction, assistAmount * _assistBlend);
    }

    private float CalculateAIZVelocity(float dynamicMaxZ)
    {
        var bounds = _tableCollider.bounds;
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

        var bounds = _tableCollider.bounds;
        var halfWidth = (bounds.max.x - bounds.min.x) * 0.5f;
        var distanceToNet = Mathf.Abs(paddleTransform.position.x - bounds.center.x);
        var t = 1f - Mathf.Clamp01(distanceToNet / halfWidth);

        var finalSpeed = Mathf.Lerp(_minFinalSpeed, _maxFinalSpeed, power01);
        finalSpeed += Mathf.Lerp(0f, 0.8f, 1f - t);

        dir.Normalize();
        _velocity = dir * finalSpeed;
    }
}