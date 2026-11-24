using UnityEngine;

namespace KartRush
{
    /// <summary>Materials used by runtime-spawned item objects (assigned by the scene builder).</summary>
    public class ItemAssets : MonoBehaviour
    {
        public static ItemAssets Instance { get; private set; }
        public Material bananaMat, bananaDarkMat, shellMat, shellRingMat;

        void Awake() => Instance = this;
    }
}
