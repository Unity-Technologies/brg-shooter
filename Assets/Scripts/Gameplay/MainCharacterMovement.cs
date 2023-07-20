using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class MainCharacterMovement: MonoBehaviour
{
    public float m_autoShootPeriod=1.5f;
    public float m_sinAmplitude = 8.0f;
    public float m_sinSpeed = 1.0f;
    
    public GameObject bulletPrefab;
    public GameObject shootingPoint;


    private float m_autoShootTimer;
    private Vector3 m_initPos;
    private float m_sinPhase;
    private Material m_material;
    private Color m_originalColor;
    private Camera m_mainCamera;
    private Vector3 m_originalCameraPos;

    private const float kInvincibleDuration = 3.0f;
    private float m_invicibleTimer;


    private const float kShakeDuration = 1.0f;
    private float m_shakePhase;
    private float m_shakeTimer;

    private void Awake()
    {
        m_initPos = transform.position;
    }

    void Start()
    {
        //Fetch the Material from the Renderer of the GameObject
        Renderer[] rdr = GetComponentsInChildren<Renderer>();
        if (rdr[0] != null )
        {
            m_material = rdr[0].material;
            if ( m_material != null )
                m_originalColor = m_material.color;
        }
        m_mainCamera = Camera.main;
        m_originalCameraPos = m_mainCamera.transform.position;
        m_invicibleTimer = 0.0f;
    }


    void Update()
    {

        float dt = Time.deltaTime;

        Vector3 newPos = m_initPos;
        newPos.x += m_sinAmplitude * math.sin(m_sinPhase);
        m_sinPhase += dt * m_sinSpeed;
        transform.position = newPos;

        float derivative = math.cos(m_sinPhase);

        transform.rotation = Quaternion.AngleAxis(-derivative * 25.0f, Vector3.forward);

        if ( m_invicibleTimer > 0.0f )
        {
            m_invicibleTimer -= dt;
            if (m_invicibleTimer < 0.0f)
                m_invicibleTimer = 0.0f;

            Color updatedColor = Color.Lerp(m_originalColor, Color.red, m_invicibleTimer / kInvincibleDuration);
            if ( m_material != null )
            {
                m_material.color = updatedColor;
            }
        }

        if (m_shakeTimer > 0.0f)
        {
            m_shakeTimer -= dt;
            if (m_shakeTimer < 0.0f)
                m_shakeTimer = 0.0f;

            m_shakePhase += dt * 10.0f;
            float shakeAmplitude = (m_shakeTimer / kShakeDuration) * 0.5f;
            m_mainCamera.transform.position = m_originalCameraPos + new Vector3(shakeAmplitude * math.sin(m_shakePhase), shakeAmplitude * math.sin(m_shakePhase * 1.37f), shakeAmplitude * math.sin(m_shakePhase * 2.17f));
        }


        /*
                float xMov = Input.GetAxisRaw("Horizontal");
                float yMov = Input.GetAxisRaw("Vertical");
        */

        bool fire = Input.GetKeyDown(KeyCode.Space);

        if ( Input.touchCount == 1 )
        {
            if (Input.touches[0].phase == TouchPhase.Began)
                fire = true;
        }

        if (m_autoShootPeriod > 0.0f)
        {
            m_autoShootTimer += dt;
            if (m_autoShootTimer >= m_autoShootPeriod)
            {
                fire = true;
                m_autoShootTimer = 0.0f;
            }
        }


        if (fire)
        {
            var bulletInstance=   Instantiate(bulletPrefab,shootingPoint.transform.position,shootingPoint.transform.rotation);
        }
    }

    // if enemy collides the player, explode!
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Enemy")
        {
            EnemyMovement enemy = other.gameObject.GetComponent<EnemyMovement>();
            if (enemy != null)
            {
                if ( m_shakeTimer <= 0.0f )
                    m_shakeTimer = kShakeDuration;

                m_invicibleTimer = kInvincibleDuration;

                enemy.Explode();
            }
            Destroy(other.gameObject);
        }
    }

}
