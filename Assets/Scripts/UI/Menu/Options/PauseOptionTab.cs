using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NSMB.UI.Pause.Options {
    public class PauseOptionTab : MonoBehaviour {

        //---Serialized Variables
        [Header("Tab Graphic")]
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite deselectedSprite;
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color deselectedTextColor = new Color32(64, 64, 64, 255);
        [SerializeField] private TMP_Text text;
        [SerializeField] internal List<PauseOption> options;

        //---Components
        [SerializeField] private Image image;

        public void OnValidate() {
            if (!image) image = GetComponent<Image>();
        }

        public virtual void Selected() {
            foreach (PauseOption option in options) {
                option.gameObject.SetActive(true);
                option.Deselected();
            }
            text.color = selectedTextColor;
            image.sprite = selectedSprite;
            image.color = selectedTextColor;
        }

        public virtual void Deselected() {
            foreach (PauseOption option in options) {
                option.gameObject.SetActive(false);
            }
            text.color = deselectedTextColor;
            image.sprite = deselectedSprite;
            image.color = deselectedTextColor;
        }

        public void Highlighted() {
            text.color = selectedTextColor;
        }

        public void Unhighlighted() {
            text.color = deselectedTextColor;
        }
    }
}
