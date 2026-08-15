using UnityEngine;

namespace LastLight.Presentation.Combat
{
    /// <summary>
    /// Gives an actor a slow drift and a breathing glow, so the board is not two static discs.
    /// </summary>
    /// <remarks>
    /// This drives the actor's <b>pivot</b>, never the sprite itself. <see cref="ActorView"/>
    /// already animates the sprite's local position for the hit shake and caches its rest position
    /// on Awake; if both wrote the same transform they would fight, and the shake would slowly walk
    /// the actor off its mark. Splitting them means the two animations compose instead of collide.
    ///
    /// The bob and the glow run on different periods and each actor is given a phase offset, so the
    /// two never fall into lockstep - which is what makes idle motion read as alive rather than
    /// mechanical.
    /// </remarks>
    public sealed class ActorIdleMotion : MonoBehaviour
    {
        [Header("Drift")]
        [SerializeField] private float bobDistance = 0.12f;
        [SerializeField] private float bobPeriod = 3.2f;

        [Header("Glow")]
        [SerializeField] private SpriteRenderer halo;
        [SerializeField] private float haloPeriod = 4.6f;
        [SerializeField] private float haloMinAlpha = 0.30f;
        [SerializeField] private float haloMaxAlpha = 0.55f;
        [SerializeField] private float haloMinScale = 1.9f;
        [SerializeField] private float haloMaxScale = 2.2f;

        [Header("Phase")]
        [SerializeField, Tooltip("Seconds of offset, so two actors are never synchronised.")]
        private float phaseOffset;

        private Vector3 restPosition;
        private Color haloColor;

        private void Awake()
        {
            restPosition = transform.localPosition;
            if (halo != null) haloColor = halo.color;
        }

        private void Update()
        {
            float time = Time.time + phaseOffset;

            transform.localPosition = restPosition +
                new Vector3(0f, Mathf.Sin(time * Mathf.PI * 2f / bobPeriod) * bobDistance, 0f);

            if (halo == null) return;

            // 0..1 rather than -1..1, so the glow swells and settles instead of inverting.
            float pulse = (Mathf.Sin(time * Mathf.PI * 2f / haloPeriod) + 1f) * 0.5f;

            halo.color = new Color(haloColor.r, haloColor.g, haloColor.b,
                Mathf.Lerp(haloMinAlpha, haloMaxAlpha, pulse));

            halo.transform.localScale = Vector3.one * Mathf.Lerp(haloMinScale, haloMaxScale, pulse);
        }

#if UNITY_EDITOR
        public void Bind(SpriteRenderer glow, float offset, float distance, float period)
        {
            halo = glow;
            phaseOffset = offset;
            bobDistance = distance;
            bobPeriod = period;
        }
#endif
    }
}
