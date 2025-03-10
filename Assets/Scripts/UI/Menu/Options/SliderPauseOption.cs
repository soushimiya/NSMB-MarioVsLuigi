using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Pause.Options {

    public class SliderPauseOption : PauseOption {

        //---Public Variables
        public Slider slider;

        //---Private Variables
        private float holdTime;

        public override void OnValidate() {
            base.OnValidate();
            if (!slider) slider = GetComponentInChildren<Slider>();
        }

        public override void Awake() {
            base.Awake();
            slider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        public override void OnLeftPress() {
            if (slider.wholeNumbers) {
                slider.value--;
                holdTime = -0.33f;
            } else {
                holdTime = 0;
            }
        }

        public override void OnRightPress() {
            if (slider.wholeNumbers) {
                slider.value++;
                holdTime = -0.33f;
            } else {
                holdTime = 0;
            }
        }

        public override void OnLeftHeld() {
            holdTime += Time.deltaTime;

            float range = slider.maxValue - slider.minValue;
            if (slider.wholeNumbers) {

                if (holdTime > 3f / range) {
                    slider.value--;
                    holdTime = 0;
                }
            } else {
                slider.value -= range * 0.5f * Time.deltaTime;
            }
        }

        public override void OnRightHeld() {
            holdTime += Time.deltaTime;

            float range = slider.maxValue - slider.minValue;
            if (slider.wholeNumbers) {

                if (holdTime > 3f / range) {
                    slider.value++;
                    holdTime = 0;
                }
            } else {
                slider.value += range * 0.5f * Time.deltaTime;
            }
        }

        public virtual void OnSliderValueChanged(float newValue) {
            if (loader)
                loader.OnValueChanged(this, newValue);

            Settings.Instance.SaveSettings();
        }
    }
}
