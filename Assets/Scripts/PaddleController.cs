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
    // Added on top of _hitDistance. The scene has the player on 0.03 while the
    // AI sits on 0.05, so the player had the tighter window of the two.
    [SerializeField] private float _hitAssist = 0.035f;
    [SerializeField] private float _paddleSpeed;
    [Tooltip("Extra follow speed per unit of distance to the cursor. 0 = the old constant follow.")]
    [SerializeField] private float _catchUp = 8f;
    [SerializeField] private float _closeDistanceThreshold = 0.4f;
    [SerializeField] private float _closeFollowSpeed = 15f;
    [Tooltip("Opacity while the paddle is not held. Needs the material's Surface Type on Transparent.")]
    [Range(0f, 1f)]
    [SerializeField] private float _inactiveAlpha = 0.4f;

    private const string DEPTH_PREPASS_SHADER = "Custom/DepthPrepass";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MatchController.Side _side;
    private Bounds _bounds;
    private Plane _movePlane;
    private Vector3 _lastPos;
    private Vector3 _paddleVelocity;
    private bool _isDragging;
    private Color _heldColor;
    private bool _hasBaseColor;

    private void Awake()
    {
        _side = MatchController.Side.Player;
        _matchController.OnBallServed += OnBallServed;
        SetupMovementArea(false);
        _isDragging = false;

        AddDepthPrepass();

        _hasBaseColor = _paddleRenderer.material.HasProperty(BaseColorId);
        if (_hasBaseColor)
            _heldColor = _paddleRenderer.material.GetColor(BaseColorId);

        SetHeld(false);
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
            SetHeld(true);
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
            SetHeld(false);
        }

        if (_isDragging)
            MovePaddle();
    }
    
    private void SetHeld(bool held)
    {
        if (!_hasBaseColor) return;

        // The Toon graph routes _BaseColor.a straight to SurfaceDescription.Alpha.
        var color = _heldColor;
        color.a = held ? _heldColor.a : _inactiveAlpha;

        _paddleRenderer.material.SetColor(BaseColorId, color);
    }

    /// <summary>
    /// Gives the paddle a depth-only material so it can draw transparent without
    /// showing the handle buried inside its own head. A renderer handed more
    /// materials than the mesh has submeshes redraws the mesh for each extra
    /// one, and the prepass material sits a queue ahead of the paddle's.
    /// </summary>
    private void AddDepthPrepass()
    {
        var shader = Shader.Find(DEPTH_PREPASS_SHADER);
        if (shader == null)
        {
            Debug.LogWarning($"PaddleController: shader '{DEPTH_PREPASS_SHADER}' not found - "
                             + "a transparent paddle will show its own handle through.", this);
            return;
        }

        var materials = _paddleRenderer.sharedMaterials;
        foreach (var material in materials)
            if (material != null && material.shader == shader) return;   // already set up

        var extended = new Material[materials.Length + 1];
        materials.CopyTo(extended, 0);
        extended[materials.Length] = new Material(shader) { name = "PaddleDepthPrepass" };

        _paddleRenderer.materials = extended;
    }
    
    private void CheckBallHit()
    {
        if (!_matchController.CanHit(_side))
            return;

        if (!IsBallInReach())
            return;

        _ballController.Hit(transform, _paddleVelocity);

        _matchController.RegisterHit(_side);
    }

    private bool IsBallInReach()
    {
        var range = _hitDistance + _hitAssist;

        // Sample along the path the ball travelled this frame: a fast ball can
        // cross the whole hit window between two Updates and slip past a paddle
        // that visually looks like it was right there.
        const int samples = 5;
        var from = _ballController.PreviousPosition;
        var to = _ballController.transform.position;

        for (var i = 0; i <= samples; i++)
        {
            var point = Vector3.Lerp(from, to, i / (float)samples);
            if (Vector3.Distance(_paddleCollider.ClosestPoint(point), point) <= range)
                return true;
        }

        return false;
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

        var position = transform.position;

        // Smoothing this way always trails the cursor by about (cursor speed /
        // rate), so a fast swipe left the paddle dragging behind the mouse.
        // Growing the rate with the gap closes big jumps without making small
        // adjustments twitchy.
        var gap = Vector3.Distance(position, point);
        var followRate = _paddleSpeed * (1f + gap * _catchUp);

        position.x = Mathf.Lerp(position.x, point.x, Smoothing(followRate));
        position.z = Mathf.Lerp(position.z, point.z, Smoothing(followRate));
        position.y = Mathf.Lerp(position.y, point.y, Smoothing(YFollowRate()));

        transform.position = position;
        RotatePaddle();
    }

    /// <summary>
    /// Frame-rate independent smoothing factor. The old `rate * deltaTime` form
    /// covered a different fraction of the gap at 60 and at 144 fps, and blew
    /// past the target outright whenever a frame hitched.
    /// </summary>
    private static float Smoothing(float rate) => 1f - Mathf.Exp(-rate * Time.deltaTime);

    private float YFollowRate()
    {
        var distance = Vector3.Distance(transform.position, _ballController.transform.position);
        return Mathf.Lerp(_yFollowSpeed, _closeFollowSpeed, Closeness(distance));
    }

    /// <summary>1 on top of the ball, fading to 0 at <see cref="_closeDistanceThreshold"/>.</summary>
    private float Closeness(float distanceToBall) =>
        1f - Mathf.Clamp01(distanceToBall / Mathf.Max(0.0001f, _closeDistanceThreshold));
    
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

        // Blend rather than snap at the threshold: crossing it mid-swing jumped
        // the target height between "track the ball" and "hold centre", which is
        // what read as the paddle catching on something.
        var influence = Mathf.Lerp(_yInfluence, 1f, Closeness(distanceToBall));

        return Mathf.Clamp(Mathf.Lerp(_bounds.center.y, ballY, influence), _bounds.min.y, _bounds.max.y);
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

        if (ballServed || !_matchController.IsServer(_side)) return;

        transform.position = new Vector3(_bounds.max.x, transform.position.y, _bounds.center.z);
        ReleasePaddle();
    }

    /// <summary>
    /// Drops the paddle out of the player's hand. A button that was already held
    /// down does not grab it again, so the serve can't go off by accident - they
    /// have to let go and click once more.
    /// </summary>
    private void ReleasePaddle()
    {
        _isDragging = false;
        SetHeld(false);
    }
    
    private void OnDestroy()
    {
        _matchController.OnBallServed -= OnBallServed;
    }
}