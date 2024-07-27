using System.Collections;
using System.Collections.Generic;

using UnityEngine.InputSystem;
using UnityEngine;
using System;
using UnityEngine.Tilemaps;
using System.Threading.Tasks;
using UnityEngine.EventSystems;
using System.Net.NetworkInformation;
using UnityEngine.UIElements;

public class PlayerController : MonoBehaviour
{
    private enum PlayerMovementState
    {
        Normal,
        OnHair,
        OnWall,
        HairWrapped,
    }

    private enum GroundedState
    {
        Grounded,
        Caoyote,
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
    private float m_ClimbingSpeed = 0.2f;

    [SerializeField]
    private float m_SwingForce = 0.2f;

    [SerializeField]
    private Vector3 m_PosToGrabHair;

    [SerializeField]
    private float m_GrabHairRadius = 0.2f;

    [SerializeField]
    private float m_Graviry;

    [SerializeField]
    private float m_Mass = 120;

    private float m_LastMass = 0;

    private float m_JumpStartTime;

    private bool m_IsSwingMovementInit = false;

    private Transform m_SwingMovementLastClosestPart;

    private bool m_ShouldApplyJumpForce = false;

    private Animator m_Animator;

    private TargetJoint2D m_TargetJoint2D;

    private FixedJoint2D m_FixedJoint2D;

    private HairManager m_HairManager;

    /// <summary>
    /// Holds the horizontal direction of travel of the player character.
    /// </summary>
    private float m_HorizontalDirection;

    /// <summary>
    /// Holds the vertical direction of travel of the player character.
    /// </summary>
    private float m_VerticalDirection;

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

    [SerializeField]
    private PlayerMovementState m_MovementState = PlayerMovementState.Normal;

    private bool m_IsHairWrapped = false;

    private bool m_IsOnHair = false;

    public bool IsFacingRight => m_IsFacingRight;

    public bool IsOnHair => m_IsOnHair;

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

        if (!m_TargetJoint2D)
        {
            if (!TryGetComponent<TargetJoint2D>(out m_TargetJoint2D))
            {
                m_TargetJoint2D = gameObject.AddComponent<TargetJoint2D>();
            }
        }

        if (!m_FixedJoint2D)
        {
            if (!TryGetComponent<FixedJoint2D>(out m_FixedJoint2D))
            {
                m_FixedJoint2D = gameObject.AddComponent<FixedJoint2D>();
            }
        }

        m_HairManager = HairManager.Instance;

        m_HairManager.HairIsWrapped += OnHairWrapped;
    }

