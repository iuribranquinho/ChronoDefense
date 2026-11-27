// Troca de poses estáticas do herói (Bíblia §5.0: Idle, Ataque, Habilidade, Movimento, Vitória).
// Cada pose é um filho do prefab (Tools/split_poses.py → Tools ▸ Chrono ▸ Importar modelos 3D); só um fica
// ativo. É a forma mais barata de dar vida ao herói antes de existir rig + animação esquelética.
// Zero-GC: só SetActive e um timer.
using UnityEngine;

namespace ChronoDefense.UI
{
    public enum HeroPose : byte { Idle, Attack, Skill, Move, Victory }

    public sealed class HeroPoseView : MonoBehaviour
    {
        public const float AttackHold = 0.35f;
        public const float SkillHold = 1.2f;
        public const float MoveHold = 0.6f;

        [Tooltip("Um filho por pose, na ordem de HeroPose. Vazio = a pose usa Idle.")]
        public GameObject[] Poses = new GameObject[5];

        HeroPose _current;
        float _hold;   // > 0: volta para Idle quando zerar; 0: fica na pose

        void Awake() => Show(HeroPose.Idle);

        /// <param name="seconds">Tempo na pose antes de voltar para Idle; 0 = permanente (vitória).</param>
        public void Play(HeroPose pose, float seconds)
        {
            if (_current == HeroPose.Victory) return;   // a vitória encerra a fase: nada a sobrescreve
            Show(pose);
            _hold = seconds;
        }

        void Update()
        {
            if (_hold <= 0f) return;
            _hold -= Time.deltaTime;
            if (_hold <= 0f) Show(HeroPose.Idle);
        }

        void Show(HeroPose pose)
        {
            int target = (int)pose < Poses.Length && Poses[(int)pose] != null ? (int)pose : (int)HeroPose.Idle;
            for (int i = 0; i < Poses.Length; i++)
                if (Poses[i] != null) Poses[i].SetActive(i == target);
            _current = pose;
        }
    }
}
