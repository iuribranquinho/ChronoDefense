// GDD §4.3 — Flow Field 128×128 (células de 1 m) com Burst + Jobs.
// Bake no carregamento; Recalculate() só em eventos de mapa (MapChangedEvent).
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ChronoDefense.Systems
{
    public sealed class FlowField : IDisposable
    {
        public const int Size = 128;
        public const int CellCount = Size * Size;
        public const float CellSize = 1f;
        public const byte Walkable = 1;
        public const byte Blocked = 255;
        public const ushort Unreached = ushort.MaxValue;

        public NativeArray<byte> Cost;            // 16 KB
        public NativeArray<ushort> Integration;   // 32 KB
        public NativeArray<float2> Flow;          // 128 KB
        public float2 Origin;
        public float2 CorePos;
        public float2 FinalDir;

        public FlowFieldView View => new FlowFieldView { Integration = Integration, Flow = Flow, Origin = Origin };

        public FlowField()
        {
            Cost = new NativeArray<byte>(CellCount, Allocator.Persistent);
            Integration = new NativeArray<ushort>(CellCount, Allocator.Persistent);
            Flow = new NativeArray<float2>(CellCount, Allocator.Persistent);
        }

        /// <summary>Origem padrão: mapa centrado em (0,0), cobrindo ±64 m.</summary>
        public static float2 DefaultOrigin => new float2(-Size * 0.5f * CellSize);

        /// <summary>Bake completo: custo a partir da polilinha → integração (BFS) → direções.</summary>
        public void Bake(Vector3[] polyline, float pathWidth, float2 origin)
        {
            Origin = origin;
            var points = new NativeArray<float2>(polyline.Length, Allocator.TempJob);
            for (int i = 0; i < polyline.Length; i++) points[i] = new float2(polyline[i].x, polyline[i].z);
            CorePos = points[points.Length - 1];
            FinalDir = math.normalizesafe(CorePos - points[math.max(0, points.Length - 2)], new float2(1, 0));

            new BuildCostJob
            {
                Cost = Cost,
                Points = points,
                HalfWidth = pathWidth * 0.5f + 0.5f * CellSize,   // garante ≥ 2 células de largura
                Origin = origin,
            }.Schedule(CellCount, 256).Complete();
            points.Dispose();

            Recalculate();
        }

        /// <summary>Recalcula integração e direções com o custo atual (≤ 0,6 ms em Medium, GDD §4.3).</summary>
        public void Recalculate()
        {
            var queue = new NativeArray<int>(CellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var integ = new BuildIntegrationJob
            {
                Cost = Cost,
                Integration = Integration,
                Queue = queue,
                Origin = Origin,
                CorePos = CorePos,
                GoalRadius = 1.5f,
            }.Schedule();
            var flow = new BuildFlowJob
            {
                Cost = Cost,
                Integration = Integration,
                Flow = Flow,
                Origin = Origin,
                CorePos = CorePos,
                FinalDir = FinalDir,
            }.Schedule(CellCount, 256, integ);
            flow.Complete();
            queue.Dispose();
        }

        public void Dispose()
        {
            if (Cost.IsCreated) Cost.Dispose();
            if (Integration.IsCreated) Integration.Dispose();
            if (Flow.IsCreated) Flow.Dispose();
        }

        // ------------------------------------------------------------------ helpers estáticos
        public static int2 CellOf(float2 pos, float2 origin) => (int2)math.floor((pos - origin) / CellSize);
        public static bool InBounds(int2 c) => c.x >= 0 && c.y >= 0 && c.x < Size && c.y < Size;
        public static int Index(int2 c) => c.y * Size + c.x;
        public static float2 CellCenter(int2 c, float2 origin) => origin + ((float2)c + 0.5f) * CellSize;

        public static float DistanceToSegment(float2 p, float2 a, float2 b)
        {
            float2 ab = b - a;
            float t = math.saturate(math.dot(p - a, ab) / math.max(math.lengthsq(ab), 1e-6f));
            return math.distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------ jobs
        [BurstCompile(CompileSynchronously = true)]
        struct BuildCostJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<byte> Cost;
            [ReadOnly] public NativeArray<float2> Points;
            public float HalfWidth;
            public float2 Origin;

            public void Execute(int index)
            {
                int2 c = new int2(index % Size, index / Size);
                float2 p = CellCenter(c, Origin);
                float best = float.MaxValue;
                for (int i = 0; i < Points.Length - 1; i++)
                    best = math.min(best, DistanceToSegment(p, Points[i], Points[i + 1]));
                Cost[index] = best <= HalfWidth ? Walkable : Blocked;
            }
        }

        /// <summary>
        /// BFS reverso a partir das células do Núcleo. Com custo uniforme (1) o algoritmo de Dial
        /// do GDD degenera num BFS com uma única fila.
        /// </summary>
        [BurstCompile(CompileSynchronously = true)]
        struct BuildIntegrationJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Cost;
            public NativeArray<ushort> Integration;
            public NativeArray<int> Queue;
            public float2 Origin, CorePos;
            public float GoalRadius;

            public void Execute()
            {
                int head = 0, tail = 0;
                for (int i = 0; i < CellCount; i++)
                {
                    Integration[i] = Unreached;
                    if (Cost[i] == Blocked) continue;
                    int2 c = new int2(i % Size, i / Size);
                    if (math.distance(CellCenter(c, Origin), CorePos) <= GoalRadius)
                    {
                        Integration[i] = 0;
                        Queue[tail++] = i;
                    }
                }

                while (head < tail)
                {
                    int i = Queue[head++];
                    int2 c = new int2(i % Size, i / Size);
                    ushort next = (ushort)(Integration[i] + Cost[i]);
                    Visit(c + new int2(1, 0), next, ref tail);
                    Visit(c + new int2(-1, 0), next, ref tail);
                    Visit(c + new int2(0, 1), next, ref tail);
                    Visit(c + new int2(0, -1), next, ref tail);
                }
            }

            void Visit(int2 n, ushort value, ref int tail)
            {
                if (!InBounds(n)) return;
                int ni = Index(n);
                if (Cost[ni] == Blocked || Integration[ni] <= value) return;
                Integration[ni] = value;
                Queue[tail++] = ni;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct BuildFlowJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> Cost;
            [ReadOnly] public NativeArray<ushort> Integration;
            [WriteOnly] public NativeArray<float2> Flow;
            public float2 Origin, CorePos, FinalDir;

            public void Execute(int index)
            {
                ushort own = Integration[index];
                if (Cost[index] == Blocked || own == Unreached)
                {
                    Flow[index] = float2.zero;
                    return;
                }
                int2 c = new int2(index % Size, index / Size);
                if (own == 0)
                {
                    float2 toCore = CorePos - CellCenter(c, Origin);
                    Flow[index] = math.lengthsq(toCore) > 0.01f ? math.normalize(toCore) : FinalDir;
                    return;
                }

                int bestValue = own;
                float2 bestDir = float2.zero;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int2 n = c + new int2(dx, dy);
                    if (!InBounds(n)) continue;
                    int ni = Index(n);
                    if (Cost[ni] == Blocked) continue;
                    // Diagonal só se as duas ortogonais forem livres (sem cortar quinas).
                    if (dx != 0 && dy != 0)
                    {
                        if (Cost[Index(c + new int2(dx, 0))] == Blocked || Cost[Index(c + new int2(0, dy))] == Blocked) continue;
                    }
                    int v = Integration[ni];
                    if (v < bestValue)
                    {
                        bestValue = v;
                        bestDir = math.normalize(new float2(dx, dy));
                    }
                }
                Flow[index] = bestDir;
            }
        }
    }

    /// <summary>Visão somente leitura do flow field para jobs (copiada por valor).</summary>
    public struct FlowFieldView
    {
        [ReadOnly] public NativeArray<ushort> Integration;
        [ReadOnly] public NativeArray<float2> Flow;
        public float2 Origin;

        /// <summary>Direção com interpolação bilinear (curvas suaves, GDD §4.3).</summary>
        public float2 SampleDirection(float2 pos)
        {
            float2 g = (pos - Origin) / FlowField.CellSize - 0.5f;
            int2 c0 = (int2)math.floor(g);
            float2 f = g - c0;
            float2 sum = float2.zero;
            sum += Cell(c0) * (1 - f.x) * (1 - f.y);
            sum += Cell(c0 + new int2(1, 0)) * f.x * (1 - f.y);
            sum += Cell(c0 + new int2(0, 1)) * (1 - f.x) * f.y;
            sum += Cell(c0 + new int2(1, 1)) * f.x * f.y;
            if (math.lengthsq(sum) > 1e-6f) return math.normalize(sum);
            return Cell(FlowField.CellOf(pos, Origin));
        }

        /// <summary>Distância de integração até o Núcleo (progresso gratuito, GDD §4.3).</summary>
        public float SampleIntegration(float2 pos)
        {
            int2 c = FlowField.CellOf(pos, Origin);
            if (!FlowField.InBounds(c)) return FlowField.Unreached;
            ushort v = Integration[FlowField.Index(c)];
            if (v != FlowField.Unreached) return v;
            // Fora do corredor (ex.: empurrão): usa o vizinho alcançado mais próximo.
            float best = FlowField.Unreached;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int2 n = c + new int2(dx, dy);
                if (!FlowField.InBounds(n)) continue;
                ushort nv = Integration[FlowField.Index(n)];
                if (nv != FlowField.Unreached) best = math.min(best, nv + 1f);
            }
            return best;
        }

        public bool IsWalkable(float2 pos)
        {
            int2 c = FlowField.CellOf(pos, Origin);
            return FlowField.InBounds(c) && Integration[FlowField.Index(c)] != FlowField.Unreached;
        }

        float2 Cell(int2 c) => FlowField.InBounds(c) ? Flow[FlowField.Index(c)] : float2.zero;
    }
}
