// GDD §11.1 — canal simulação → apresentação: ring buffer pré-alocado de structs, sem alocação.
using Unity.Mathematics;

namespace ChronoDefense.Systems
{
    public enum GameEventType : byte
    {
        Damage, Kill, Leak, TowerBuilt, TowerUpgraded, TowerSold, PedestalDisabled,
        SkillCast, WaveStarted, BossSpawned, BossPhase, BossDefeated, HeroRescued, MapChanged,
        // Extensões do MVP:
        WaveCleared, HeroSummoned, PedestalTelegraph, Victory, Defeat,
    }

    public struct GameEvent
    {
        public GameEventType Type;
        public int A, B;            // ids (torre, pedestal, inimigo, skill…)
        public float3 Position;
        public float Value;         // dano, ouro…
    }

    /// <summary>Escrito no Commit (main thread), consumido pela apresentação no mesmo frame.</summary>
    public sealed class GameEventBuffer
    {
        readonly GameEvent[] _items;
        int _head, _count;

        public GameEventBuffer(int capacity) { _items = new GameEvent[capacity]; }

        public int Count => _count;

        /// <summary>Cheio → sobrescreve o mais antigo.</summary>
        public void Push(GameEventType type, int a, int b, float2 pos, float value)
        {
            int index = (_head + _count) % _items.Length;
            if (_count == _items.Length) _head = (_head + 1) % _items.Length;
            else _count++;
            ref GameEvent e = ref _items[index];
            e.Type = type;
            e.A = a;
            e.B = b;
            e.Position = new float3(pos.x, 0f, pos.y);
            e.Value = value;
        }

        public ref readonly GameEvent this[int i] => ref _items[(_head + i) % _items.Length];

        public void Clear()
        {
            _head = 0;
            _count = 0;
        }
    }
}
