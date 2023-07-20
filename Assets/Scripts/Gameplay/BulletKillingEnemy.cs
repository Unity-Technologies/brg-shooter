using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletKillingEnemy : MonoBehaviour
{

    private Vector3 m_prevPos;
    private void Awake()
    {
        m_prevPos = transform.position;
    }
    private void Update()
    {
        float dt = Time.deltaTime;
        transform.Translate(0,0,dt*50.0f);
        if ( transform.position.z > 100 )
        {
            Destroy(this.gameObject);
        }
        else
        {
            if (BRG_Background.gBackgroundManager != null)
                BRG_Background.gBackgroundManager.SetMagnetCell(m_prevPos, transform.position);
            m_prevPos = transform.position;
        }
    }

    // Start is called before the first frame update
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Enemy")
        {
            EnemyMovement enemy = other.gameObject.GetComponent<EnemyMovement>();
            if ( enemy != null)
            {
                enemy.Explode();
            }
            Destroy(other.gameObject);
            Destroy(this.gameObject);
        }
    }
}
