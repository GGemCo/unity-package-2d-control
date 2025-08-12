using System;
using UnityEngine;

namespace GGemCo2DControl
{
    public class ControlManager : MonoBehaviour
    {
        private void Awake()
        {
            gameObject.AddComponent<BootstrapperControl>();
        }
    }
}