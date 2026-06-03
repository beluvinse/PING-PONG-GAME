using UnityEngine;

public class PaddleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController _matchController;
    [SerializeField] private Camera _cam;
    [SerializeField] private BallController _ballController;
    [SerializeField] private Transform _paddleVisual;
    [SerializeField] private BoxCollider _paddleCollider;
    [SerializeField] private BoxCollider _playerArea;
    [SerializeField] private BoxCollider _playerServeArea;
    [SerializeField] private Renderer _paddleRenderer;
    
    [Header("Values")]
    [SerializeField] private float _yFollowSpeed = 10f;
    [SerializeField] private float _yInfluence = 0.2f;
    [SerializeField] private float _maxTilt = 50f;
    [SerializeField] private float _hitDistance = 0.05f;
    [SerializeField] private float _paddleSpeed; 
    [SerializeField] private float _closeDistanceThreshold = 0.4f;
    [SerializeField] private float _closeFollowSpeed = 15f;
    [SerializeField] private float _inactiveAlpha = 0.4f;
        
    private MatchController.Side _side;
    private Bounds _bounds;
    private Plane _movePlane;
    private Vector3 _lastPos;
    private Vector3 _paddleVelocity;
    private bool _isDragging;
    
    private void Awake()
    {
        _side = MatchController.Side.Player;
        _matchController.OnBallServed += OnBallServed;
        SetupMovementArea(false);
        _isDragging = false;

        SetAlpha(_inactiveAlpha);
    }

    private void Update()
    {
        HandleInput();
        
        CheckBallHit();
        
        _paddleVelocity = (transform.position - _lastPos) / Time.deltaTime;
        _lastPos = transform.position;
    }
    
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _isDragging = true;
            SetAlpha(1f);
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
            SetAlpha(_inactiveAlpha);
        }

        if (_isDragging)
            MovePaddle();
    }
    
    private void SetAlpha(float alpha)
    {
        var color =
            _paddleRenderer.material.color;

        color.a = alpha;

        _paddleRenderer.material.color =
            color;
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
    
    private void SetupMovementArea(bool ballServed)
    { 
        var area = (_matchController.IsServer(_side) && !ballServed) ? _playerServeArea : _playerArea;
        _bounds = area.bounds;
        _movePlane = new Plane(Vector3.up, area.bounds.center);
    }
    
    private void MovePaddle()
    {
        var ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!_movePlane.Raycast(ray, out var enter)) return;

        var point = ray.GetPoint(enter);
        point.x = Mathf.Clamp(point.x, _bounds.min.x, _bounds.max.x);
        point.z = Mathf.Clamp(point.z, _bounds.min.z, _bounds.max.z);
        point.y = CalculateTargetY();
        
        var ySpeed = Vector3.Distance(transform.position, _ballController.transform.position) < _closeDistanceThreshold ? _closeFollowSpeed : _yFollowSpeed;

        var current = transform.position;
        current.y = Mathf.Lerp(current.y, point.y, Time.deltaTime * ySpeed);
        point.y = current.y;

        transform.position = Vector3.Lerp(transform.position, point, Time.deltaTime * _paddleSpeed);
        RotatePaddle();
    }
    
    private float CalculateTargetY()
    {
        if (!_matchController.ballServed || !_matchController.IsRallyActive)
            return _bounds.center.y;
        
        var ballY = _ballController.transform.position.y;
        var currentSide = _ballController.CheckCurrentSide();
        var isBallOnPlayerSide = currentSide == MatchController.Side.Player;

        if (!isBallOnPlayerSide)
            return _bounds.center.y;

        var distanceToBall = Vector3.Distance(transform.position, _ballController.transform.position);
        
        if (distanceToBall < _closeDistanceThreshold)
            return ballY;

        return Mathf.Clamp(Mathf.Lerp(_bounds.center.y, ballY, _yInfluence), _bounds.min.y, _bounds.max.y);
    }
    
    private void RotatePaddle()
    {
        var t = Mathf.InverseLerp(_bounds.min.z, _bounds.max.z, transform.position.z);
        var centered = (t - 0.5f) * 2f;
        var curved = Mathf.Sign(centered) * Mathf.Sqrt(Mathf.Abs(centered));
        var curvedT = (curved + 1f) * 0.5f;
        var xRot = Mathf.Lerp(90 + _maxTilt, 90 - _maxTilt, curvedT);
        var yRot = Mathf.Lerp(15f, -15f, t);
        
        _paddleVisual.localRotation = Quaternion.Euler(xRot, 180f + yRot, 90f);
    }

    private void OnBallServed(bool ballServed)
    {
        SetupMovementArea(ballServed);
        
        if(!ballServed && _matchController.IsServer(_side))
            transform.position = new Vector3(_bounds.max.x, transform.position.y, _bounds.center.z);
    }
    
    private void OnDestroy()
    {
        _matchController.OnBallServed -= OnBallServed;
    }
}