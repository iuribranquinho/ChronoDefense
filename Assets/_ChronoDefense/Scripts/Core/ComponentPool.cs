using System;
using UnityEngine;

namespace ChronoDefense.Core
{
    /// <summary>GDD §3.4 — contrato de objetos de pool. Nenhum dos dois métodos pode alocar.</summary>
    public interface IPoolable
    {
        void OnRent();
        void OnReturn();
    }

    /// <summary>Interface não genérica para o ObjectPoolManager guardar pools heterogêneos.</summary>
    public interface IComponentPool
    {
        int Capacity { get; }
        int ActiveCount { get; }
        void ReturnAll();
        void Destroy();
    }

    /// <summary>
    /// GDD §3.4 — GameObjects pré-instanciados e desativados + pilha de índices livres.
    /// Esgotado → recicla o item alugado há mais tempo (floaty text, VFX). Sem alocação após o Prewarm.
    /// </summary>
    public sealed class ComponentPool<T> : IComponentPool where T : Component, IPoolable
    {
        readonly T[] _items;
        readonly int[] _free;
        readonly long[] _rentStamp;    // 0 = livre
        readonly bool _recycleOldest;
        int _freeCount;
        long _stamp;

        public int Capacity => _items.Length;
        public int ActiveCount => _items.Length - _freeCount;

        /// <param name="factory">Cria um item desativado (chamado só aqui, no carregamento).</param>
        public ComponentPool(int capacity, Func<int, T> factory, bool recycleOldest)
        {
            _items = new T[capacity];
            _free = new int[capacity];
            _rentStamp = new long[capacity];
            _recycleOldest = recycleOldest;
            for (int i = 0; i < capacity; i++)
            {
                T item = factory(i);
                item.gameObject.SetActive(false);
                _items[i] = item;
                _free[i] = capacity - 1 - i;
            }
            _freeCount = capacity;
        }

        /// <summary>Retorna null se esgotado e o pool não recicla.</summary>
        public T Rent()
        {
            int index;
            if (_freeCount > 0)
            {
                index = _free[--_freeCount];
            }
            else
            {
                if (!_recycleOldest) return null;
                index = 0;
                long oldest = long.MaxValue;
                for (int i = 0; i < _rentStamp.Length; i++)
                    if (_rentStamp[i] < oldest) { oldest = _rentStamp[i]; index = i; }
                _items[index].OnReturn();
            }
            _rentStamp[index] = ++_stamp;
            T item = _items[index];
            item.gameObject.SetActive(true);
            item.OnRent();
            return item;
        }

        public void Return(T item)
        {
            int index = IndexOf(item);
            if (index < 0 || _rentStamp[index] == 0) return;
            _rentStamp[index] = 0;
            item.OnReturn();
            item.gameObject.SetActive(false);
            _free[_freeCount++] = index;
        }

        public void ReturnAll()
        {
            for (int i = 0; i < _items.Length; i++)
                if (_rentStamp[i] != 0) Return(_items[i]);
        }

        public void Destroy()
        {
            for (int i = 0; i < _items.Length; i++)
                if (_items[i] != null) UnityEngine.Object.Destroy(_items[i].gameObject);
        }

        int IndexOf(T item)
        {
            for (int i = 0; i < _items.Length; i++)
                if (ReferenceEquals(_items[i], item)) return i;
            return -1;
        }
    }
}
