using UnityEngine;

namespace BlackFriday
{
    /// <summary>Ana menüdeki 3D karakter önizlemesi.</summary>
    public class CharacterPreview : MonoBehaviour
    {
        public float spinSpeed = 18f;
        CharacterVisual visual;

        void Awake()
        {
            visual = GetComponent<CharacterVisual>();
            if (visual == null) visual = gameObject.AddComponent<CharacterVisual>();
            visual.modelRoot = transform;
        }

        public void Show(int index)
        {
            if (visual == null) Awake();
            visual.SetModel(index);
        }

        void Update()
        {
            transform.Rotate(0, spinSpeed * Time.deltaTime, 0, Space.World);
        }
    }
}
