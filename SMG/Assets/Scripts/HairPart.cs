using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;

using static UnityEngine.Rendering.DebugUI;

public class HairPart : MonoBehaviour
{
    private int m_id;

    private bool m_ActivatedWrapFromOther = false;

    private bool m_ShouldWrap = false;

    private bool m_ShouldHoldOn = false;

    private float m_TimeToHoldOn = 0.2f;

    private float m_TimeOfContactStop;

    private float m_TimeOfContactStart;

    private HingeJoint2D m_HingeJoint2D;
    
    private Rigidbody2D m_Rigidbody2D;

    private HairManager m_HairManager;

    private bool m_IsFacingRight = true;

    public int Id { get => m_id; set => m_id = value; }

    public Rigidbody2D Rigidbody2D => m_Rigidbody2D;

    public bool IsFacingRight
    {
        get => m_IsFacingRight;
        set
        {
            m_IsFacingRight = value;

            JointMotor2D jointMotor2D = m_HingeJoint2D.motor;

            jointMotor2D.motorSpeed = value == true ?  200 : -200;

            m_HingeJoint2D.motor = jointMotor2D;
        }
    }

    public bool ActivatedWrapFromOther
    {
        get => m_ActivatedWrapFromOther;
        set => m_ActivatedWrapFromOther = value;
    }

    void Start()
    {
        m_HingeJoint2D = transform.GetComponent<HingeJoint2D>();

        m_Rigidbody2D = transform.GetComponent<Rigidbody2D>();

        m_HairManager = HairManager.Instance;
    }

    private void Update()
    {
        if (!m_ShouldWrap && m_Rigidbody2D.constraints != RigidbodyConstraints2D.None)
        {
            m_HingeJoint2D.useMotor = true;

            JointMotor2D jointMotor2D = m_HingeJoint2D.motor;

            jointMotor2D.motorSpeed = m_IsFacingRight == false ? 800 : -800;

            m_HingeJoint2D.motor = jointMotor2D;
        }

        if (!m_ShouldWrap)
        {
            //m_HingeJoint2D.useLimits = false;

            m_TimeOfContactStart = 0;

            m_Rigidbody2D.constraints = RigidbodyConstraints2D.None;
            m_ActivatedWrapFromOther = false;

            m_Rigidbody2D.sharedMaterial = m_HairManager.LongHairPhysMat[0];
        }

        if (ActivatedWrapFromOther)
        {
            m_HingeJoint2D.useMotor = true;
            m_HingeJoint2D.useLimits = true;
        }

        if (Time.time - m_TimeOfContactStop > m_TimeToHoldOn && !m_ShouldHoldOn && !ActivatedWrapFromOther)
        {
            m_HingeJoint2D.useMotor = false;
            //m_HingeJoint2D.useLimits = false;
            m_TimeOfContactStart = 0;
            m_Rigidbody2D.constraints = RigidbodyConstraints2D.None;
            m_Rigidbody2D.sharedMaterial = m_HairManager.LongHairPhysMat[0];

            m_HairManager.DeactivateNextPart(m_id);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("HairGrap"))
        {
            return;
        }

        if (m_ShouldWrap == true)
        {
            m_HingeJoint2D.useMotor = true;
            //m_HingeJoint2D.useLimits = true;
            m_ShouldHoldOn = true;
            m_TimeOfContactStart = Time.time;

            m_Rigidbody2D.sharedMaterial = m_HairManager.LongHairPhysMat[1];

            m_HairManager.ActivateNextPart(m_id);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("HairGrap"))
        {
            return;
        }

        if (Time.time - m_TimeOfContactStart > 1.5f)
        {
            m_Rigidbody2D.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("HairGrap"))
        {
            return;
        }

        m_TimeOfContactStop = Time.time;
        m_ShouldHoldOn = false;
    }

    public void SetWrap(bool value)
    {
        m_ShouldWrap = value;
    }
}