    void Update()
    {
        UpdateMovementState();

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

        if (!m_IsFacingRight && m_HorizontalDirection > 0f && !m_HairManager.IsPullingHair)
        {
            Flip();
        }
        else if (m_IsFacingRight && m_HorizontalDirection < 0f && !m_HairManager.IsPullingHair)
        {
            Flip();
        }

        if (m_HairManager.IsPullingHair)
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
        ApplyMovement();

        ApplyGravity();

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

    private void UpdateMovementState()
    {
        if (!m_IsOnHair)
        {
            m_TargetJoint2D.enabled = false;
            m_IsSwingMovementInit = false;
        }

        if (m_IsHairWrapped)
        {
            if (m_IsOnHair)
            {
                m_MovementState = PlayerMovementState.OnHair;
            }
            else
            {
                m_MovementState = PlayerMovementState.HairWrapped;
            }         
        }
        else
        {
            m_MovementState = PlayerMovementState.Normal;
        }

        if (m_HaveJumped && (m_MovementState == PlayerMovementState.OnHair || m_MovementState == PlayerMovementState.OnWall))
        {
            m_MovementState = PlayerMovementState.Normal;
        }
    }

    private void ApplyMovement() 
    {
        if (m_HairManager.IsPullingHair)
        {
            m_Rigidbody.constraints = RigidbodyConstraints2D.FreezeAll;

            return;
        }
        else if (!m_HairManager.IsPullingHair && !m_HairManager.HasShotHair)
        {
            m_Rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        switch (m_MovementState)
        {
            case PlayerMovementState.Normal:
                NormalMovement();
                break;
            case PlayerMovementState.OnHair:
                SwingMovement();
                break;
            case PlayerMovementState.OnWall:
                WallMovement();
                break;
            case PlayerMovementState.HairWrapped:
                WrappedHairMovement();
                break;
            default:
                break;
        }
    }

    private void NormalMovement()
    {
        m_Rigidbody.mass = m_Mass;

        if (m_HairManager.HasShotHair)
        {
            m_HairManager.ResetPartsMass();
        }
        else
        {
            m_FixedJoint2D.enabled = false;
        }

        if (m_HorizontalDirection == 0)
        {
            m_Animator.SetBool("Move", false);
            return;
        }

        m_Animator.SetBool("Move", true);

        m_Rigidbody.velocity = new Vector2(Mathf.Lerp(m_Rigidbody.velocity.x, m_HorizontalDirection * m_MovementSpeed, Time.fixedDeltaTime * m_MovementAccerelation), m_Rigidbody.velocity.y);
    }

    private void SwingMovement()
    {
        m_Rigidbody.mass = 15;

        Transform closestPart = m_HairManager.GetClosestPart(transform.position);

        if (!closestPart)
        {
            return;
        }

        if (!m_IsSwingMovementInit)
        {
            m_SwingMovementLastClosestPart = closestPart;

            m_HairManager.PlayerHoldingPart(closestPart, m_Rigidbody.mass);

            m_FixedJoint2D.connectedBody = closestPart.GetComponent<Rigidbody2D>();
            m_TargetJoint2D.target = closestPart.position;

            m_IsSwingMovementInit = true;
        }

        if (m_HorizontalDirection != 0)
        {
            if (m_VerticalDirection == 0)
            {
                m_FixedJoint2D.enabled = true;
                m_TargetJoint2D.enabled = false;
                m_FixedJoint2D.connectedBody = closestPart.GetComponent<Rigidbody2D>();

                m_Rigidbody.AddForce(new Vector2(m_HorizontalDirection * m_SwingForce, 0));
                m_TargetJoint2D.target = (transform.position - closestPart.position) + closestPart.position;
                return;
            }
        }

        if (m_VerticalDirection != 0)
        {
            if (closestPart != m_SwingMovementLastClosestPart)
            {
                m_SwingMovementLastClosestPart = closestPart;
                m_HairManager.PlayerHoldingPart(closestPart, m_Rigidbody.mass);
            }
        }

        if (m_VerticalDirection > 0)
        {
            Transform nextPart = m_HairManager.GetNextPart(closestPart.GetComponent<HairPart>().Id);

            if (nextPart.GetComponent<Rigidbody2D>().constraints == RigidbodyConstraints2D.None)
            {
                m_TargetJoint2D.enabled = true;
                m_FixedJoint2D.enabled = false;

                Transform nextNextPart = m_HairManager.GetNextPart(nextPart.GetComponent<HairPart>().Id);

                if (nextNextPart.GetComponent<Rigidbody2D>().constraints == RigidbodyConstraints2D.None)
                {
                    Vector2 newDir = (new Vector2(nextNextPart.position.x, nextNextPart.position.y) - m_TargetJoint2D.target).normalized;

                    newDir.x = 10 * m_ClimbingSpeed * Time.fixedDeltaTime * newDir.x;
                    newDir.y = m_ClimbingSpeed * Time.fixedDeltaTime * newDir.y;

                    m_TargetJoint2D.target += newDir;
                }
                else
                {
                    m_TargetJoint2D.enabled = false;
                    m_FixedJoint2D.enabled = true;

                    m_FixedJoint2D.connectedBody = m_HairManager.GetLastHairPart().GetComponent<Rigidbody2D>();

                    m_TargetJoint2D.target = closestPart.transform.position;
                }   
            }
        }
        else if (m_VerticalDirection < 0)
        {
            m_TargetJoint2D.enabled = true;
            m_FixedJoint2D.enabled = false;

            if (IsGrounded())
            {
                m_IsOnHair = false;

                return;
            }

            Transform prevPart = m_HairManager.GetPrevPart(closestPart.GetComponent<HairPart>().Id);

            Transform prevPrevPart = m_HairManager.GetPrevPart(prevPart.GetComponent<HairPart>().Id);

            Vector2 newDir = Vector2.zero;

            if (prevPart != prevPrevPart)
            {
                Vector2 vec = prevPrevPart.position - prevPart.position;

                Vector2 combinePos = new Vector2(prevPart.position.x, prevPart.position.y) + (vec.normalized * (vec.magnitude / 2));

                newDir = (combinePos - m_TargetJoint2D.target).normalized;
                newDir.y = m_ClimbingSpeed * Time.fixedDeltaTime * newDir.y;
                newDir.x = 10 * m_ClimbingSpeed * Time.fixedDeltaTime * newDir.x;

                m_TargetJoint2D.target += newDir;
            }
            else
            {
                m_TargetJoint2D.enabled = false;
                m_FixedJoint2D.enabled = true;

                m_FixedJoint2D.connectedBody = m_HairManager.GetLastHairPart().GetComponent<Rigidbody2D>();
            }
        }
        else
        {
            m_FixedJoint2D.enabled = true;
            m_FixedJoint2D.connectedBody = closestPart.GetComponent<Rigidbody2D>();
            m_TargetJoint2D.enabled = false;
            m_TargetJoint2D.target = (transform.position - closestPart.position) + closestPart.position;
        }
    }

    private void WallMovement()
    { 
    
    }

    private void WrappedHairMovement()
    {
        m_FixedJoint2D.enabled = true;
        m_FixedJoint2D.connectedBody = m_HairManager.GetLastHairPart().GetComponent<Rigidbody2D>();

        if (m_HorizontalDirection == 0)
        {
            m_Animator.SetBool("Move", false);
            return;
        }

        m_Animator.SetBool("Move", true);

        NormalMovement();
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (m_HairManager.IsPullingHair)
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
            bool jump = false;

            GroundedState state = CoyoteTime();

            if (m_MovementState == PlayerMovementState.OnHair)
            {
                m_IsOnHair = false;

                m_Rigidbody.gravityScale = m_Graviry;
                m_Rigidbody.mass = m_Mass;

                jump = true;
            }

            if (context.started && (state == GroundedState.Grounded || state == GroundedState.Caoyote))
            {
                jump = true;
            }

            if (jump)
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

    public async void Shoot(InputAction.CallbackContext context)
    {
        if (!context.started)
        {
            return;
        }

        if (m_MovementState == PlayerMovementState.OnHair)
        {
            return;
        }

        if (m_MovementState == PlayerMovementState.OnWall && m_HairManager.HasShotHair)
        {
            return;
        }

        m_IsOnHair = false;

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
            if (!IsGrounded())
            {
                return;
            }

            m_Animator.SetBool("Pulling", true);

            await m_HairManager.RetractHair();

            m_Animator.SetBool("Pulling", false);
            m_Animator.SetBool("HairOut", false);
        }
    }

    

    private void ApplyGravity()
    {
        if (m_MovementState == PlayerMovementState.OnWall)
        {
            m_Rigidbody.gravityScale = 0f;
            return;
        }
        else if (m_MovementState == PlayerMovementState.OnHair)
        {
            m_Rigidbody.gravityScale = 20f;

            m_LastMass = m_Mass;
            return;
        }

        GroundedState state = CoyoteTime();

        switch (state)
        {
            case GroundedState.Grounded:
                m_Rigidbody.gravityScale = m_Graviry;
                m_Rigidbody.mass = m_Mass;
                m_LastMass = m_Rigidbody.mass;
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
                    if (m_Rigidbody.gravityScale > 25 || m_Rigidbody.mass < 15)
                    {
                        if (m_MovementState == PlayerMovementState.HairWrapped)
                        {
                            m_IsOnHair = true;
                        }
                        break;
                    }

                    if (m_MovementState == PlayerMovementState.HairWrapped)
                    {
                        if (m_Rigidbody.mass > 15)
                        {
                            m_Rigidbody.mass = m_LastMass;
                            m_LastMass -= 2f;
                        }
                    }

                    m_Rigidbody.gravityScale = m_Rigidbody.gravityScale < m_Graviry + 5f ? m_Graviry + 5f : m_Rigidbody.gravityScale;
                    m_Rigidbody.gravityScale = Mathf.Lerp(m_Rigidbody.gravityScale, m_Rigidbody.gravityScale + m_FallingSpeed, Time.fixedDeltaTime * m_FallingAccerelation);

                    break;
                }

                m_Rigidbody.gravityScale = m_Graviry;
                break;
            default:
                break;
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

    public void GrabOnHair(InputAction.CallbackContext context)
    {
        if (!context.started)
        {
            return;
        }

        if (m_MovementState != PlayerMovementState.HairWrapped)
        {
            return;
        }

        if (Physics2D.OverlapCircle(transform.position + m_PosToGrabHair, m_GrabHairRadius, LayerMask.GetMask("PlayerTale")))
        {
            m_HaveJumped = false;
            m_IsOnHair = true;
        }
    }


    private async void ShootHair()
    {
        //THIS METHOD IS CALLED FORM TROW HAIR ANIMATION!!!!
        await m_HairManager.ShootHair();
    }

    private void OnHairWrapped(bool value)
    {
        m_IsHairWrapped = value;
    }

    public void Move(InputAction.CallbackContext context)
    {
        m_HorizontalDirection = context.ReadValue<Vector2>().x;
        m_VerticalDirection = context.ReadValue<Vector2>().y;
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

    private void Flip()
    {
        m_IsFacingRight = !m_IsFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1f;
        transform.localScale = localScale;
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position + m_PosToGrabHair, m_GrabHairRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawCube(m_GroundCheck.position, m_GroundChecksize);

    }
}
