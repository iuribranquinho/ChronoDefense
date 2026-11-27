using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ChronoDefense.Data
{
    /// <summary>GDD §10.3 — habilidade como dados; os Effects são aplicados em ordem pelo SkillSystem.</summary>
    [CreateAssetMenu(menuName = "Chrono/Skill", fileName = "SKL_00_Skill")]
    public sealed class SkillDataSO : ScriptableObject
    {
        public int Id;
        public float Cooldown;
        public float CastTime;
        public SkillTargeting Targeting;
        public float Radius;
        public float Length;
        public float Duration;
        public SkillEffect[] Effects;
        public AssetReferenceGameObject Vfx;
    }
}
