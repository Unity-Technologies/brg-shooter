using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using System.Threading;
using System.Collections.Generic;

public unsafe class BRG_Debris : MonoBehaviour
{
    public Mesh m_mesh;
    public Material m_material;
    public bool m_castShadows;

    public static BRG_Debris gDebrisManager;

    public const int kMaxDebris = 16 * 1024;
    public const int kDebrisGpuSize = (3+3+1)*16;     // 3float4 for obj2world, 3 float4 for w2obj and 1 float4 for color
    private const int kMaxJustLandedPerFrame = 256;
    private const int kMaxDeadPerFrame = 256;
    private const float kDebrisScale = 1.0f/4.0f;

    private BRG_Container m_brgContainer;

    private const int kDebrisCounter = 0;
    private const int kGpuItemsCounter = 1;
    private const int kJustLandedCounter = 2;
    private const int kJustDeadCounter = 3;
    private const int kTotalCounters = 4;

    private Unity.Mathematics.Random m_rndGen;

    struct GfxItem
    {
        public float3 pos;
        public int groundCell;
        public float3 speed;
        public float3x3 mat;
        public float3 color;
        public float antiZFight;
        public int landedCount;
    };

    struct DebrisSpawnDesc
    {
        public float3 pos;
        public int count;
        public float rndHueColor;
    };

    private NativeArray<GfxItem> m_gfxItems;
    private List<DebrisSpawnDesc> m_debrisExplosions = new List<DebrisSpawnDesc>();

    public NativeArray<int> m_inOutCounters;
    public NativeArray<int> m_justLandedList;
    public NativeArray<int> m_justDeadList;


