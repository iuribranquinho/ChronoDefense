using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ChronoDefense.Data
{
    /// <summary>GDD §10.1 — cue de áudio (música da Era, SFX de habilidade).</summary>
    [CreateAssetMenu(menuName = "Chrono/Audio Cue", fileName = "AUD_Cue")]
    public sealed class AudioCueSO : ScriptableObject
    {
        public int Id;
        [Tooltip("Variações sorteadas a cada disparo")]
        public AssetReferenceT<AudioClip>[] Clips;
        [Range(0f, 1f)] public float Volume = 1f;
        [Range(0f, 0.5f)] public float PitchVariance;
        public bool Loop;
        [Tooltip("Instâncias simultâneas máximas")]
        [Range(1, 16)] public int MaxVoices = 4;
    }
}
