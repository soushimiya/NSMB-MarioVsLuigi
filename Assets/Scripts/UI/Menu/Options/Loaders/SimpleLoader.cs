using System.Reflection;
using UnityEngine;

using NSMB.UI.Pause.Loaders;

namespace NSMB.UI.Pause.Options {
    public abstract class SimpleLoader<O, V> : PauseOptionLoader where O : PauseOption {

        //---Serialized Variables
        [SerializeField] private string fieldName;

        //---Private Variables
        private FieldInfo field;
        private PropertyInfo property;

        public override void LoadOptions(PauseOption option) {
            if (option is not O optionType)
                return;

            if (string.IsNullOrEmpty(fieldName))
                return;

            V value;
            field = Settings.Instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null) {
                property = Settings.Instance.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public);
                if (property == null) {
                    Debug.LogWarning("Could not load setting: Property/field \"" + fieldName + "\" not found in Settings class");
                    return;
                }
                value = (V) property.GetValue(Settings.Instance);
            } else {
                value = (V) field.GetValue(Settings.Instance);
            }

            SetValue(optionType, value);
        }

        public abstract void SetValue(O pauseOption, V value);
        public abstract V GetValue(O pauseOption);

        public override void OnValueChanged(PauseOption option, object newValue) {
            if (option is not O optionType)
                return;

            V value = GetValue(optionType);

            field?.SetValue(Settings.Instance, value);
            property?.SetValue(Settings.Instance, value);
        }
    }
}
