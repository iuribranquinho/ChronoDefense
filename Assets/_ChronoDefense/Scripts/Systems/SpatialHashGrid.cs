// GDD §4.4 — grade espacial 64×64 de células de 2 m, reconstruída a cada tick por counting sort
// (contagem → prefixo → preenchimento). Consultas de alcance em O(K).
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ChronoDefense.Systems
{
    public struct SpatialHashGrid : IDisposable
    {
        public const int Size = 64;
        public const int CellCount = Size * Size;
        public const float CellSize = 2f;

        public NativeArray<int> CellStart;
        public NativeArray<int> CellCount_;
        public NativeArray<int> Entities;      // índices de creep ordenados por célula
        public float2 Origin;

        public SpatialHashGrid(int capacity, float2 origin)
        {
            CellStart = new NativeArray<int>(CellCount, Allocator.Persistent);
            CellCount_ = new NativeArray<int>(CellCount, Allocator.Persistent);
            Entities = new NativeArray<int>(capacity, Allocator.Persistent);
            Origin = origin;
        }

        public void Dispose()
        {
            if (CellStart.IsCreated) CellStart.Dispose();
            if (CellCount_.IsCreated) CellCount_.Dispose();
            if (Entities.IsCreated) Entities.Dispose();
        }

        public static int2 CellOf(float2 pos, float2 origin) =>
            math.clamp((int2)math.floor((pos - origin) / CellSize), 0, Size - 1);

        /// <summary>Retângulo de células que envolve o círculo (inclusive).</summary>
        public void GetRange(float2 center, float radius, out int2 min, out int2 max)
        {
            min = CellOf(center - radius, Origin);
            max = CellOf(center + radius, Origin);
        }

        /// <summary>
        /// Preenche <paramref name="results"/> com creeps vivos a até <paramref name="radius"/> de
        /// <paramref name="center"/>. Retorna quantos foram escritos (limitado ao tamanho do buffer).
        /// Burst-compatível, sem alocação.
        /// </summary>
        public int QueryRadius(NativeArray<CreepState> creeps, float2 center, float radius, NativeArray<int> results)
        {
            GetRange(center, radius, out int2 min, out int2 max);
            float r2 = radius * radius;
            int n = 0;
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                int cell = y * Size + x;
                int start = CellStart[cell], end = start + CellCount_[cell];
                for (int k = start; k < end; k++)
                {
                    int e = Entities[k];
                    CreepState c = creeps[e];
                    if ((c.Flags & CreepFlags.Alive) == 0) continue;
                    if (math.distancesq(c.Pos, center) > r2) continue;
                    if (n < results.Length) results[n] = e;
                    n++;
                }
            }
            return math.min(n, results.Length);
        }

        public JobHandle ScheduleBuild(NativeArray<CreepState> creeps, NativeArray<byte> active, NativeArray<int> scratch, JobHandle dep) =>
            new BuildJob
            {
                Creeps = creeps,
                Active = active,
                CellStart = CellStart,
                CellCount = CellCount_,
                Entities = Entities,
                Write = scratch,
                Origin = Origin,
            }.Schedule(dep);

        [BurstCompile(CompileSynchronously = true)]
        struct BuildJob : IJob
        {
            [ReadOnly] public NativeArray<CreepState> Creeps;
            [ReadOnly] public NativeArray<byte> Active;
            public NativeArray<int> CellStart, CellCount, Entities, Write;
            public float2 Origin;

            public void Execute()
            {
                for (int i = 0; i < CellCount.Length; i++) CellCount[i] = 0;

                // 1) contagem
                for (int i = 0; i < Creeps.Length; i++)
                {
                    if (Active[i] == 0 || (Creeps[i].Flags & CreepFlags.Alive) == 0) continue;
                    int2 c = CellOf(Creeps[i].Pos, Origin);
                    CellCount[c.y * Size + c.x]++;
                }
                // 2) prefixo
                int sum = 0;
                for (int i = 0; i < CellCount.Length; i++)
                {
                    CellStart[i] = sum;
                    Write[i] = sum;
                    sum += CellCount[i];
                }
                // 3) preenchimento
                for (int i = 0; i < Creeps.Length; i++)
                {
                    if (Active[i] == 0 || (Creeps[i].Flags & CreepFlags.Alive) == 0) continue;
                    int2 c = CellOf(Creeps[i].Pos, Origin);
                    int cell = c.y * Size + c.x;
                    Entities[Write[cell]++] = i;
                }
            }
        }
    }
}
