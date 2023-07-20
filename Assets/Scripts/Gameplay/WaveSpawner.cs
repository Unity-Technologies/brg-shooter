using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{

    public GameObject m_objToSpawn;
    public int m_waveCount;
    public float m_startTime;
    public float m_waveLinearSpeed;
    public float m_waveCircularSpeed;
    public float m_waveRadius;
    public float m_waveLenInDegree;


    private GameObject[] m_spawnedObjects;
    private Vector3 m_wavePos;
    private float m_clock;
    private float m_angle;
    private int m_instantiatePos;


    // Start is called before the first frame update
    void Start()
    {
        if (m_objToSpawn == null)
        {
            // delete spawner if level designer forget to fill the spawn type :)
            Debug.Log("WARNING: SpawnableObject with empty m_objToSpawn field");
            Destroy(gameObject);
        }
        else
        {
            m_wavePos = transform.position;
            m_spawnedObjects = new GameObject[m_waveCount];
            m_instantiatePos = 0;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1, 1, 0, 0.5f);
        Gizmos.DrawCube(transform.position, new Vector3(2, 2, 2));
    }

    private int UpdateWavePos(float dt)
    {
        float angle = m_angle;
        int count = 0;
        for (int i = 0; i < m_instantiatePos; i++)
        {
            if (m_spawnedObjects[i] != null)
            {
                Vector3 pos = transform.position;
                pos.x = m_wavePos.x + m_waveRadius * Mathf.Cos(angle);
                pos.z = m_wavePos.z + m_waveRadius * Mathf.Sin(angle);
                m_spawnedObjects[i].transform.position = pos;

                if (m_spawnedObjects[i].transform.position.z < 0)
                {
                    Destroy(m_spawnedObjects[i]);
                    m_spawnedObjects[i] = null;
                }
                else
                {
                    count++;
                }

            }
            angle += m_waveLenInDegree * 3.1415926f / (180.0f * (float)m_waveCount);
        }

        m_angle += dt * m_waveCircularSpeed;
        m_wavePos.z -= dt * m_waveLinearSpeed;

        return count;
    }


    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        m_clock += dt;
        if ( m_clock >= m_startTime )
        {
            if (m_instantiatePos < m_waveCount)
            {
                int i = m_instantiatePos;
                m_spawnedObjects[i] = Instantiate(m_objToSpawn);
                float rndHue = UnityEngine.Random.Range(0.0f, 1.0f);
                Renderer r = m_spawnedObjects[i].GetComponent<Renderer>();
                EnemyMovement enemy = m_spawnedObjects[i].GetComponent<EnemyMovement>();
                enemy.m_rndHueColor = rndHue;
                r.material.color = Color.HSVToRGB(rndHue, 0.8f, 1);
                m_instantiatePos++;
            }

            int count = UpdateWavePos(dt);

            // if wave emitter is empty (all dead) AND outside of playground, reset it
            if ((count == 0) && (m_wavePos.z < 0.0f))
            {
                m_clock = 0.0f;
                m_wavePos = transform.position;
                m_instantiatePos = 0;
            }
        }
    }
}