    public void Awake()
    {
        gDebrisManager = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        m_rndGen = new Unity.Mathematics.Random(0x22112003);

        m_brgContainer = new BRG_Container();
        m_brgContainer.Init(m_mesh, m_material, kMaxDebris, kDebrisGpuSize, m_castShadows);

        m_inOutCounters = new NativeArray<int>(kTotalCounters, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        m_justLandedList = new NativeArray<int>(kMaxJustLandedPerFrame, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        m_justDeadList = new NativeArray<int>(kMaxDeadPerFrame, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        // setup positions & scale of each background elements
        m_gfxItems = new NativeArray<GfxItem>(kMaxDebris, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        
}


    public void GenerateBurstOfDebris(Vector3 pos, int count, float rndHue)
    {
        DebrisSpawnDesc foo;
        foo.count = count;
        foo.pos = pos;
        foo.rndHueColor = rndHue;
        m_debrisExplosions.Add(foo);
    }

    [BurstCompile]
    private struct DebrisGenerationJob : IJob
    {
        public NativeArray<GfxItem> _gfxItems;
        public NativeArray<int> _inOutCounters;
        [DeallocateOnJobCompletion] 
        public NativeArray<DebrisSpawnDesc> _inSpawnInfos;
        public int _spawnCount;
        public Unity.Mathematics.Random _rnd;

        public void Execute()
        {
            for (int s = 0; s < _spawnCount; s++)
            {
                DebrisSpawnDesc info = _inSpawnInfos[s];

                GfxItem item;

                if (_inOutCounters[kDebrisCounter] + info.count > kMaxDebris)
                    break;

                int writePos = _inOutCounters[kDebrisCounter];
                _inOutCounters[kDebrisCounter] += info.count;

                float rndImpulse = _rnd.NextFloat(3.0f, 4.0f);


                for (int i = 0; i < info.count; i++)
                {
                    item.pos = info.pos;

                    Vector4 burstColor = Color.HSVToRGB(info.rndHueColor + _rnd.NextFloat(-0.1f, 0.1f), 1.0f, 1.0f);

                    float rndI = _rnd.NextFloat(0.5f, 1.5f);
                    item.color = new float3(burstColor.x * rndI, burstColor.y * rndI, burstColor.z * rndI);

                    item.mat = math.mul(float3x3.RotateY(_rnd.NextFloat(0.0f, 2.0f * 3.1415926f)), float3x3.Scale(kDebrisScale));

                    Vector3 rndSpeed;
                    rndSpeed.x = _rnd.NextFloat(-1.0f, 1.0f);
                    rndSpeed.y = _rnd.NextFloat(-1.0f, 1.0f);
                    rndSpeed.z = _rnd.NextFloat(-1.0f, 1.0f);
                    rndSpeed.Normalize();
                    rndSpeed += new Vector3(0, 1, 1);   // initial speed a bit up backward
                    item.speed = rndSpeed * rndImpulse * _rnd.NextFloat(1.0f, 2.0f);

                    item.groundCell = -1;
                    item.antiZFight = 0.0f;
                    item.landedCount = 0;

                    _gfxItems[writePos] = item;
                    writePos++;
                }
            }
        }
    }


    [BurstCompile]
    private struct BackgroundDiggerJob : IJob
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<BRG_Background.BackgroundItem> _backgroundItems;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> _deadList;
        [NativeDisableParallelForRestriction]
        public NativeArray<int> _inOutCounters;

        public void Execute()
        {
            // now GPU raw buffer have been updated and contains _inOutCounters[kDebrisCounter]
            // We should backup this exact count for later UploadGpuData
            _inOutCounters[kGpuItemsCounter] = _inOutCounters[kDebrisCounter];

            int justLandedCount = _inOutCounters[kJustLandedCounter];
            if (justLandedCount > kMaxJustLandedPerFrame)     // by design counter could be > kMaxDeadDebrisPerFrame
                justLandedCount = kMaxJustLandedPerFrame;
            for (int i = 0; i < justLandedCount; i++)
            {
                int cellIndex = _deadList[i];
                BRG_Background.BackgroundItem cell = _backgroundItems[cellIndex];
                if (cell.hInitial > 0.2f)
                    cell.hInitial -= 0.4f;
                cell.color *= new float4(0.5f, 0.5f, 0.5f, 1);
                cell.weight++;
                if (cell.flashTime <= 0.0f)
                    cell.flashTime = 1.0f;
                _backgroundItems[cellIndex] = cell;
            }
        }
    }

    [BurstCompile]
    private struct DebrisRecyclingJob : IJob
    {
        public NativeArray<GfxItem> _gfxItems;
        public NativeArray<int> _inOutCounters;
        public NativeArray<int> _deadList;

        public void Execute()
        {
            // recycle dead particles
            int recycled = 0;
            int iEnd = _inOutCounters[kDebrisCounter];
            int count = _inOutCounters[kJustDeadCounter];
            if (count > kMaxDeadPerFrame)
                count = kMaxDeadPerFrame;
            for (int i=0;i<count;i++)
            {
                int iRead = _deadList[i];
                iEnd--;
                if ( iRead < iEnd )
                {
                    _gfxItems[iRead] = _gfxItems[iEnd];
                }
                recycled++;
            }
            _inOutCounters[kDebrisCounter] -= recycled;
        }
    }

    [BurstCompile]
    private struct PhysicsUpdateJob : IJobFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<GfxItem> _gfxItems;
        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<float4> _sysmemBuffer;
        [NativeDisableParallelForRestriction]
        [ReadOnly]
        public NativeArray<BRG_Background.BackgroundItem> _backgroundItems;
        [NativeDisableParallelForRestriction]
        public NativeArray<int> _justLandedList;
        [NativeDisableParallelForRestriction]
        public NativeArray<int> _justDeadList;
        [NativeDisableParallelForRestriction]
        public NativeArray<int> _inOutCounters;

        public int _sliceId;
        public uint _w;
        public uint _h;
        public float _dt;
        public float _smoothScrolling;
        public float _zDisplacement;
        public int _maxInstancePerWindow;
        public int _windowSizeInFloat4;
        public Unity.Mathematics.Random _rnd;

        private void updateGpuSysmemBuffer(int index, in GfxItem item)
        {
            int i;
            int windowId = System.Math.DivRem(index, _maxInstancePerWindow, out i);

            int windowOffsetInFloat4 = windowId * _windowSizeInFloat4;
            Vector3 bpos = item.pos;
            float3x3 rot = item.mat;

            // compute the new current frame matrix
            _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 0)] = new float4(rot.c0.x, rot.c0.y, rot.c0.z, rot.c1.x);
            _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 1)] = new float4(rot.c1.y, rot.c1.z, rot.c2.x, rot.c2.y);
            _sysmemBuffer[(windowOffsetInFloat4 + i * 3 + 2)] = new float4(rot.c2.z, bpos.x, bpos.y, bpos.z);

