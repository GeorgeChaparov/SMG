using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HairPart : MonoBehaviour
{
    protected int m_Id;
    protected float m_Mass;

    public int Id { get => m_Id; set => m_Id = value; }
    public float Mass { get => m_Mass; set { m_Mass = value; gameObject.GetComponent<Rigidbody2D>().mass = value; } }
}
