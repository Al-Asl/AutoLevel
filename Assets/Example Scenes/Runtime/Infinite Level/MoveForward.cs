using UnityEngine;

namespace AutoLevel.Examples
{
    public class MoveForward : MonoBehaviour
    {
        public float speed;

        void Update()
        {
            transform.position += Vector3.forward * speed * Time.deltaTime;
        }
    }
}