﻿using UnityEngine;

public class ParallaxMover : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private float speed;

    //---Private Variables
    private Vector3 moveBy;

    public void Start() {
        moveBy = new(speed, 0, 0);
        foreach (var mover in GetComponentsInParent<ParallaxMover>()) {
            if (mover.transform != transform) {
                enabled = false;
                break;
            }
        }
    }

    public void Update() {
        transform.position += Time.deltaTime * moveBy;
    }
}
