using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    // Start is called before the first frame update
    public float m_rndHueColor;
    private void Start()
    {
    }
    // Update is called once per frame
    void Update()
    {
    }

    public void    Explode()
    {
        if (BRG_Debris.gDebrisManager != null)
            BRG_Debris.gDebrisManager.GenerateBurstOfDebris(transform.position, 1024, m_rndHueColor);
    }

    void    OnDestroy()
    {
    }
}
