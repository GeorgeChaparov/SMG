using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using UnityEditor.Experimental.GraphView;

using UnityEngine;

public class HairManager : MonoBehaviour
{
    public Action<bool> Wrap;

    public Action<bool> HairIsWrapped;

    [SerializeField]
    private PlayerController m_Player;

    [SerializeField]
    private GameObject m_LongHair;

    [SerializeField]
    private PhysicsMaterial2D[] m_LongHairPhysMat;

    [SerializeField]
    private GameObject[] m_HairAnchors;

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

    private List<ActiveHairPart> m_ActiveHairParts;

    private bool m_ShouldWrap = false;

    private float m_LastTimeWrapTest;

    private bool m_HasShotHair = false;

    private bool m_IsPullringHair = false;

    private bool m_IsHairWrapped = false;

    private static HairManager _instance;

    public static HairManager Instance { get { return _instance; } }

    public PhysicsMaterial2D[] LongHairPhysMat => m_LongHairPhysMat;

    public bool HasShotHair => m_HasShotHair;

    public bool IsPullingHair => m_IsPullringHair;

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
        m_ActiveHairParts = new ();

        Transform longHairTransform = m_LongHair.transform;

        int index = 0;
        float mass = 2;


        for (int i = 0; i < m_LongHair.transform.childCount - 1; i++)
        {
            m_HairSegments[i] = longHairTransform.GetChild(i);

            if (m_HairSegments[i].TryGetComponent<ActiveHairPart>(out ActiveHairPart part))
            {
                part.Id = index;
                part.Mass = mass;
                m_ActiveHairParts.Add(part);
                Wrap += m_ActiveHairParts[index].SetWrap;
            }
            else if (m_HairSegments[i].TryGetComponent<HairPart>(out HairPart segment))
            {
                segment.Id = index;
                segment.Mass = mass;            
            }

            ++index;
            mass += 0.5f;
        }
    }

    void Update()
    {
        CheckForWrap();  
    }

    private void FixedUpdate()
    {
        ApplyTailPhysics();
    }

    public async Task ShootHair()
    {
        await Task.Delay(m_ShootDelay);

        Transform shortHair = m_HairAnchors[1].transform.parent;

        for (int i = 0; i < shortHair.childCount; i++)
        {
            Transform currentPart = shortHair.GetChild(i);

            currentPart.localScale = Vector3.zero;
        }

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;

        Transform lastPart = null;

        Vector3 mouseDirection = (mousePos - m_Player.transform.position);

        for (int i = 0; i < m_HairSegments.Length; i++)
        {
            Transform currentPartTransform = m_HairSegments[i];

            currentPartTransform.localScale = new Vector3(2.77f, 2.77f, 2.77f);

            currentPartTransform.SetPositionAndRotation(m_Player.transform.position, Quaternion.Euler(0, 0, Quaternion.Angle(currentPartTransform.rotation, Quaternion.FromToRotation(currentPartTransform.right, mouseDirection.normalized))));

            if (i< m_ActiveHairParts.Count)
            {
                m_ActiveHairParts[i].IsFacingRight = m_Player.IsFacingRight;
            }

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

        Transform shortHair = m_HairAnchors[1].transform.parent;

        for (int i = 0; i < shortHair.childCount; i++)
        {
            Transform part = shortHair.GetChild(i);

            part.SetPositionAndRotation(m_Player.transform.position, m_Player.transform.rotation);
            part.localScale = Vector3.one;
        }

        m_HasShotHair = false;
        m_IsPullringHair = false;
    }

    private void ApplyTailPhysics()
    {
        if (m_HasShotHair)
        {
            m_HairAnchors[0].GetComponent<FixedJoint2D>().enabled = true;
            m_HairAnchors[0].GetComponent<DistanceJoint2D>().enabled = true;
        }
        else
        {
            m_HairAnchors[0].GetComponent<FixedJoint2D>().enabled = false;
            m_HairAnchors[0].GetComponent<DistanceJoint2D>().enabled = false;
        }
    }

    private void CheckForWrap()
    {
        if (!m_HasShotHair)
        {
            m_LastTimeWrapTest = Time.time;

            HairIsWrapped?.Invoke(false);
            return;
        }

        if (Time.time - m_LastTimeWrapTest < 0.05f)
        {
            return;
        }

        m_IsHairWrapped = false;
        ActiveHairPart lastWrappedPart = null;

        for (int i = 0; i < m_ActiveHairParts.Count - 1; i++)
        {
            if (m_ActiveHairParts[i].Rigidbody2D.constraints == RigidbodyConstraints2D.FreezeAll)
            {
                lastWrappedPart = m_ActiveHairParts[i];

                m_IsHairWrapped = true;
            }
        }

        if (m_IsHairWrapped)
        {
            FreezePartsAfter(lastWrappedPart.Id);
            HairIsWrapped?.Invoke(m_IsHairWrapped);
        }

        m_LastTimeWrapTest = Time.time;
    }

    public void PlayerHoldingPart(Transform part, float playerMass)
    {
        ChangePartsMassAfter(part, playerMass);
        ChangePartsMassBefore(part);
    }

    private void ChangePartsMassAfter(Transform lastPart, float playerMass)
    {
        Transform currentPart = GetNextPart(lastPart.GetComponent<HairPart>().Id);

        if (currentPart == lastPart)
        {
            return;
        }

        playerMass += 20;
        currentPart.GetComponent<Rigidbody2D>().mass = playerMass;
        ChangePartsMassAfter(currentPart, playerMass);
    }

    private void ChangePartsMassBefore(Transform lastPart)
    {
        Transform currentPart = GetPrevPart(lastPart.GetComponent<HairPart>().Id);

        if (currentPart == lastPart)
        {
            return;
        }
        currentPart.GetComponent<Rigidbody2D>().mass = 10;
        ChangePartsMassBefore(currentPart);
    }

    public Transform GetLastHairPart()
    {
        return m_HairSegments[m_HairSegments.Length - 1];
    }

    public Transform GetClosestPart(Vector3 to)
    {
        Transform closest = null;

        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < m_HairSegments.Length; i++)
        {
            if (i < m_ActiveHairParts.Count && m_ActiveHairParts[i].Rigidbody2D.constraints == RigidbodyConstraints2D.FreezeAll)
            {
                continue;
            }

            Transform part = m_HairSegments[i];

            if (part.position.y < to.y - 0.2f)
            {
                break;
            }

            float distance = (part.position - to).magnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = part;
            }
        }

        return closest;
    }

    public GameObject GetAnchor()
    {
        return m_HairAnchors[0];
    }

    public void ResetPartsMass()
    {
        for (int i = 0; i < m_HairSegments.Length; i++)
        {
            Transform part = m_HairSegments[i];
            part.GetComponent<Rigidbody2D>().mass = part.GetComponent<HairPart>().Mass;
        }
    }

    public void FreezePartsAfter(int partId)
    {
        if (partId - 1 < 0)
        {
            return; 
        }

        ActiveHairPart part = m_ActiveHairParts[partId - 1];

        if (part.Rigidbody2D.constraints != RigidbodyConstraints2D.FreezeAll)
        {
            part.Rigidbody2D.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        FreezePartsAfter(part.Id);
    }

    public Transform GetNextPart(int currentPartId)
    {
        if (currentPartId - 1 < 0)
        {
            return m_HairSegments[currentPartId];
        }
        else
        {
            return m_HairSegments[currentPartId - 1];
        }
    }

    public Transform GetPrevPart(int currentPartId)
    {
        if (currentPartId + 1 > m_HairSegments.Length - 1)
        {
            return m_HairSegments[currentPartId];
        }
        else
        {
            return m_HairSegments[currentPartId + 1];
        }
    }

    public void ActivateNextPart(int partId)
    {
        for (int i = partId - 1; i > partId - 4; i--)
        {
            if (i < 0)
            {
                break;
            }

            m_ActiveHairParts[i].ActivatedWrapFromOther = true;
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

            m_ActiveHairParts[i].ActivatedWrapFromOther = false;
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
