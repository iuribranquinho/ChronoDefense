using System;
using UnityEngine;

namespace ChronoDefense.Core
{
    /// <summary>Uma entrada do manifesto de pools: id inteiro + tamanho (GDD §3.4).</summary>
    [Serializable]
    public struct PoolManifestEntry
    {
        public int PoolId;
        public int Size;
    }

    /// <summary>
    /// GDD §3.4 — manifesto calculado no carregamento. Os tamanhos de creeps vêm da concorrência
    /// máxima simulada × 1,2 (ver CombatSimulation.ComputePeakConcurrency).
    /// </summary>
    public struct PoolManifest
    {
        public int CreepCapacity;
        public int ProjectileCapacity;
        public int FloatyTextCapacity;

        public static int SizeFor(int peakConcurrency) => (int)Math.Ceiling(peakConcurrency * 1.2);
    }

    /// <summary>
    /// GDD §3.4 — dono dos ComponentPools (views). Os StructPools da simulação ficam na
    /// CombatSimulation, que é quem os lê nos jobs.
    /// Ids de pool são inteiros atribuídos no Prewarm; nada é identificado por string em runtime.
    /// </summary>
    public sealed class ObjectPoolManager : MonoBehaviour
    {
        const int MaxPools = 32;
        readonly IComponentPool[] _pools = new IComponentPool[MaxPools];
        int _poolCount;

        public int PoolCount => _poolCount;

        /// <summary>Único ponto que aloca: registra um pool e devolve o seu id.</summary>
        public int Register<T>(int capacity, Func<int, T> factory, bool recycleOldest) where T : Component, IPoolable
        {
            if (_poolCount >= MaxPools) throw new InvalidOperationException("ObjectPoolManager: limite de pools atingido.");
            _pools[_poolCount] = new ComponentPool<T>(capacity, factory, recycleOldest);
            return _poolCount++;
        }

        public T Rent<T>(int poolId) where T : Component, IPoolable =>
            ((ComponentPool<T>)_pools[poolId]).Rent();

        public void Return<T>(int poolId, T item) where T : Component, IPoolable =>
            ((ComponentPool<T>)_pools[poolId]).Return(item);

        public ComponentPool<T> Get<T>(int poolId) where T : Component, IPoolable =>
            (ComponentPool<T>)_pools[poolId];

        /// <summary>Fim de partida: devolve tudo (não destrói).</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < _poolCount; i++) _pools[i].ReturnAll();
        }

        void OnDestroy()
        {
            for (int i = 0; i < _poolCount; i++) _pools[i].Destroy();
            _poolCount = 0;
        }
    }
}
