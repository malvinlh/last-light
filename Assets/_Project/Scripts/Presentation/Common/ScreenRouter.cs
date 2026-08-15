using UnityEngine;

namespace LastLight.Presentation.Common
{
    /// <summary>
    /// Shows exactly one screen at a time.
    /// </summary>
    /// <remarks>
    /// The whole run lives in a single scene, so moving between a fight, a draft and a shrine is
    /// a matter of which panel is active rather than which scene is loaded. That keeps the run's
    /// state in one object graph with nothing to serialise across a scene load, and makes the
    /// transitions instant.
    ///
    /// Every switch deactivates all four roots first, so a screen can never be left visible
    /// underneath another one.
    /// </remarks>
    public sealed class ScreenRouter : MonoBehaviour
    {
        [SerializeField] private GameObject combatRoot;
        [SerializeField] private GameObject rewardRoot;
        [SerializeField] private GameObject shrineRoot;
        [SerializeField] private GameObject runResultRoot;

        public void ShowCombat() => Show(combatRoot);

        public void ShowReward() => Show(rewardRoot);

        public void ShowShrine() => Show(shrineRoot);

        public void ShowRunResult() => Show(runResultRoot);

        private void Show(GameObject wanted)
        {
            SetActive(combatRoot, ReferenceEquals(combatRoot, wanted));
            SetActive(rewardRoot, ReferenceEquals(rewardRoot, wanted));
            SetActive(shrineRoot, ReferenceEquals(shrineRoot, wanted));
            SetActive(runResultRoot, ReferenceEquals(runResultRoot, wanted));
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

#if UNITY_EDITOR
        public void Bind(GameObject combat, GameObject reward, GameObject shrine, GameObject runResult)
        {
            combatRoot = combat;
            rewardRoot = reward;
            shrineRoot = shrine;
            runResultRoot = runResult;
        }
#endif
    }
}
