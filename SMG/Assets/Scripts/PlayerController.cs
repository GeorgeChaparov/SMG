using System.Collections;
using System.Collections.Generic;

using UnityEngine.InputSystem;
using UnityEngine;
using System;
using UnityEngine.Tilemaps;
using System.Threading.Tasks;
using UnityEngine.EventSystems;

public class PlayerController : MonoBehaviour
{
    private enum GroundedState
    {
        Grounded,
        Caoyote,
        Jumpable,
        Falling,
    }

    [SerializeField]
    [Tooltip("It is set automatically by getting it from the object, but you can set it on your own if you want to. It will be created if it does not exist!")]
    private Rigidbody2D m_Rigidbody;

    [SerializeField]
    [Tooltip("The size of the ground check area, visualized by a green box")]
    private Vector2 m_GroundChecksize;

    [SerializeField]
    private LayerMask m_GroundLayer;

    [SerializeField]
    private Transform m_GroundCheck;

    [Header("Movement")]

    [SerializeField]
    [Tooltip("The time that the player takes to get to the maximum movement speed")]
    private float m_MovementSpeed;

    [SerializeField]
    [Tooltip("The time that the player takes to get to the maximum movement speed")]
    private float m_MovementAccerelation = 2f;

    [SerializeField]
    [Tooltip("The force that is applied to the rigidbody when the jump button is held down.")]
    private float m_JumpForce;

    [SerializeField]
    private float m_MaxJumpTime = 0.5f;

    [SerializeField]
    [Tooltip("The time that the player will be able to jump after falling of a platform.")]
    private float m_CoyoteTime = 0.1f;

    [SerializeField]
    private float m_FallingAccerelation = 2f;

    [SerializeField]
    private float m_FallingSpeed = 0.2f;   

    [SerializeField]
    private float m_Graviry;

    private float m_JumpStartTime;

    private bool m_ShouldApplyJumpForce = false;

    private Animator m_Animator;

    private HairManager m_HairManager;

    /// <summary>
    /// Holds the horizontal direction of travel of the player character.
    /// </summary>
    private float m_HorizontalDirection;

    /// <summary>
    /// Holds the time when the player character was on the ground
    /// </summary>
    private float m_CaoyoteTimeOfFalling;
    
    /// <summary>
    /// Shows the forward direction of the player.
    /// </summary>
    private bool m_IsFacingRight = true;

    /// <summary>
    /// Shows if the player have performed a jump or not.
    /// </summary>
    private bool m_HaveJumped = false;

    /// <summary>
    /// Holds the time in which the jump button was pressed while falling.
    /// </summary>
    private float m_JumpBuffer;

    /// <summary>
    /// Shows if the player have pressed the jump button and so we have to jump if the player character hits the ground in given time.
    /// </summary>
    private bool m_HaveBufferedJump = false;

    /// <summary>
    /// Shows if the player have been teleported once during the current fall using "safe edge".
    /// </summary>
    private bool m_UseSafeEdge = true;

    [SerializeField]
    private bool m_IsHairStretching = false;
    [SerializeField]
    private float m_HairStretchDistance = 0;

    public bool IsFacingRight => m_IsFacingRight;

    void Start()
    {
        if (!m_Animator)
        {
            if (!TryGetComponent<Animator>(out m_Animator))
            {
                m_Animator = gameObject.AddComponent<Animator>();
            }
        }

        if (!m_Rigidbody)
        {
            if (!TryGetComponent<Rigidbody2D>(out m_Rigidbody))
            {
                m_Rigidbody = gameObject.AddComponent<Rigidbody2D>();
            }
        }

        m_Rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        m_Rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        m_Rigidbody.freezeRotation = true;

        m_HairManager = HairManager.Instance;

        m_HairManager.HairIsStretching += OnHairStretched;
    }

    void Update()
    {
        SafeEdge();

        if (Time.time - m_JumpBuffer < 0.2f && m_HaveJumped == true)
        {
            m_HaveBufferedJump = true;
        }
        else
        {
            m_HaveBufferedJump = false;
        }

        if (!IsGrounded() && m_Rigidbody.velocity.y <= -0.01)
        {
            m_HaveJumped = false;
            m_Animator.SetBool("Jump", false);
        }

        if (!m_IsFacingRight && m_HorizontalDirection > 0f && !m_HairManager.IsPullringHair)
        {
            Flip();
        }
        else if (m_IsFacingRight && m_HorizontalDirection < 0f && !m_HairManager.IsPullringHair)
        {
            Flip();
        }

        if (m_HairManager.IsPullringHair)
        {
            Vector3 hairDir = (m_HairManager.HairPos - transform.position).normalized;

            if (!m_IsFacingRight && hairDir.x > 0)
            {
                Flip();
            }
            else if (m_IsFacingRight && hairDir.x < 0)
            {
                Flip();
            }
        }
    }

