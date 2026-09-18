using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RoyalVault.Game.Visual3D
{
    /// <summary>
    /// A jewelry tray as real geometry: a gold frame, a velvet panel inset into it, and recessed
    /// slots so an empty tray still reads as "this holds four things".
    ///
    /// The camera sits on the -Z side looking toward +Z, so smaller Z is nearer the player.
    /// Layers are stacked toward the camera — frame, then velvet, then pieces — which is what
    /// gives the board its sense of depth.
    /// </summary>
    public sealed class Tray3D : MonoBehaviour
    {
        public int Index { get; private set; }
        public float SlotSize { get; private set; }
        public int Capacity { get; private set; }

        private MeshRenderer _frame;
        private readonly List<GameObject> _pieces = new List<GameObject>();
        private Transform _pieceRoot;
        private Coroutine _shake;
        private Vector3 _restPosition;

        public IList<GameObject> Pieces { get { return _pieces; } }
        public Transform PieceRoot { get { return _pieceRoot; } }

        private const float FrameDepth = 0.30f;
        private const float PieceZ = -0.34f;

        public static Tray3D Create(Transform parent, int index, int capacity, float slotSize)
        {
            GameObject root = new GameObject("Tray3D_" + index);
            root.transform.SetParent(parent, false);

            Tray3D tray = root.AddComponent<Tray3D>();
            tray.Build(index, capacity, slotSize);
            return tray;
        }

        private void Build(int index, int capacity, float slotSize)
        {
            Index = index;
            Capacity = capacity;
            SlotSize = slotSize;

            float width = slotSize * 1.40f;
            float height = slotSize * capacity + slotSize * 0.36f;

            // Gold frame — the outermost layer.
            GameObject frame = Piece("Frame", JewelryMeshFactory.Slab(new Vector3(width, height, FrameDepth)),
                                     JewelryMaterialFactory.TrayFrame(), new Vector3(0f, 0f, 0f));
            _frame = frame.GetComponent<MeshRenderer>();

            // Velvet panel, inset and standing slightly proud of the frame.
            Piece("Velvet", JewelryMeshFactory.Slab(new Vector3(width - 0.10f, height - 0.10f, 0.12f)),
                  JewelryMaterialFactory.Velvet(), new Vector3(0f, 0f, -0.13f));

            // Recessed slots.
            for (int i = 0; i < capacity; i++)
            {
                Piece("Slot" + i,
                      JewelryMeshFactory.Slab(new Vector3(slotSize * 0.80f, slotSize * 0.80f, 0.04f)),
                      JewelryMaterialFactory.VaultFloor(),
                      new Vector3(0f, SlotY(i), -0.17f));
            }

            // One generous collider for the whole tray — thumbs are not precise.
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, FrameDepth + 0.9f);
            collider.center = new Vector3(0f, 0f, -0.3f);

            _pieceRoot = new GameObject("Pieces").transform;
            _pieceRoot.SetParent(transform, false);

            _restPosition = Vector3.zero;
        }

        private GameObject Piece(string name, Mesh mesh, Material material, Vector3 localPosition)
        {
            GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;

            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            return go;
        }

        /// <summary>Slot 0 is the bottom of the tray, matching the simulation's stack order.</summary>
        public float SlotY(int slotIndex)
        {
            // Must match the height used in Build, or pieces sit off-centre in their slots.
            float height = SlotSize * Capacity + SlotSize * 0.36f;
            float bottom = -height * 0.5f + SlotSize * 0.5f + SlotSize * 0.18f;
            return bottom + slotIndex * SlotSize;
        }

        public Vector3 SlotLocalPosition(int slotIndex)
        {
            return new Vector3(0f, SlotY(slotIndex), PieceZ);
        }

        public Vector3 SlotWorldPosition(int slotIndex)
        {
            return transform.TransformPoint(SlotLocalPosition(slotIndex));
        }

        /// <summary>Where a lifted piece hovers — above the tray and pulled toward the player.</summary>
        public Vector3 LiftedWorldPosition()
        {
            float height = SlotSize * Capacity + SlotSize * 0.36f;
            return transform.TransformPoint(new Vector3(0f, height * 0.5f + SlotSize * 0.55f, PieceZ - 0.55f));
        }

        public void AddPiece(GameObject piece, int slotIndex, float pieceScale)
        {
            _pieces.Add(piece);
            piece.transform.SetParent(_pieceRoot, false);
            piece.transform.localPosition = SlotLocalPosition(slotIndex);
            piece.transform.localRotation = JewelryPieceBuilder.PresentationRotation;
            piece.transform.localScale = Vector3.one * pieceScale;

            // The sway animates around wherever the piece came to rest, so it has to be told
            // the new angle — otherwise a moved piece drifts back toward its old orientation.
            JewelryPieceVisual visual = piece.GetComponent<JewelryPieceVisual>();
            if (visual != null) visual.SetRestRotation(JewelryPieceBuilder.PresentationRotation);

            ApplyFrameState();
        }

        public GameObject RemoveTop()
        {
            if (_pieces.Count == 0) return null;
            GameObject piece = _pieces[_pieces.Count - 1];
            _pieces.RemoveAt(_pieces.Count - 1);
            ApplyFrameState();
            return piece;
        }

        public GameObject Top
        {
            get { return _pieces.Count == 0 ? null : _pieces[_pieces.Count - 1]; }
        }

        private bool _isSealed;
        private bool _isHighlighted;

        public void SetSealed(bool isSealed)
        {
            _isSealed = isSealed;
            ApplyFrameState();
        }

        public void SetHighlighted(bool highlighted)
        {
            _isHighlighted = highlighted;
            ApplyFrameState();
        }

        /// <summary>
        /// One place decides how the frame looks, from all of its state at once. Previously the
        /// sealed and highlighted setters each wrote the frame directly and clobbered each other,
        /// so a tray could lose its gold seal simply by being tapped.
        /// </summary>
        private void ApplyFrameState()
        {
            if (_isSealed) _frame.sharedMaterial = JewelryMaterialFactory.TrayFrameSealed();
            else if (_isHighlighted) _frame.sharedMaterial = JewelryMaterialFactory.TrayFrameHighlighted();
            else if (_pieces.Count == 0) _frame.sharedMaterial = JewelryMaterialFactory.TrayFrameEmpty();
            else _frame.sharedMaterial = JewelryMaterialFactory.TrayFrame();
        }

        /// <summary>Re-evaluates the frame after pieces are added or removed.</summary>
        public void RefreshFrame()
        {
            ApplyFrameState();
        }

        /// <summary>A short, controlled shake. A rejection should inform, not punish.</summary>
        public IEnumerator Shake()
        {
            if (_shake != null) StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeRoutine());
            yield return _shake;
        }

        private IEnumerator ShakeRoutine()
        {
            const float duration = 0.22f;
            float elapsed = 0f;
            float amplitude = SlotSize * 0.09f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float offset = Mathf.Sin(t * Mathf.PI * 6f) * amplitude * (1f - t);
                transform.localPosition = _restPosition + new Vector3(offset, 0f, 0f);
                yield return null;
            }

            transform.localPosition = _restPosition;
            _shake = null;
        }

        public void SetRestPosition(Vector3 position)
        {
            _restPosition = position;
            transform.localPosition = position;
        }

        /// <summary>
        /// Royal Match: the frame flares to bright gold and each stone flashes in sequence.
        /// Kept under half a second so it never delays the next move.
        /// </summary>
        public IEnumerator PlaySealSequence()
        {
            SetSealed(true);

            for (int i = 0; i < _pieces.Count; i++)
            {
                StartCoroutine(PopPiece(_pieces[i].transform));
                yield return new WaitForSeconds(0.05f);
            }

            yield return new WaitForSeconds(0.14f);
        }

        private IEnumerator PopPiece(Transform piece)
        {
            Vector3 baseScale = piece.localScale;
            Vector3 basePosition = piece.localPosition;
            const float duration = 0.30f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);

                piece.localScale = baseScale * (1f + pulse * 0.16f);
                piece.localPosition = basePosition + new Vector3(0f, 0f, -pulse * 0.16f);
                piece.localRotation = JewelryPieceBuilder.PresentationRotation *
                                      Quaternion.Euler(0f, pulse * 26f, 0f);
                yield return null;
            }

            piece.localScale = baseScale;
            piece.localPosition = basePosition;
            piece.localRotation = JewelryPieceBuilder.PresentationRotation;
        }
    }
}
