using System;
using Unity.Collections;

namespace ChronoDefense.Core
{
    /// <summary>
    /// GDD §3.4 — slots num NativeArray + free list. Usado por creeps e projéteis.
    /// Todos os campos são NativeArrays (inclusive o contador da free list), então a struct pode ser
    /// copiada para dentro de jobs Burst e Rent/Return funcionam lá sem alocação.
    /// Não é thread-safe: Rent/Return só em jobs single-thread (IJob) ou na main thread.
    /// </summary>
    public struct StructPool<T> : IDisposable where T : unmanaged
    {
        public NativeArray<T> Items;
        /// <summary>1 = slot alugado. Iterar 0..Capacity e filtrar por Active.</summary>
        public NativeArray<byte> Active;
        NativeArray<int> _free;
        NativeArray<int> _state;   // [0] = freeCount, [1] = activeCount

        public int Capacity => Items.Length;
        public int ActiveCount => _state[1];
        public bool IsCreated => Items.IsCreated;

        public StructPool(int capacity, Allocator allocator = Allocator.Persistent)
        {
            Items = new NativeArray<T>(capacity, allocator);
            Active = new NativeArray<byte>(capacity, allocator);
            _free = new NativeArray<int>(capacity, allocator, NativeArrayOptions.UninitializedMemory);
            _state = new NativeArray<int>(2, allocator);
            // Pilha em ordem inversa: o primeiro Rent devolve o índice 0 (determinismo).
            for (int i = 0; i < capacity; i++) _free[i] = capacity - 1 - i;
            _state[0] = capacity;
        }

        /// <summary>Retorna o índice do slot ou -1 se esgotado.</summary>
        public int Rent()
        {
            int count = _state[0];
            if (count == 0) return -1;
            count--;
            _state[0] = count;
            int index = _free[count];
            Active[index] = 1;
            _state[1] = _state[1] + 1;
            Items[index] = default;
            return index;
        }

        public void Return(int index)
        {
            if (Active[index] == 0) return;   // retorno duplo é ignorado
            Active[index] = 0;
            int count = _state[0];
            _free[count] = index;
            _state[0] = count + 1;
            _state[1] = _state[1] - 1;
        }

        public void Clear()
        {
            int capacity = Items.Length;
            for (int i = 0; i < capacity; i++)
            {
                Active[i] = 0;
                _free[i] = capacity - 1 - i;
            }
            _state[0] = capacity;
            _state[1] = 0;
        }

        public void Dispose()
        {
            if (Items.IsCreated) Items.Dispose();
            if (Active.IsCreated) Active.Dispose();
            if (_free.IsCreated) _free.Dispose();
            if (_state.IsCreated) _state.Dispose();
        }
    }
}