    private void FixedUpdate()
    {
        ApplyGravity();

        ApplyMovement();

        GroundedState state = CoyoteTime();

        if (m_HaveBufferedJump && (state == GroundedState.Grounded || state == GroundedState.Caoyote))
        {
            ApplyJumpForce(m_JumpForce * 2);
        }
        
        if (m_ShouldApplyJumpForce)
        {
            if (Time.time - m_JumpStartTime < m_MaxJumpTime)
            {
                ApplyJumpForce();
            }
        }
    }

    private void ApplyMovement() 
    {
        if (m_HairManager.IsPullringHair)
        {
            return;
        }

        if (m_HorizontalDirection != 0)
        {
            m_Animator.SetBool("Move", true);

            if (m_IsHairStretching)
            {
                Vector3 hairDir = (m_HairManager.HairPos - transform.position).normalized;

                if ((m_HorizontalDirection < 0 && hairDir.x > 0) || (m_HorizontalDirection > 0 && hairDir.x < 0))
                {
                    m_Rigidbody.velocity = new Vector2(Mathf.Lerp(m_Rigidbody.velocity.x, -m_HorizontalDirection * m_MovementSpeed * (0.02f / m_HairStretchDistance), Time.fixedDeltaTime * m_MovementAccerelation), m_Rigidbody.velocity.y);
                }
                else
                {
                    m_Rigidbody.velocity = new Vector2(Mathf.Lerp(m_Rigidbody.velocity.x, m_HorizontalDirection * m_MovementSpeed, Time.fixedDeltaTime * m_MovementAccerelation), m_Rigidbody.velocity.y);
                }
            }
            else
            {
                m_Rigidbody.velocity = new Vector2(Mathf.Lerp(m_Rigidbody.velocity.x, m_HorizontalDirection * m_MovementSpeed, Time.fixedDeltaTime * m_MovementAccerelation), m_Rigidbody.velocity.y);
            }
        }
        else
        {
            m_Animator.SetBool("Move", false);
        }
    }

    private void SafeEdge()
    {
        if (IsGrounded() && m_HaveJumped == false)
        {
            return;
        }

        BoxCollider2D box = GetComponent<BoxCollider2D>();


        Collider2D coll = Physics2D.OverlapBox(new Vector2(transform.position.x + box.bounds.extents.x, transform.position.y - box.bounds.extents.y + 0.04f), new Vector2(0.04f, 0.02f), 0, m_GroundLayer);

        if (coll == null)
        {
            return;
        }

        if (m_UseSafeEdge == true)
        {
            Tilemap timelap = coll.GetComponent<Tilemap>();

            Vector2 contactPoint = new Vector2(transform.position.x + box.bounds.extents.x + 0.1f, transform.position.y - box.bounds.extents.y + 0.03f);

            Vector3Int cellPos = timelap.layoutGrid.WorldToCell(new Vector2(contactPoint.x, contactPoint.y + 0.36f));

            Vector3 tilePos = timelap.layoutGrid.CellToWorld(cellPos);

            float safeEdgeYPos = tilePos.y - 0.05f;

            if (contactPoint.y <= tilePos.y && contactPoint.y >= safeEdgeYPos)
            {
                transform.position = new Vector3(transform.position.x, tilePos.y + box.bounds.extents.y + 0.1f, transform.position.z);
                m_UseSafeEdge = false;
            }
        }
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (m_HairManager.IsPullringHair)
        {
            m_ShouldApplyJumpForce = false;
            return;
        }

        if (m_HaveJumped == true)
        {
            if (context.started)
            {
                m_JumpBuffer = Time.time;
            }
        }
        else
        {
            GroundedState state = CoyoteTime();

            if (context.started && (state == GroundedState.Grounded || state == GroundedState.Caoyote))
            {
                m_JumpStartTime = Time.time;
                m_ShouldApplyJumpForce = true;
            }
        }

        if (m_HaveJumped && m_ShouldApplyJumpForce && context.canceled)
        {
            m_ShouldApplyJumpForce = true;
            m_JumpStartTime = 0f;
        }
    }