            // compute the new inverse matrix
            _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 0)] = new float4(rot.c0.x, rot.c1.x, rot.c2.x, rot.c0.y);
            _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 1)] = new float4(rot.c1.y, rot.c2.y, rot.c0.z, rot.c1.z);
            _sysmemBuffer[(windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 1 + i * 3 + 2)] = new float4(rot.c2.z, -bpos.x, -bpos.y, -bpos.z);

            // update colors
            _sysmemBuffer[windowOffsetInFloat4 + _maxInstancePerWindow * 3 * 2 + i] = new float4(item.color, 1);

        }

        public void Execute(int index)
        {

            int* pCounter = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(_inOutCounters);

            float3 acc = new float3(0, -_dt * 16.0f, 0);
            GfxItem item = _gfxItems[(int)(index)];

            if (item.groundCell >= 0)
            {
                item.pos.z -= _zDisplacement;
                BRG_Background.BackgroundItem cell = _backgroundItems[item.groundCell];
                item.pos.y = cell.h + kDebrisScale * 0.5f + item.antiZFight;
                if (( cell.magnetIntensity > 0.5f ) && (cell.magnetIntensity < 1.0f))
                {
                    // cell just popped because of the magnetic field, so it should project the debris
                    item.speed = new float3(_rnd.NextFloat(-2.0f, 2.0f),8.0f, _rnd.NextFloat(-2.0f, 2.0f));
                    item.groundCell = -1;       // not linked to a cell anymore
                }
            }
            else
            {
                item.pos += item.speed * _dt;
                item.pos.z -= _zDisplacement;
                item.speed += acc;
                // find background cell
                uint xc = (uint)(int)(item.pos.x);
                uint zc = (uint)(int)(item.pos.z + 0.5f + _smoothScrolling);
                if ((xc < _w) && (zc < _h))
                {
                    zc = ((uint)_sliceId + zc) % _h;
                    int cellId = (int)(zc * _w + xc);
                    BRG_Background.BackgroundItem cell = _backgroundItems[cellId];
                    if (item.pos.y <= cell.h)
                    {
                        // debris just landed the ground
                        item.antiZFight = (index & 31) * 0.001f; // anti zfight
                        item.pos.y = cell.h + kDebrisScale * 0.5f + item.antiZFight;
                        item.groundCell = cellId;
                        item.landedCount++;

                        if ( item.landedCount == 1 )
                        {
                            // flash floor cell only on first landing
                            item.color *= 0.5f;
                            // add it to the "just landed list"
                            int outIndex = Interlocked.Add(ref pCounter[kJustLandedCounter], 1) - 1;       // return incremented value (after increment)
                            if (outIndex < kMaxJustLandedPerFrame)
                            {
                                _justLandedList[outIndex] = cellId;
                            }
                        }
                    }
                }
            }

            updateGpuSysmemBuffer(index, item);

            // particle is dead
            if ((item.pos.z < 0.0f) || (item.pos.y < -5.0f))
            {
                int outIndex = Interlocked.Add(ref pCounter[kJustDeadCounter], 1) - 1;       // return incremented value (after increment)
                if (outIndex < kMaxDeadPerFrame)
                {
                    _justDeadList[outIndex] = index;
                }
            }

            _gfxItems[index] = item;



        }
    }

    public JobHandle AddPhysicsUpdateJob(NativeArray<BRG_Background.BackgroundItem> backgroundItems, int sliceId, uint w, uint h, float dt, float zDisplacement, float smoothScrollingPos)
    {

        m_inOutCounters[kJustLandedCounter] = 0;
        m_inOutCounters[kJustDeadCounter] = 0;

        JobHandle jobFence = new JobHandle();

        // First, enqueue debris physics job (gravity, collision with ground cells, updating "just landed" or "just die" lists)
        int totalGpuBufferSize;
        int alignedWindowSize;
        NativeArray<float4> sysmemBuffer = m_brgContainer.GetSysmemBuffer(out totalGpuBufferSize, out alignedWindowSize);

        PhysicsUpdateJob myJob = new PhysicsUpdateJob()
        {
            _gfxItems = m_gfxItems,
            _backgroundItems = backgroundItems,
            _sliceId = sliceId,
            _w = w,
            _h = h,
            _dt = dt,
            _smoothScrolling = smoothScrollingPos,
            _zDisplacement = zDisplacement,
            _inOutCounters = m_inOutCounters,
            _justLandedList = m_justLandedList,
            _justDeadList = m_justDeadList,
            _sysmemBuffer = sysmemBuffer,
            _maxInstancePerWindow = alignedWindowSize / kDebrisGpuSize,
            _windowSizeInFloat4 = alignedWindowSize / 16,
            _rnd = m_rndGen
        };

        jobFence = myJob.ScheduleParallel(m_inOutCounters[kDebrisCounter], 256, jobFence); // 256 debris per Execute

        // run digger jobs on few "just landed" debris
        BackgroundDiggerJob diggerJob = new BackgroundDiggerJob()
        {
            _backgroundItems = backgroundItems,
            _deadList = m_justLandedList,
            _inOutCounters = m_inOutCounters
        };
        jobFence = diggerJob.Schedule(jobFence);

        // then enqueue the recycling job on "just dead" debris
        DebrisRecyclingJob recyclingJob = new DebrisRecyclingJob()
        {
            _inOutCounters = m_inOutCounters,
            _deadList = m_justDeadList,
            _gfxItems = m_gfxItems
        };
        jobFence = recyclingJob.Schedule(jobFence);

        // then eventually add freshly new generated debris if any explosion occurred
        int explosionsCount = m_debrisExplosions.Count;
        if (explosionsCount > 0)
        {
            // copy & clear the list of all explosions for this frame
            NativeArray<DebrisSpawnDesc> na = new NativeArray<DebrisSpawnDesc>(explosionsCount, Allocator.TempJob);
            for (int i = 0; i < m_debrisExplosions.Count; i++)
                na[i] = m_debrisExplosions[i];
            m_debrisExplosions.Clear();

            // enqueue the generation job
            DebrisGenerationJob generationJob = new DebrisGenerationJob()
            {
                _gfxItems = m_gfxItems,
                _inOutCounters = m_inOutCounters,
                _inSpawnInfos = na,
                _spawnCount = explosionsCount,
                _rnd = m_rndGen
            };
            jobFence = generationJob.Schedule(jobFence);
        }

        return jobFence;
    }

    public void UploadGpuData()
    {
        m_brgContainer.UploadGpuData(m_inOutCounters[kGpuItemsCounter]);
    }

    private void OnDestroy()
    {
        if ( m_brgContainer != null )
            m_brgContainer.Shutdown();

        m_gfxItems.Dispose();
        m_inOutCounters.Dispose();
        m_justLandedList.Dispose();
        m_justDeadList.Dispose();
    }
}
