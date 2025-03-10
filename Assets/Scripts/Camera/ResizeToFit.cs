using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ResizeToFit : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private float aspect = 4f / 3f;

    //---Private Variables
    private RectTransform rect;
    private RectTransform parent;

    public void Awake() {
        rect = GetComponent<RectTransform>();
        parent = rect.parent.GetComponent<RectTransform>();
    }

    public void LateUpdate() {
        if (!Settings.Instance.graphicsNdsEnabled)
            return;

        if (Settings.Instance.graphicsNdsForceAspect)
            SizeToParent(aspect);
        else
            SizeToParent(Camera.main.aspect);
    }

    public void SizeToParent(float aspect) {
        float padding = 1;
        float w, h;
        var bounds = new Rect(0, 0, parent.rect.width, parent.rect.height);
        if (Mathf.RoundToInt(rect.eulerAngles.z) % 180 == 90)
              //Invert the bounds if the image is rotated
              bounds.size = new(bounds.height, bounds.width);

        //Size by height first
        h = bounds.height * padding;
        w = h * aspect;
        if (w > bounds.width * padding) { //If it doesn't fit, fallback to width;
            w = bounds.width * padding;
            h = w / aspect;
        }
        rect.sizeDelta = new(w, h);
    }
}
