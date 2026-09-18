using System.Collections.Generic;
using UnityEngine;

namespace RoyalVault.Game.Visual3D
{
    /// <summary>
    /// Gives a piece of jewelry its life: a slow sway and an independent twinkle on its stones.
    ///
    /// The sway matters more than it sounds. The gems are flat-shaded, so a facet only flares
    /// when it turns through the angle that catches a light — on a perfectly still board every
    /// highlight is frozen and the jewelry reads as a painted icon. A couple of degrees of
    /// motion is enough to keep highlights travelling across the facets.
    ///
    /// The twinkle is driven through a MaterialPropertyBlock rather than by editing the material,
    /// because materials are shared and cached: writing to one would make every ruby on the board
    /// flash in unison. The property block keeps it per-renderer and does not break batching.
    /// </summary>
    public sealed class JewelryPieceVisual : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private MeshRenderer[] _stones;
        private Color[] _baseEmission;
        private float[] _phase;
        private MaterialPropertyBlock _block;

        private Quaternion _restRotation;
        private float _swayPhase;
        private bool _ready;

        private const float SwayDegrees = 3.2f;
        private const float SwaySpeed = 0.7f;

        public void Initialise(List<MeshRenderer> stones)
        {
            _stones = stones.ToArray();
            _baseEmission = new Color[_stones.Length];
            _phase = new float[_stones.Length];
            _block = new MaterialPropertyBlock();

            for (int i = 0; i < _stones.Length; i++)
            {
                Material material = _stones[i].sharedMaterial;
                _baseEmission[i] = material != null && material.HasProperty(EmissionColor)
                    ? material.GetColor(EmissionColor)
                    : Color.black;

                // Stagger the stones so a piece shimmers rather than strobing as one block.
                _phase[i] = Random.Range(0f, Mathf.PI * 2f);
            }

            _swayPhase = Random.Range(0f, Mathf.PI * 2f);
            _ready = true;
        }

        private void OnEnable()
        {
            _restRotation = transform.localRotation;
        }

        /// <summary>Called whenever the piece is placed, so sway returns to the new resting angle.</summary>
        public void SetRestRotation(Quaternion rotation)
        {
            _restRotation = rotation;
        }

        private void Update()
        {
            if (!_ready) return;

            float time = Time.time;

            // Two slightly detuned frequencies stop the motion reading as a mechanical loop.
            float yaw = Mathf.Sin(time * SwaySpeed + _swayPhase) * SwayDegrees;
            float pitch = Mathf.Sin(time * SwaySpeed * 0.63f + _swayPhase * 1.7f) * SwayDegrees * 0.45f;
            transform.localRotation = _restRotation * Quaternion.Euler(pitch, yaw, 0f);

            for (int i = 0; i < _stones.Length; i++)
            {
                if (_stones[i] == null) continue;

                // Mostly dim, with a brief bright flare — a gem catches the light occasionally,
                // it does not pulse like a heartbeat.
                float wave = Mathf.Sin(time * 1.6f + _phase[i]);
                float flare = Mathf.Pow(Mathf.Max(0f, wave), 6f);

                _stones[i].GetPropertyBlock(_block);
                _block.SetColor(EmissionColor, _baseEmission[i] * (1f + flare * 1.9f));
                _stones[i].SetPropertyBlock(_block);
            }
        }
    }
}
