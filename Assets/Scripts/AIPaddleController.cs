using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class AIPaddleController : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private MatchController _matchController;
    [SerializeField] private BallController _ballController;
    [SerializeField] private Transform _paddleVisual;
    [SerializeField] private BoxCollider _movementArea;
    [SerializeField] private BoxCollider _paddleCollider;
    
    [Header("Values")]
    [SerializeField] private float _hitDistance = 0.05f;
    [SerializeField] private float _maxTilt = 50f;
    [SerializeField] private float _xMoveSpeed = 8f; 
    [SerializeField] private float _zMoveSpeed = 5.5f; 
    [SerializeField] private float _yMoveSpeed = 10f;
    [SerializeField] private float _minAttackSpeed = .2f;
    [SerializeField] private float _maxAttackSpeed = 1f;
    [SerializeField] private float _retreatOffset = 2f;
    [SerializeField] private float _attackOffset = 0.2f;
    [SerializeField] private float _prepareOffset = 0.8f;
    [SerializeField] private float _attackThreshold = 0.2f;
    [SerializeField] private float _yTrackingBias = 0.35f;
    
    private MatchController.Side _side;
    private Bounds _bounds;
    private Vector3 _lastPos;
    private Vector3 _paddleVelocity;
    private float _currentAttackSpeed;
    private bool _attackSpeedChosen;
    private Coroutine _serveRoutine;

    private void Awake()
    {
        _side = MatchController.Side.AI;
        _bounds = _movementArea.bounds;
        _matchController.OnBallServed += OnBallServed;
    }
    
    private void Update()
    {
        RotatePaddle();
        
        CheckBallHit();
        
        if(_matchController.IsServer(_side) && !_matchController.ballServed)
            return;

        FollowBall();
    }

    private void FollowBall()
    {
        var targetPos = CalculateTargetPosition();
        ApplyMovement(targetPos);

        _paddleVelocity = (transform.position - _lastPos) / Time.deltaTime;
        _lastPos = transform.position;
    }

    private Vector3 CalculateTargetPosition()
    {
        var ballPos = _ballController.transform.position;
        var bounds = _movementArea.bounds;
        var targetPos = transform.position;

        var isAITurn = _matchController.currentTurn == MatchController.Side.AI;
        var bouncedOnMySide = _matchController.ballBounced && _matchController.lastBounceSide == MatchController.Side.AI;
        var isBallOnAISide = _ballController.CheckCurrentSide() == MatchController.Side.AI;
        var distanceToBall = Mathf.Abs(transform.position.x - ballPos.x);

        targetPos.x = CalculateTargetX(ballPos, bounds, isAITurn, bouncedOnMySide, isBallOnAISide, distanceToBall);
        targetPos.x = Mathf.Clamp(targetPos.x, bounds.min.x, bounds.max.x);
        targetPos.z = Mathf.Clamp(ballPos.z, bounds.min.z, bounds.max.z);
        targetPos.y = CalculateTargetY(ballPos, bounds, isBallOnAISide);

        return targetPos;
    }



    private float CalculateTargetX(Vector3 ballPos, Bounds bounds, bool isAITurn, bool bouncedOnMySide,
        bool isBallOnAISide, float distanceToBall)
    {
        var retreatX = bounds.max.x - _retreatOffset;

        if (!isAITurn)
        {
            _attackSpeedChosen = false;
            return retreatX;
        }

        if (!isBallOnAISide)
            return retreatX;

        if (!bouncedOnMySide)
            return retreatX;

        if (!_attackSpeedChosen)
            ChooseAttackSpeed();

        var shouldAttack = distanceToBall < _attackThreshold;
        return shouldAttack ? ballPos.x + _attackOffset : ballPos.x + _prepareOffset;
    }

    private float CalculateTargetY(Vector3 ballPos, Bounds bounds, bool isBallOnAISide)
    {
        if (!_matchController.IsRallyActive)
            return bounds.center.y;
        
        return isBallOnAISide ? Mathf.Lerp(bounds.center.y, ballPos.y, _yTrackingBias) : bounds.center.y;
    }

    private void ApplyMovement(Vector3 targetPos)
    {
        var currentPos = transform.position;
        var xSpeed = _attackSpeedChosen ? _currentAttackSpeed : _xMoveSpeed;

        currentPos.x = Mathf.Lerp(currentPos.x, targetPos.x, Time.deltaTime * xSpeed);
        currentPos.z = Mathf.Lerp(currentPos.z, targetPos.z, Time.deltaTime * _zMoveSpeed);
        currentPos.y = Mathf.Lerp(currentPos.y, targetPos.y, Time.deltaTime * _yMoveSpeed);

        transform.position = currentPos;
    }
    
    private void CheckBallHit()
    {
        if (!_matchController.CanHit(_side))
            return;

        var closestPoint = _paddleCollider.ClosestPoint(_ballController.transform.position);

        var distance = Vector3.Distance(closestPoint, _ballController.transform.position);
        
        if (distance > _hitDistance)
            return;
        
        _ballController.Hit(transform, _paddleVelocity);
        
        _matchController.RegisterHit(_side);
    }
    
    private void ChooseAttackSpeed()
    {
        _currentAttackSpeed = Random.Range(_minAttackSpeed, _maxAttackSpeed);
        _attackSpeedChosen = true;
    }
    
    private void RotatePaddle()
    {
        var t = Mathf.InverseLerp(_bounds.min.z, _bounds.max.z, transform.position.z);
        
        var centered = (t - 0.5f) * 2f;
        var curved = Mathf.Sign(centered) * Mathf.Sqrt(Mathf.Abs(centered));
        
        var curvedT = (curved + 1f) * 0.5f;
        var xRot = Mathf.Lerp(90 + _maxTilt, 90 - _maxTilt, curvedT);

        var yRot = Mathf.Lerp(-15f, 15f, t);

        _paddleVisual.localRotation = Quaternion.Euler(xRot, 180f + yRot, 90f);
    }
 
    private void OnBallServed(bool ballServed)
    {
        if (_serveRoutine != null)
            StopCoroutine(_serveRoutine);

        if (_matchController.IsServer(_side) && !ballServed)
        {
            var pos = transform.position;
            pos.x = _bounds.min.x - .4f;
            transform.position = pos;
            
            _serveRoutine = StartCoroutine(ServeRoutine());
        }
    }
    
    private IEnumerator ServeRoutine()
    {
        var serveTargetZ = Random.Range(_bounds.min.z + 0.3f, _bounds.max.z - 0.3f);
        
        while (Mathf.Abs(transform.position.z - serveTargetZ) > 0.05f)
        {
            var pos = transform.position;
            
            pos.z = Mathf.MoveTowards(pos.z, serveTargetZ, _zMoveSpeed * Time.deltaTime);

            transform.position = pos;

            yield return null;
        }
        
        yield return new WaitForSeconds(Random.Range(0.2f, 0.6f));

        while (!_matchController.ballServed)
        {
            var pos = transform.position;

            var targetX = _ballController.transform.position.x + 0.05f;
         
            pos.x = Mathf.MoveTowards(pos.x, targetX, _xMoveSpeed * Time.deltaTime);

            transform.position = pos;

            yield return null;
        }
    }
    
    private void OnDestroy()
    {
        _matchController.OnBallServed -= OnBallServed;
    }
}