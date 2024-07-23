using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;

public class HairManager : MonoBehaviour
{
    public Action<bool> Wrap;

    public Action<bool, float> HairIsStretching;

    [SerializeField]
    private PlayerController m_Player;

    [SerializeField]
    private GameObject m_LongHair;

    [SerializeField]
    private PhysicsMaterial2D[] m_LongHairPhysMat;

    [SerializeField]
    private GameObject[] m_HairAnchors;

    [SerializeField]
    private Vector3 m_HairPosition;

    [SerializeField]
    private float m_HairSpeed;

    [SerializeField]
    private float m_HairSpawnRate;

    [SerializeField]
    private float m_HairAppliedForce;

    [SerializeField]
    private float m_PullingHairForce = 30f;

    [SerializeField]
    private int m_PullingHairDelay = 100;

    [SerializeField]
    private int m_ShootDelay;

    private Transform[] m_HairSegments;

    private List<HairPart> m_HairParts;

    private bool m_ShouldWrap = false;

    private float m_LastTimeStretchTest;

    private bool m_IsStretched = false;

    private bool m_HasShotHair = false;

    private bool m_IsPullringHair = false;

    private static HairManager _instance;

    public static HairManager Instance { get { return _instance; } }

    public PhysicsMaterial2D[] LongHairPhysMat => m_LongHairPhysMat;

    public bool HasShotHair => m_HasShotHair;

    public bool IsPullringHair => m_IsPullringHair;

    public Vector3 HairPos { get; set; }

    public bool ShouldWrap
    {
        get => m_ShouldWrap;
        set
        {
            DelayWrap(value);
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }

        m_HairSegments = new Transform[m_LongHair.transform.childCount - 1];
        m_HairParts = new ();

        Transform longHairTransform = m_LongHair.transform;

        int index = 0;

        for (int i = 0; i < m_LongHair.transform.childCount - 1; i++)
        {
            m_HairSegments[i] = longHairTransform.GetChild(i);

            if (m_HairSegments[i].TryGetComponent<HairPart>(out HairPart part))
            {
                part.Id = index;
                m_HairParts.Add(part);
                Wrap += m_HairParts[index].SetWrap;

                ++index;
            }
        }
    }

    void Update()
    {
        CheckForStretch();
    }

    private void FixedUpdate()
    {
        ApplyTailPhysics();
    }

    public async Task ShootHair()
    {
        await Task.Delay(m_ShootDelay);

        for (int i = 1; i < m_HairAnchors[1].transform.parent.childCount; i++)
        {
            Transform currentPart = m_HairAnchors[1].transform.parent.GetChild(i);

            currentPart.localScale = Vector3.zero;
        }

        float mass = 2;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;

        Transform lastPart = null;

        Vector3 mouseDirection = (mousePos - m_Player.transform.position);

        for (int i = 0; i < m_HairSegments.Length; i++)
        {
            Transform currentPartTransform = m_HairSegments[i];

            currentPartTransform.localScale = new Vector3(2.77f, 2.77f, 2.77f);

            currentPartTransform.SetPositionAndRotation(m_Player.transform.position, Quaternion.Euler(0, 0, Quaternion.Angle(currentPartTransform.rotation, Quaternion.FromToRotation(currentPartTransform.right, mouseDirection.normalized))));

            if (i< m_HairParts.Count)
            {
                m_HairParts[i].IsFacingRight = m_Player.IsFacingRight;
            }

            Rigidbody2D currentPartRigidBody = currentPartTransform.GetComponent<Rigidbody2D>();

            currentPartRigidBody.mass = mass;

            mass += 0.5f;

            if (lastPart && i < m_HairSegments.Length - 5)
            {
                lastPart.GetComponent<Rigidbody2D>().velocity = mouseDirection.normalized * m_HairAppliedForce;
            }

            lastPart = currentPartTransform;
        }

        ShouldWrap = true;

        await Task.Delay(50);

        m_HasShotHair = true;
    }

    public async Task RetractHair()
    {
        m_IsPullringHair = true;

        for (int i = 1; i < m_HairAnchors[1].transform.parent.childCount; i++)
        {
            m_HairAnchors[1].transform.parent.GetChild(i).position = new Vector3(m_Player.transform.position.x + i, m_Player.transform.position.y, m_Player.transform.position.z);
        }

        ShouldWrap = false;

        for (int i = m_HairSegments.Length - 1; i >= 0; i--)
        {
            Transform currentPartTransform = m_HairSegments[i];

            HairPos = currentPartTransform.position;

            Rigidbody2D currentPartRigidBody = currentPartTransform.GetComponent<Rigidbody2D>();
            
            currentPartRigidBody.velocity = m_PullingHairForce * (new Vector3(m_Player.transform.position.x, m_Player.transform.position.y) - currentPartTransform.position);

            await Task.Delay(m_PullingHairDelay);

            currentPartTransform.localScale = Vector3.zero;
        }

        for (int i = 1; i < m_HairAnchors[1].transform.parent.childCount; i++)
        {
            m_HairAnchors[1].transform.parent.GetChild(i).localScale = Vector3.one;
        }

        m_HasShotHair = false;
        m_IsPullringHair = false;
    }

    private void ApplyTailPhysics()
    {
        GameObject currentAnchor = m_HasShotHair ? m_HairAnchors[0] : m_HairAnchors[1];

        Vector2 directionToEndPos = m_Player.transform.position + m_HairPosition - currentAnchor.transform.position;
        currentAnchor.GetComponent<Rigidbody2D>().velocity = (directionToEndPos * m_HairSpeed) - directionToEndPos;
    }

    private void CheckForStretch()
    {
        if (!m_HasShotHair)
        {
            return;
        }

        if (Time.time - m_LastTimeStretchTest < 0.2f)
        {
            return;
        }

        bool isWraped = false;

        for (int i = 0; i < m_HairSegments.Length - 1; i++)
        {
            if (i < m_HairParts.Count && m_HairParts[i].Rigidbody2D.constraints != RigidbodyConstraints2D.None)
            {
                isWraped = true;
                continue;
            }

            if (!isWraped)
            {
                break;
            }

            Transform segment = m_HairSegments[i];

            Vector3 vec = (segment.position - m_HairSegments[i + 1].position);

            float vecLenght = vec.magnitude;

            if (vecLenght > 0.25f)
            {
                if (m_ShouldWrap && !m_IsStretched)
                {
                    HairIsStretching?.Invoke(true, vecLenght);
                    //m_Rigidbody2D.velocity = 30 * Time.deltaTime * vec.normalized;

                    m_IsStretched = true;

                    break;
                }
            }
            else
            {
                if (!m_ShouldWrap || m_IsStretched)
                {
                    HairIsStretching?.Invoke(false, 0);
                    m_IsStretched = false;
                }
            }
        }

        m_LastTimeStretchTest = Time.time;
    }

    public void ActivateNextPart(int partId)
    {
        for (int i = partId - 1; i > partId - 4; i--)
        {
            if (i < 0)
            {
                break;
            }

            m_HairParts[i].ActivatedWrapFromOther = true;
        }
    }

    public void DeactivateNextPart(int partId)
    {
        for (int i = partId - 1; i > partId - 4; i--)
        {
            if (i < 0)
            {
                break;
            }

            m_HairParts[i].ActivatedWrapFromOther = false;
        }
    }

    private async void DelayWrap(bool value)
    {
        if (value)
        {
            await Task.Delay(300);
        }

        m_ShouldWrap = value;
        Wrap?.Invoke(value);
    }
}
