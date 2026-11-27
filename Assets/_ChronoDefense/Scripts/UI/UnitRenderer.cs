// GDD §3.5 — creeps e projéteis sem GameObject: Graphics.RenderMeshInstanced a partir dos arrays da
// simulação, 1 draw call por tipo. Interpola entre os dois últimos ticks (render 60 FPS, sim 30 Hz).
// Movimento procedural (26/11; os modelos não têm esqueleto): passo saltitante, balanço lateral e "amasse" a
// cada pisada. O relógio é o da simulação: congela na pausa e acelera junto com o jogo.
using System;
using ChronoDefense.Systems;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChronoDefense.UI
{
    public sealed class UnitRenderer : IDisposable
    {
        // Projéteis por tipo (índice = TowerArchetype; 4 = herói): lança do Caçador, pedra da Catapulta, orbe do
        // Xamã, pedrinha do Guardião, lança em chamas do Krog. Medidas em metros (já no tamanho de jogo).
        static readonly bool[] Oriented = ArtLibrary.ProjectileOriented;
        static readonly float[] Length = ArtLibrary.ProjectileLength;
        static readonly float[] Thickness = ArtLibrary.ProjectileThickness;
        static readonly float[] Arc = { 0.12f, 0.35f, 0.02f, 0.25f, 0.12f };      // altura do arco ÷ distância
        static readonly float[] Spin = { 0f, 420f, 120f, 600f, 0f };             // graus/s (pedras rolando no ar)
        const float ImpactHeight = 0.9f;
        /// <summary>Câmera do jogo olha para +Z (Bíblia §3.2): um modelo parado vira de frente para ela.</summary>
        static readonly Quaternion FacingCamera = Quaternion.Euler(0f, 180f, 0f);

        readonly ArtLibrary _art;
        readonly NativeArray<Matrix4x4>[] _creepMatrices;
        readonly int[] _creepCounts;
        readonly NativeArray<Matrix4x4>[] _projMatrices;
        // Soldados do Guardião: corpo + barra de vida (fundo e preenchimento), 3 draw calls no total.
        NativeArray<Matrix4x4> _soldierMatrices, _barBackMatrices, _barFillMatrices;
        RenderParams _soldierParams, _barBackParams, _barFillParams;
        const float SoldierHeight = 2.6f, SoldierWidth = 1.75f;   // m de jogo (menor que um campeão)
        // Habilidade de fogo: chão em brasa por zona + chamas espalhadas + chamas sobre quem está queimando.
        const int FlamesPerZone = 16;
        NativeArray<Matrix4x4> _fireGroundMatrices, _flameMatrices;
        RenderParams _fireGroundParams, _flameParams;
        readonly int[] _projCounts;
        RenderParams[] _creepParams, _projParams;

        public UnitRenderer(ArtLibrary art, int creepCapacity, int projectileCapacity)
        {
            _art = art;
            int types = art.CreepMaterials.Length;
            _creepMatrices = new NativeArray<Matrix4x4>[types];
            _creepCounts = new int[types];
            _creepParams = new RenderParams[types];
            var bounds = new Bounds(Vector3.zero, new Vector3(256f, 32f, 256f));
            for (int t = 0; t < types; t++)
            {
                _creepMatrices[t] = new NativeArray<Matrix4x4>(creepCapacity, Allocator.Persistent);
                _creepParams[t] = new RenderParams(art.CreepMaterials[t]) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            }
            int soldiers = CombatSimulation.MaxUnits * CombatSimulation.SoldiersPerGuard;
            _soldierMatrices = new NativeArray<Matrix4x4>(soldiers, Allocator.Persistent);
            _barBackMatrices = new NativeArray<Matrix4x4>(soldiers, Allocator.Persistent);
            _barFillMatrices = new NativeArray<Matrix4x4>(soldiers, Allocator.Persistent);
            _soldierParams = new RenderParams(art.SoldierMaterial) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            _barBackParams = new RenderParams(art.BarBackMaterial) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            _barFillParams = new RenderParams(art.BarFillMaterial) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            _fireGroundMatrices = new NativeArray<Matrix4x4>(CombatSimulation.MaxZones, Allocator.Persistent);
            _flameMatrices = new NativeArray<Matrix4x4>(CombatSimulation.MaxZones * FlamesPerZone + creepCapacity, Allocator.Persistent);
            _fireGroundParams = new RenderParams(art.FireGroundMaterial) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            _flameParams = new RenderParams(art.FlameMaterial) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            int pt = art.ProjectileMaterials.Length;
            _projMatrices = new NativeArray<Matrix4x4>[pt];
            _projCounts = new int[pt];
            _projParams = new RenderParams[pt];
            for (int t = 0; t < pt; t++)
            {
                _projMatrices[t] = new NativeArray<Matrix4x4>(projectileCapacity, Allocator.Persistent);
                _projParams[t] = new RenderParams(art.ProjectileMaterials[t]) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            }
        }

        /// <param name="alpha">Fração entre o tick anterior e o atual (0..1).</param>
        public void Render(CombatSimulation sim, Quaternion billboard, float alpha)
        {
            for (int t = 0; t < _creepCounts.Length; t++) _creepCounts[t] = 0;
            for (int t = 0; t < _projCounts.Length; t++) _projCounts[t] = 0;

            float clock = (sim.TickCount + alpha) * CombatSimulation.Dt;
            int flames = 0;
            var creeps = sim.Creeps;
            for (int i = 0; i < creeps.Capacity; i++)
            {
                if (creeps.Active[i] == 0) continue;
                CreepState c = creeps.Items[i];
                if ((c.Flags & CreepFlags.Alive) == 0) continue;
                int type = c.TypeIndex;
                float2 p = math.lerp(c.PrevPos, c.Pos, alpha);
                float s = _art.CreepScale[type] * ArtLibrary.CreepVisualScale;
                if (c.HitFlash > 0f) s *= 1.08f;   // "pulo" curto no impacto (sem novo material)

                // Passada: chefe pesado e lento, bichos pequenos rápidos; parado (atordoado) não anda.
                bool walking = c.StunTimer <= 0f && c.Blocked == 0 && math.lengthsq(c.Dir) > 1e-6f;
                float freq = c.IsBoss != 0 ? 4.5f : 8f + 2f * math.saturate(c.Speed - 1f);
                float phase = (c.Serial * 2.399f) % 6.2832f;
                float step = walking ? math.abs(math.sin(clock * freq + phase)) : 0f;    // 0 = pé no chão, 1 = no ar
                float hop = step * (c.IsBoss != 0 ? 0.03f : 0.07f) * s;
                float roll = walking ? math.sin(clock * freq * 0.5f + phase) * (c.IsBoss != 0 ? 3f : 7f) : 0f;
                float squash = 1f - 0.07f * (1f - step);                                  // amassa ao pisar
                Vector3 scale = new Vector3(s * (2f - squash), s * squash, s * (2f - squash));

                Quaternion rot = billboard * Quaternion.Euler(0f, 0f, roll * 0.6f);
                float y = 0.02f + hop;
                if (_art.CreepIs3D[type])
                {
                    // Modelos normalizados com a frente em +Z (Tools/normalize_glb.py): olham para onde andam.
                    rot = (math.lengthsq(c.Dir) > 1e-6f
                        ? Quaternion.LookRotation(new Vector3(c.Dir.x, 0f, c.Dir.y))
                        : FacingCamera) * Quaternion.Euler(-4f * step, 0f, roll);
                    y = hop;
                }
                NativeArray<Matrix4x4> buf = _creepMatrices[type];
                buf[_creepCounts[type]++] = Matrix4x4.TRS(new Vector3(p.x, y, p.y), rot, scale);

                // Queimando (fogo do Krog ou melhoria de fogo): chama tremulando em cima do corpo.
                if (c.DotTimer > 0f && flames < _flameMatrices.Length)
                {
                    float flick = 0.85f + 0.25f * math.sin(clock * 15f + i * 2.1f);
                    float fs = 0.55f * s;
                    _flameMatrices[flames++] = Matrix4x4.TRS(new Vector3(p.x, y + 0.35f * s, p.y),
                        billboard * Quaternion.Euler(0f, 0f, 8f * math.sin(clock * 9f + i)), new Vector3(fs * 0.8f, fs * flick, 1f));
                }
            }

            Vector3 camForward = billboard * Vector3.forward;
            var projs = sim.Projectiles;
            for (int i = 0; i < projs.Capacity; i++)
            {
                if (projs.Active[i] == 0) continue;
                ProjectileState pr = projs.Items[i];
                int v = pr.Visual < 4 ? pr.Visual : 4;
                float2 p = math.lerp(pr.PrevPos, pr.Pos, alpha);

                // Sai da MÃO (ArtLibrary.HandLocal, virada para o alvo) e chega no alvo: o desvio lateral da mão
                // some ao longo do voo e a altura segue uma parábola entre a mão e o alvo.
                float total = math.max(0.5f, math.distance(pr.Origin, pr.TargetPos));
                float u = math.saturate(math.distance(pr.Origin, p) / total);
                float2 fwd = math.normalizesafe(pr.TargetPos - pr.Origin, new float2(0f, 1f));
                float2 right = new float2(fwd.y, -fwd.x);
                Vector3 hand = ArtLibrary.HandLocal[v];
                float2 handXZ = right * hand.x + fwd * hand.z;
                float launch = hand.y;
                float y = math.lerp(launch, ImpactHeight, u) + 4f * Arc[v] * total * u * (1f - u);
                if (v == 2) y += 0.15f * math.sin(clock * 14f + i);                      // orbe flutua
                float2 xz = p + handXZ * (1f - u);
                Vector3 pos = new Vector3(xz.x, y, xz.y);

                float2 hd = pr.Pos - pr.PrevPos;
                if (math.lengthsq(hd) < 1e-8f) hd = pr.TargetPos - pr.Pos;
                hd = math.normalizesafe(hd, new float2(1f, 0f));
                float slope = 4f * Arc[v] * (1f - 2f * u) + (ImpactHeight - launch) / total;
                Vector3 dir = new Vector3(hd.x, slope, hd.y).normalized;

                NativeArray<Matrix4x4> buf = _projMatrices[v];
                int cap = buf.Length;
                if (_projCounts[v] >= cap) continue;
                if (Oriented[v])
                {
                    // Billboard axial: o eixo X do quad segue a direção do voo e a face olha para a câmera.
                    Vector3 f = camForward - Vector3.Dot(camForward, dir) * dir;
                    if (f.sqrMagnitude < 1e-6f) f = Vector3.up;
                    f.Normalize();
                    Quaternion rot = Quaternion.LookRotation(f, Vector3.Cross(f, dir));
                    buf[_projCounts[v]++] = Matrix4x4.TRS(pos, rot, new Vector3(Length[v], Thickness[v], 1f));
                }
                else
                {
                    Quaternion rot = billboard * Quaternion.Euler(0f, 0f, clock * Spin[v] + i * 37f);
                    buf[_projCounts[v]++] = Matrix4x4.TRS(pos, rot, new Vector3(Length[v], Thickness[v], 1f));
                    if (v == 2)   // rastro do orbe: duas cópias menores para trás
                        for (int k = 1; k <= 2 && _projCounts[v] < cap; k++)
                            buf[_projCounts[v]++] = Matrix4x4.TRS(pos - dir * (0.45f * k), rot, Vector3.one * (Length[v] * (1f - 0.3f * k)));
                }
            }

            int soldierCount = RenderSoldiers(sim, billboard, alpha, clock, out int barCount);
            int grounds = RenderFireZones(sim, billboard, clock, ref flames);

            for (int t = 0; t < _creepCounts.Length; t++)
                if (_creepCounts[t] > 0)
                    Graphics.RenderMeshInstanced(_creepParams[t], _art.CreepMeshes[t], 0, _creepMatrices[t], _creepCounts[t]);
            for (int t = 0; t < _projCounts.Length; t++)
                if (_projCounts[t] > 0)
                    Graphics.RenderMeshInstanced(_projParams[t], _art.QuadCenter, 0, _projMatrices[t], _projCounts[t]);
            if (soldierCount > 0) Graphics.RenderMeshInstanced(_soldierParams, _art.QuadUpright, 0, _soldierMatrices, soldierCount);
            if (grounds > 0) Graphics.RenderMeshInstanced(_fireGroundParams, _art.QuadXZ, 0, _fireGroundMatrices, grounds);
            if (flames > 0) Graphics.RenderMeshInstanced(_flameParams, _art.QuadUpright, 0, _flameMatrices, flames);
            if (barCount > 0)
            {
                Graphics.RenderMeshInstanced(_barBackParams, _art.QuadCenter, 0, _barBackMatrices, barCount);
                Graphics.RenderMeshInstanced(_barFillParams, _art.QuadCenter, 0, _barFillMatrices, barCount);
            }
        }

        /// <summary>
        /// Zonas de fogo (Caça Coordenada de Fogo): o chão pega fogo com um "estouro" ao cair, chamas espalhadas
        /// tremulam durante a duração e tudo apaga nos últimos 0,6 s. Zero-GC.
        /// </summary>
        int RenderFireZones(CombatSimulation sim, Quaternion billboard, float clock, ref int flames)
        {
            int grounds = 0;
            var zones = sim.Zones;
            for (int z = 0; z < zones.Length; z++)
            {
                SkillZone zone = zones[z];
                if (zone.Active == 0) continue;
                float age = math.max(0f, zone.Duration - zone.Timer);
                float grow = math.saturate(age / 0.25f);
                float pop = 1f + 0.25f * math.sin(math.saturate(age / 0.4f) * math.PI);      // estouro ao cair
                float fade = math.saturate(zone.Timer / 0.6f);
                float r = zone.Radius * grow * pop;
                Vector3 c = new Vector3(zone.Center.x, 0.03f, zone.Center.y);
                _fireGroundMatrices[grounds++] = Matrix4x4.TRS(c, Quaternion.Euler(0f, z * 47f, 0f), new Vector3(2f * r, 1f, 2f * r));

                for (int k = 0; k < FlamesPerZone && flames < _flameMatrices.Length; k++)
                {
                    // distribuição "girassol": chamas espalhadas por igual no círculo
                    float ang = k * 2.39996f + z;
                    float dist = zone.Radius * 0.9f * math.sqrt((k + 0.5f) / FlamesPerZone) * grow;
                    Vector3 fp = c + new Vector3(math.cos(ang) * dist, 0f, math.sin(ang) * dist);
                    float flick = 0.8f + 0.3f * math.sin(clock * 12f + k * 1.7f + z);
                    float h = 1.6f * flick * fade * grow;
                    _flameMatrices[flames++] = Matrix4x4.TRS(fp, billboard * Quaternion.Euler(0f, 0f, 7f * math.sin(clock * 8f + k)),
                        new Vector3(1.1f * fade * grow, h, 1f));
                }
            }
            return grounds;
        }

        /// <summary>
        /// Soldados: billboard virado para o inimigo (espelha na tela), passinho ao andar, golpe inclinando para
        /// frente e barra de vida acima da cabeça quando ferido. Zero-GC.
        /// </summary>
        int RenderSoldiers(CombatSimulation sim, Quaternion billboard, float alpha, float clock, out int bars)
        {
            int count = 0;
            bars = 0;
            SoldierState[] soldiers = sim.Soldiers;
            if (soldiers == null) return 0;
            Vector3 camRight = billboard * Vector3.right;
            var items = sim.Creeps.Items;
            for (int i = 0; i < soldiers.Length; i++)
            {
                SoldierState s = soldiers[i];
                if (s.Active == 0 || s.Alive == 0) continue;
                float2 p = math.lerp(s.PrevPos, s.Pos, alpha);
                float2 look = s.Engaged >= 0 ? items[s.Engaged].Pos - s.Pos : s.Pos - s.PrevPos;
                float side = Vector3.Dot(new Vector3(look.x, 0f, look.y), camRight);
                float flip = side < -0.01f ? -1f : 1f;
                bool moving = math.distancesq(s.Pos, s.PrevPos) > 1e-6f;
                float step = moving ? math.abs(math.sin(clock * 12f + i * 1.3f)) : 0f;
                float swing = math.saturate(s.Swing / 0.25f);                        // 1 → 0 no golpe
                Quaternion rot = billboard * Quaternion.Euler(0f, 0f, -flip * 14f * swing);
                Vector3 pos = new Vector3(p.x, 0.02f + step * 0.18f, p.y);
                _soldierMatrices[count++] = Matrix4x4.TRS(pos, rot, new Vector3(SoldierWidth * flip, SoldierHeight * (1f - 0.05f * step), 1f));

                if (s.Hp < s.MaxHp - 0.5f)
                {
                    float frac = math.saturate(s.Hp / math.max(1f, s.MaxHp));
                    Vector3 barPos = pos + Vector3.up * (SoldierHeight + 0.35f);
                    _barBackMatrices[bars] = Matrix4x4.TRS(barPos, billboard, new Vector3(1.3f, 0.2f, 1f));
                    Vector3 fillPos = barPos - camRight * (0.6f * (1f - frac)) - billboard * Vector3.forward * 0.01f;
                    _barFillMatrices[bars] = Matrix4x4.TRS(fillPos, billboard, new Vector3(1.2f * frac, 0.13f, 1f));
                    bars++;
                }
            }
            return count;
        }

        public void Dispose()
        {
            if (_soldierMatrices.IsCreated) _soldierMatrices.Dispose();
            if (_fireGroundMatrices.IsCreated) _fireGroundMatrices.Dispose();
            if (_flameMatrices.IsCreated) _flameMatrices.Dispose();
            if (_barBackMatrices.IsCreated) _barBackMatrices.Dispose();
            if (_barFillMatrices.IsCreated) _barFillMatrices.Dispose();
            for (int t = 0; t < _creepMatrices.Length; t++) if (_creepMatrices[t].IsCreated) _creepMatrices[t].Dispose();
            for (int t = 0; t < _projMatrices.Length; t++) if (_projMatrices[t].IsCreated) _projMatrices[t].Dispose();
        }
    }
}