    private void ApplyJumpForce(float bonusForce = 0f)
    {
        m_CaoyoteTimeOfFalling = 0;
        m_Rigidbody.velocity = new Vector2(m_Rigidbody.velocity.x, m_JumpForce + bonusForce);
        m_HaveJumped = true;
        m_HaveBufferedJump = false;
        m_JumpBuffer = 0f;

        m_Animator.SetBool("Jump", true);
    }

    public async void Shoot(InputAction.CallbackContext context)
    {
        if (!context.started)
        {
            return;
        }

        if (!m_HairManager.HasShotHair)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;

            Vector3 mouseDirection = (mousePos - transform.position);

            if (!m_IsFacingRight && mouseDirection.x > 0f)
            {
                Flip();
            }
            else if (m_IsFacingRight && mouseDirection.x < 0f)
            {
                Flip();
            }

            m_Animator.SetBool("Trowing", true);
            await Task.Delay(100);

            m_Animator.SetBool("Trowing", false);
            m_Animator.SetBool("HairOut", true);
        }
        else
        {
            m_Animator.SetBool("Pulling", true);

            await m_HairManager.RetractHair();

            m_Animator.SetBool("Pulling", false);
            m_Animator.SetBool("HairOut", false);
        }
    }

    private async void ShootHair()
    {
        //THIS METHOD IS CALLED FORM TROW HAIR ANIMATION!!!!
        await m_HairManager.ShootHair();
    }

    private void ApplyGravity()
    {
        GroundedState state = CoyoteTime();

        switch (state)
        {
            case GroundedState.Grounded:
                m_Rigidbody.gravityScale = m_Graviry;
                break;
            case GroundedState.Caoyote:
                if (!m_HaveJumped)
                {
                    m_Rigidbody.gravityScale = m_Graviry + 3f;
                }
                break;
            case GroundedState.Falling:
                if (m_Rigidbody.velocity.y < m_Graviry - 0.1f)
                {
                    m_Rigidbody.gravityScale = m_Rigidbody.gravityScale < m_Graviry + 5f ? m_Graviry + 5f : m_Rigidbody.gravityScale;
                    m_Rigidbody.gravityScale = Mathf.Lerp(m_Rigidbody.gravityScale, m_Rigidbody.gravityScale + m_FallingSpeed, Time.fixedDeltaTime * m_FallingAccerelation);
                }
                else
                {
                    m_Rigidbody.gravityScale = m_Graviry;
                }
                break;
            default:
                break;
        }
    }

    private void Flip()
    {
        m_IsFacingRight = !m_IsFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1f;
        transform.localScale = localScale;
    }

    private void OnHairStretched(bool value, float distance) 
    {
        m_IsHairStretching = value;
        m_HairStretchDistance = distance;
    }

    public void Move(InputAction.CallbackContext context)
    {
        m_HorizontalDirection = context.ReadValue<Vector2>().x;
    }

    private bool IsGrounded()
    {
        return Physics2D.OverlapBox(m_GroundCheck.position, m_GroundChecksize, 0f ,m_GroundLayer);
    }

    private GroundedState CoyoteTime()
    {
        if (IsGrounded())
        {
            m_CaoyoteTimeOfFalling = Time.time;

            if (m_UseSafeEdge == false)
            {
                m_UseSafeEdge = true;
            }

            m_Animator.SetBool("Grounded", true);

            return GroundedState.Grounded;
        }
        else 
        {
            if (Time.time - m_CaoyoteTimeOfFalling < m_CoyoteTime)
            {
                m_Animator.SetBool("Grounded", true);

                return GroundedState.Caoyote;
            }
            else
            {
                m_Animator.SetBool("Grounded", false);

                return GroundedState.Falling;
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawCube(m_GroundCheck.position, m_GroundChecksize);

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        Gizmos.color = Color.red;
        Gizmos.DrawCube(new Vector2(transform.position.x + box.offset.x + box.bounds.extents.x, transform.position.y + box.offset.y - box.bounds.extents.y + 0.04f), new Vector2(0.04f, 0.02f));
    }
}
