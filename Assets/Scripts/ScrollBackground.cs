using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.CoinHunt
{
    public class ScrollBackground : MonoBehaviour
    {
        [SerializeField] private RawImage background;
        [SerializeField] private float scrollSpeed = 1f;

        private void Start()
        {
            if (background == null)
            {
                Debug.LogError("No background image assigned!");
                return;
            }
        }
        private void Update()
        {
            ScrollBG();
        }
        private void ScrollBG()
        {
            if (background != null)
            {
                float newOffset = Time.time * scrollSpeed;
                background.uvRect = new Rect(newOffset, newOffset, 1, 1);
            }            
        }
    }
}
