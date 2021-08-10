using UnityEngine;

namespace DefaultNamespace
{
    public class Spawner : MonoBehaviour
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private int _count = 200;

        private void Awake()
        {
            for (int i = 0; i < _count; ++i)
            {
                Instantiate(_prefab, transform);
            }
        }
    }
}