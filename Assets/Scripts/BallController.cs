using System.Collections;
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
    
    [Header("Impact FX")]
    [SerializeField] private Color _playerHitColor = new Color(0.9f, 0.35f, 0.3f);
    [SerializeField] private Color _aiHitColor = new Color(0.35f, 0.5f, 0.95f);

    [Header("Readability Aids")]
    [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0f, 0.4f);
    [SerializeField] private Color _landingMarkerColor = new Color(1f, 0.5f, 0.35f, 0.75f);
    [SerializeField] private Color _landingAlignedColor = new Color(0.35f, 0.9f, 0.45f, 0.85f);
    [SerializeField] private float _shadowMaxHeight = 0.8f;
    [SerializeField] private float _alignThresholdZ = 0.18f;

    private Vector3 _velocity;
    private float _ballServePosOpponent;
    private float _ballServePosPlayer;
    private Vector3 _baseScale;
    private float _ballDiameter;
    private Coroutine _squashRoutine;
    private Transform _shadowBlob;
    private Transform _landingMarker;
    private Transform _paddleShadow;
    private Material _shadowMaterial;
    private Material _landingMaterial;

    private void Awake()
    {
        var bounds = _tableCollider.bounds;
        _ballServePosPlayer =  bounds.max.x + .05f;
        _ballServePosOpponent = bounds.min.x - .05f;
        _matchController.OnBallServed += SetBallForServe;
        _matchController.OnRallyStarted += RallyStarted;
        _trail.emitting = false;
        _baseScale = transform.localScale;
        _ballDiameter = _renderer.bounds.size.x;
        if (_ballDiameter <= 0.001f) _ballDiameter = 0.04f;
        CreateIndicators();
    }

    private void CreateIndicators()
    {
        var blobShader = Shader.Find("Custom/SoftBlob");
        if (blobShader == null)
        {
            Debug.LogWarning("BallController: shader 'Custom/SoftBlob' not found - ball shadow and landing marker disabled.");
            return;
        }

        _shadowBlob = CreateIndicatorQuad("BallShadow", blobShader, _shadowColor, 0f, out _shadowMaterial);
        _landingMarker = CreateIndicatorQuad("LandingMarker", blobShader, _landingMarkerColor, 0.62f, out _landingMaterial);
        _paddleShadow = CreateIndicatorQuad("PaddleShadow", blobShader, new Color(0f, 0f, 0f, 0.28f), 0f, out _);
    }

    private static Transform CreateIndicatorQuad(string name, Shader shader, Color color, float innerRadius, out Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Destroy(go.GetComponent<Collider>());

        material = new Material(shader);
        material.SetColor("_Color", color);
        material.SetFloat("_InnerRadius", innerRadius);

        var quadRenderer = go.GetComponent<MeshRenderer>();
        quadRenderer.sharedMaterial = material;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;

        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.SetActive(false);
        return go.transform;
    }

    private void LateUpdate()
    {
        UpdateShadowBlob();
        UpdateLandingMarker();
        UpdatePaddleShadow();
    }

    private void UpdatePaddleShadow()
    {
        if (_paddleShadow == null || _playerPaddle == null) return;

        // Fixed-height shadow under the player paddle so its depth (z) can be
        // compared against the ball shadow and the landing ring.
        var bounds = _tableCollider.bounds;
        var show = _playerPaddle.gameObject.activeInHierarchy
                   && _playerPaddle.position.z > bounds.min.z - 0.3f
                   && _playerPaddle.position.z < bounds.max.z + 0.3f;

        _paddleShadow.gameObject.SetActive(show);
        if (!show) return;

        _paddleShadow.position = new Vector3(_playerPaddle.position.x, bounds.max.y + 0.002f, _playerPaddle.position.z);
        var size = _ballDiameter * 3.2f;
        _paddleShadow.localScale = new Vector3(size, size, 1f);
    }

    private void UpdateShadowBlob()
    {
        if (_shadowBlob == null) return;

        var show = _renderer.enabled && IsAboveTable();
        _shadowBlob.gameObject.SetActive(show);
        if (!show) return;

        var tableY = _tableCollider.bounds.max.y;
        var height01 = Mathf.Clamp01((transform.position.y - tableY) / _shadowMaxHeight);

        _shadowBlob.position = new Vector3(transform.position.x, tableY + 0.003f, transform.position.z);

        // Higher ball -> bigger, fainter shadow
        var size = _ballDiameter * Mathf.Lerp(1.4f, 2.4f, height01);
        _shadowBlob.localScale = new Vector3(size, size, 1f);

        var color = _shadowColor;
        color.a = _shadowColor.a * Mathf.Lerp(1f, 0.4f, height01);
        _shadowMaterial.SetColor("_Color", color);
    }

    private void UpdateLandingMarker()
    {
        if (_landingMarker == null) return;

        // Only when the ball is in flight and coming toward the player (+x side)
        var landingPos = Vector3.zero;
        var show = _matchController.ballServed && _velocity.x > 0.05f
                   && TryPredictLanding(out landingPos);

        _landingMarker.gameObject.SetActive(show);
        if (!show) return;

        _landingMarker.position = landingPos;
        var size = _ballDiameter * 3f;
        _landingMarker.localScale = new Vector3(size, size, 1f);

        // Green when the paddle is depth-aligned with where the ball will land
        if (_landingMaterial != null && _playerPaddle != null)
        {
            var aligned = Mathf.Abs(_playerPaddle.position.z - landingPos.z) < _alignThresholdZ;
            _landingMaterial.SetColor("_Color", aligned ? _landingAlignedColor : _landingMarkerColor);
        }
    }

    private bool TryPredictLanding(out Vector3 landingPos)
    {
        landingPos = default;

        var bounds = _tableCollider.bounds;
        var tableY = bounds.max.y;
        var heightAboveTable = transform.position.y - tableY;
        if (heightAboveTable <= 0f) return false;

        // Ballistic time to reach table height: y0 + vy*t - g*t^2/2 = 0
        var vy = _velocity.y;
        var discriminant = vy * vy + 2f * _gravity * heightAboveTable;
        var time = (vy + Mathf.Sqrt(discriminant)) / _gravity;

        var landX = transform.position.x + _velocity.x * time;
        var landZ = transform.position.z + _velocity.z * time;

        // Only mark landings on the player's half of the table
        if (landX < bounds.center.x || landX > bounds.max.x) return false;
        if (landZ < bounds.min.z || landZ > bounds.max.z) return false;

        landingPos = new Vector3(landX, tableY + 0.004f, landZ);
        return true;
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

        ImpactEffects.Instance.EmitBurst(transform.position, Color.white, 6);
        Squash(1.25f, 0.6f);
    }

    private void Squash(float xzScale, float yScale, float duration = 0.09f)
    {
        if (_squashRoutine != null)
            StopCoroutine(_squashRoutine);

        _squashRoutine = StartCoroutine(SquashRoutine(xzScale, yScale, duration));
    }

    private IEnumerator SquashRoutine(float xzScale, float yScale, float duration)
    {
        var squashed = new Vector3(_baseScale.x * xzScale, _baseScale.y * yScale, _baseScale.z * xzScale);
        transform.localScale = squashed;

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(squashed, _baseScale, elapsed / duration);
            yield return null;
        }

        transform.localScale = _baseScale;
        _squashRoutine = null;
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
            ImpactEffects.Instance.EmitBurst(transform.position, Color.white, 8);
            return;
        }

        RepositionBallOnHit(paddleTransform);

        var power01 = CalculatePower(paddleVelocity);
        var dynamicMaxZ = CalculateDynamicMaxZ(paddleTransform);
        var zVelocity = CalculateZVelocity(paddleVelocity, power01, dynamicMaxZ);

        ApplyHitVelocity(paddleTransform, power01, zVelocity);

        var isPlayerHit = CheckCurrentSide() == MatchController.Side.Player;
        ImpactEffects.Instance.EmitBurst(transform.position, isPlayerHit ? _playerHitColor : _aiHitColor, 10);
        Squash(1.3f, 1.3f);

        if (isPlayerHit)
            ImpactEffects.Instance.Shake(0.015f, 0.28f);
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