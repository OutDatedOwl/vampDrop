using Unity.Entities;
using UnityEngine;
using TMPro;

namespace Vampire.UI
{
    /// <summary>
    /// Displays rice collected count.
    /// OPTIMISED: Text is only rebuilt when the count actually changes,
    /// eliminating a string allocation every frame.
    /// </summary>
    public class RiceCounterUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI counterText;

        private EntityManager _em;
        private EntityQuery   _playerQuery;
        private int           _lastRiceCount = -1; // -1 forces first-frame update

        private void Start()
        {
            _em          = World.DefaultGameObjectInjectionWorld.EntityManager;
            _playerQuery = _em.CreateEntityQuery(typeof(Player.PlayerData));
        }

        private void Update()
        {
            if (_playerQuery.IsEmpty || counterText == null) return;

            if (_playerQuery.CalculateEntityCount() != 1) return;

            var data = _em.GetComponentData<Player.PlayerData>(_playerQuery.GetSingletonEntity());

            // Only rebuild the string when the value actually changed
            if (data.RiceCollected == _lastRiceCount) return;

            _lastRiceCount   = data.RiceCollected;
            counterText.text = $"Rice: {_lastRiceCount}";
        }

        private void OnDestroy()
        {
            if (_playerQuery != default)
                _playerQuery.Dispose();
        }
    }
}
