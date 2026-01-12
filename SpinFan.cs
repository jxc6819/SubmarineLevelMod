using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class SpinFan : MonoBehaviour
    {
        public float speed;
        public SpinFan(IntPtr ptr) : base(ptr) { }
        public SpinFan() : base(ClassInjector.DerivedConstructorPointer<SpinFan>())
            => ClassInjector.DerivedConstructorBody(this);

        bool isStopping;
        public float startSpeed = 350f;
        public float stopTime = 4f;

        // Start is called before the first frame update
        void OnEnable()
        {
            speed = 350f;
            isStopping = false;
        }

        // Update is called once per frame
        void Update()
        {
            transform.Rotate(0, 0, speed * Time.deltaTime);

            if (isStopping)
            {
                float decelPerSecond = startSpeed / stopTime;
                speed = Mathf.MoveTowards(speed, 0f, decelPerSecond * Time.deltaTime);
            }
        }

        public void stopFan()
        {
            isStopping = true;
        }
    }
}
