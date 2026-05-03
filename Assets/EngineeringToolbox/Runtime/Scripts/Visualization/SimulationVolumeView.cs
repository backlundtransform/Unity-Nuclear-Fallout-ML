using System.Collections.Generic;
using UnityEngine;
using EngineeringToolbox.Core;
using CSharpNumerics.Physics.Materials.Engineering;

namespace EngineeringToolbox.Visualization
{
    public enum VolumeMoveAxis
    {
        None,
        X,
        Y,
        Z
    }

    /// <summary>
    /// Builds a simple 3D presentation volume: a framed cube with a thin internal slice
    /// that displays the simulation texture as a cross-section.
    /// </summary>
    public class SimulationVolumeView
    {
        private static readonly Vector3 DefaultVolumePosition = Vector3.zero;
        private static readonly Vector3 DefaultVolumeRotation = new Vector3(-90f, 30f, -100f);
        private const bool DefaultContainerVisible = false;

        private const float VolumeSize = 2.8f;
        private const float EdgeThickness = 0.028f;
        private const float SliceThickness = 0.035f;
        private const float GridLineThickness = 0.0045f;
        private const float AxisLength = 0.8f;
        private const float AxisThickness = 0.028f;
        private const float AxisColliderThickness = 0.12f;
        private const float AxisOffset = 0.12f;

        private GameObject _root;
        private Transform _volumeRoot;
        private Transform _gizmoRoot;
        private Transform _sliceBody;
        private Transform _gridAnchor;
        private Transform _vectorAnchor;
        private Material _frameMaterial;
        private Material _cornerMaterial;
        private Material _accentMaterial;
        private Material _sliceMaterial;
        private Material _gridMaterial;
        private Vector2 _surfaceSize = new Vector2(2.15f, 2.15f);
        private float _baseSliceHeight;
        private readonly List<Renderer> _frameRenderers = new List<Renderer>();
        private readonly List<Renderer> _cornerRenderers = new List<Renderer>();
        private readonly List<Renderer> _accentRenderers = new List<Renderer>();
        private readonly List<LineRenderer> _gridLines = new List<LineRenderer>();
        private readonly Collider[] _axisColliders = new Collider[3];
        private readonly Material[] _axisMaterials = new Material[3];
        private readonly Color[] _axisColors =
        {
            new Color(0.96f, 0.34f, 0.3f),
            new Color(0.34f, 0.88f, 0.48f),
            new Color(0.28f, 0.62f, 0.98f)
        };

        public Transform VectorAnchor => _vectorAnchor;
        public Vector2 SurfaceSize => _surfaceSize;
        public float BoundsRadius => VolumeSize * 1.15f;
        public Vector3 RotationEuler
        {
            get => _volumeRoot != null ? NormalizeEuler(_volumeRoot.localEulerAngles) : Vector3.zero;
            set
            {
                if (_volumeRoot == null || _gizmoRoot == null)
                {
                    return;
                }

                Quaternion rotation = Quaternion.Euler(value);
                _volumeRoot.localRotation = rotation;
                _gizmoRoot.localRotation = rotation;
            }
        }

        public float AxisHandleLength => AxisLength;

        public Vector3 FocusPointWorld => _volumeRoot != null ? _volumeRoot.position : Vector3.zero;

        public void Initialize(Transform parent)
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject("SimulationVolume");
            _root.transform.SetParent(parent, false);

            _volumeRoot = new GameObject("VolumeRoot").transform;
            _volumeRoot.SetParent(_root.transform, false);
            _volumeRoot.localPosition = DefaultVolumePosition;
            _volumeRoot.localRotation = Quaternion.Euler(DefaultVolumeRotation);

            _gizmoRoot = new GameObject("MoveGizmo").transform;
            _gizmoRoot.SetParent(_root.transform, false);
            _gizmoRoot.localPosition = _volumeRoot.localPosition;
            _gizmoRoot.localRotation = _volumeRoot.localRotation;

            CreatePedestal(_root.transform);
            CreateBackdrop(_root.transform);
            CreateFrame(_volumeRoot);
            CreateSlice(_volumeRoot);
            CreateMoveGizmo(_gizmoRoot);
            SetContainerVisible(DefaultContainerVisible);
        }

        public void SetContainerVisible(bool isVisible)
        {
            SetRenderersVisible(_frameRenderers, isVisible);
            SetRenderersVisible(_cornerRenderers, isVisible);
            SetRenderersVisible(_accentRenderers, isVisible);
        }

        public void ConfigureForModule(PhysicsModule module)
        {
            if (_sliceBody == null)
            {
                return;
            }

            bool isOneDimensional = module == PhysicsModule.PipeFlow || module == PhysicsModule.BeamStress;
            _surfaceSize = isOneDimensional ? new Vector2(2.25f, 0.82f) : new Vector2(2.15f, 2.15f);
            _baseSliceHeight = isOneDimensional ? 0.15f : 0.0f;

            _sliceBody.localScale = new Vector3(_surfaceSize.x, _surfaceSize.y, SliceThickness);
            _sliceBody.localPosition = new Vector3(0f, _baseSliceHeight, 0f);
            if (_gridAnchor != null)
            {
                _gridAnchor.localPosition = Vector3.zero;
            }
            _vectorAnchor.localPosition = new Vector3(0f, 0f, -SliceThickness * 0.75f);
        }

        public void ConfigureGrid(PhysicsModule module, int xSegments, int ySegments)
        {
            bool showGrid = module == PhysicsModule.HeatTransfer
                || module == PhysicsModule.Electrostatics
                || module == PhysicsModule.FluidFlow2D
                || module == PhysicsModule.CylinderFlow
                || module == PhysicsModule.Magnetostatics
                || module == PhysicsModule.PlaneStress;
            if (!showGrid || _gridAnchor == null)
            {
                SetGridActive(false, 0);
                return;
            }

            int clampedX = Mathf.Max(1, xSegments);
            int clampedY = Mathf.Max(1, ySegments);
            float left = -_surfaceSize.x * 0.5f;
            float bottom = -_surfaceSize.y * 0.5f;
            float z = -SliceThickness * 0.46f;
            Color color = new Color(0.88f, 0.96f, 1f, 0.24f);
            int lineIndex = 0;

            for (int ix = 0; ix <= clampedX; ix++)
            {
                float x = left + (_surfaceSize.x * ix / clampedX);
                ConfigureGridLine(GetGridLine(lineIndex++), color,
                    new Vector3(x, bottom, z),
                    new Vector3(x, bottom + _surfaceSize.y, z));
            }

            for (int iy = 0; iy <= clampedY; iy++)
            {
                float y = bottom + (_surfaceSize.y * iy / clampedY);
                ConfigureGridLine(GetGridLine(lineIndex++), color,
                    new Vector3(left, y, z),
                    new Vector3(left + _surfaceSize.x, y, z));
            }

            SetGridActive(true, lineIndex);
        }

        public void SetTexture(Texture texture)
        {
            if (_sliceMaterial != null)
            {
                _sliceMaterial.mainTexture = texture;
                _sliceMaterial.color = Color.white;
            }
        }

        public void ApplyMaterialTheme(EngineeringMaterial material)
        {
            if (_frameMaterial == null || _cornerMaterial == null || _accentMaterial == null)
            {
                return;
            }

            MaterialVisualTheme theme = GetTheme(material);
            _frameMaterial.color = theme.BaseColor;
            _frameMaterial.SetFloat("_Metallic", theme.Metallic);
            _frameMaterial.SetFloat("_Glossiness", theme.Smoothness);
            _frameMaterial.SetColor("_EmissionColor", theme.EmissionColor);

            Color cornerColor = Color.Lerp(theme.BaseColor, Color.white, 0.18f);
            Color cornerEmission = Color.Lerp(theme.EmissionColor * 1.8f, cornerColor * 0.9f, 0.35f);
            _cornerMaterial.color = cornerColor;
            _cornerMaterial.SetFloat("_Metallic", Mathf.Min(theme.Metallic + 0.08f, 1f));
            _cornerMaterial.SetFloat("_Glossiness", Mathf.Clamp01(theme.Smoothness));
            _cornerMaterial.SetColor("_EmissionColor", cornerEmission);

            _accentMaterial.color = theme.AccentColor;
            _accentMaterial.SetColor("_Color", theme.AccentColor);

            UpdateRenderers(_frameRenderers, _frameMaterial);
            UpdateRenderers(_cornerRenderers, _cornerMaterial);
            UpdateRenderers(_accentRenderers, _accentMaterial);
        }

        public Color GetMaterialAccentColor(EngineeringMaterial material)
        {
            return GetTheme(material).AccentColor;
        }

        public void ResetOrientation()
        {
            RotationEuler = DefaultVolumeRotation;
        }

        public bool TryPickAxis(Ray ray, out VolumeMoveAxis axis)
        {
            axis = VolumeMoveAxis.None;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _axisColliders.Length; i++)
            {
                Collider collider = _axisColliders[i];
                if (collider != null && collider.Raycast(ray, out RaycastHit hit, 100f) && hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    axis = (VolumeMoveAxis)(i + 1);
                }
            }

            return axis != VolumeMoveAxis.None;
        }

        public Vector3 GetAxisDirection(VolumeMoveAxis axis)
        {
            switch (axis)
            {
                case VolumeMoveAxis.X:
                    return Vector3.right;
                case VolumeMoveAxis.Y:
                    return Vector3.up;
                case VolumeMoveAxis.Z:
                    return Vector3.forward;
                default:
                    return Vector3.zero;
            }
        }

        public Vector3 GetWorldAxisDirection(VolumeMoveAxis axis)
        {
            return _gizmoRoot != null ? _gizmoRoot.TransformDirection(GetAxisDirection(axis)) : GetAxisDirection(axis);
        }

        public Vector2 GetScreenRotationTangent(Camera camera, VolumeMoveAxis axis)
        {
            if (camera == null)
            {
                return Vector2.zero;
            }

            Vector3 axisDirection = GetWorldAxisDirection(axis).normalized;
            Vector3 tangent = Vector3.Cross(axisDirection, camera.transform.forward).normalized;
            if (tangent.sqrMagnitude < 1e-4f)
            {
                tangent = Vector3.Cross(axisDirection, camera.transform.up).normalized;
            }

            if (tangent.sqrMagnitude < 1e-4f)
            {
                tangent = Vector3.Cross(axisDirection, camera.transform.right).normalized;
            }

            Vector3 pivot = GetRotationPivotWorldPosition();
            Vector3 startScreen = camera.WorldToScreenPoint(pivot);
            Vector3 endScreen = camera.WorldToScreenPoint(pivot + tangent * AxisHandleLength);
            return new Vector2(endScreen.x - startScreen.x, endScreen.y - startScreen.y);
        }

        public Vector3 GetRotationPivotWorldPosition()
        {
            return _gizmoRoot != null ? _gizmoRoot.position : Vector3.zero;
        }

        public void SetSelectedAxis(VolumeMoveAxis axis)
        {
            for (int i = 0; i < _axisMaterials.Length; i++)
            {
                Material material = _axisMaterials[i];
                if (material == null)
                {
                    continue;
                }

                Color baseColor = _axisColors[i];
                bool isSelected = axis == (VolumeMoveAxis)(i + 1);
                material.color = isSelected ? Color.Lerp(baseColor, Color.white, 0.2f) : baseColor;
                material.SetColor("_EmissionColor", isSelected ? baseColor * 1.4f : baseColor * 0.45f);
            }
        }

        private void CreatePedestal(Transform parent)
        {
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(parent, false);
            pedestal.transform.localPosition = new Vector3(0f, -1.78f, 0f);
            pedestal.transform.localScale = new Vector3(1.7f, 0.08f, 1.7f);
            ApplyMaterial(pedestal, CreateLitMaterial(new Color(0.12f, 0.16f, 0.22f), new Color(0.05f, 0.08f, 0.14f), 0.05f, 0.75f));
        }

        private void CreateBackdrop(Transform parent)
        {
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Backdrop";
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.localPosition = new Vector3(0f, 0.2f, 1.8f);
            backdrop.transform.localRotation = Quaternion.identity;
            backdrop.transform.localScale = new Vector3(5.6f, 4.4f, 1f);
            ApplyMaterial(backdrop, CreateUnlitColorMaterial(new Color(0.08f, 0.14f, 0.2f, 1f)));
        }

        private void CreateFrame(Transform parent)
        {
            float half = VolumeSize * 0.5f;
            _frameMaterial = CreateLitMaterial(new Color(0.28f, 0.68f, 0.82f), new Color(0.08f, 0.28f, 0.35f), 0.1f, 0.9f);
            _cornerMaterial = CreateLitMaterial(new Color(0.84f, 0.95f, 1.0f), new Color(0.18f, 0.30f, 0.38f), 0.12f, 0.96f);
            _accentMaterial = CreateUnlitColorMaterial(new Color(0.18f, 0.72f, 0.96f, 1f));

            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                CreateEdge(parent, new Vector3(0f, y * half, z * half), new Vector3(VolumeSize, EdgeThickness, EdgeThickness), _frameMaterial);
                CreateAccentEdge(parent, new Vector3(0f, y * half, z * half), new Vector3(VolumeSize + 0.04f, EdgeThickness * 1.8f, EdgeThickness * 1.8f));
            }

            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                CreateEdge(parent, new Vector3(x * half, 0f, z * half), new Vector3(EdgeThickness, VolumeSize, EdgeThickness), _frameMaterial);
                CreateAccentEdge(parent, new Vector3(x * half, 0f, z * half), new Vector3(EdgeThickness * 1.8f, VolumeSize + 0.04f, EdgeThickness * 1.8f));
            }

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                CreateEdge(parent, new Vector3(x * half, y * half, 0f), new Vector3(EdgeThickness, EdgeThickness, VolumeSize), _frameMaterial);
                CreateAccentEdge(parent, new Vector3(x * half, y * half, 0f), new Vector3(EdgeThickness * 1.8f, EdgeThickness * 1.8f, VolumeSize + 0.04f));
            }

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                CreateCornerMarker(parent, new Vector3(x * half, y * half, z * half));
            }
        }

        private void CreateSlice(Transform parent)
        {
            var slice = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slice.name = "SimulationSlice";
            slice.transform.SetParent(parent, false);
            _sliceBody = slice.transform;

            _sliceMaterial = CreateUnlitTextureMaterial();
            ApplyMaterial(slice, _sliceMaterial);

            _gridAnchor = new GameObject("GridAnchor").transform;
            _gridAnchor.SetParent(_sliceBody, false);
            _gridMaterial = CreateGridMaterial();

            _vectorAnchor = new GameObject("VectorAnchor").transform;
            _vectorAnchor.SetParent(_sliceBody, false);

            ConfigureForModule(PhysicsModule.HeatTransfer);
        }

        private void CreateMoveGizmo(Transform parent)
        {
            float half = VolumeSize * 0.5f;
            Vector3 origin = new Vector3(half + AxisOffset, -half + AxisOffset * 1.2f, -half + AxisOffset * 1.2f);

            var pivot = new GameObject("MoveGizmoPivot").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = origin;

            CreateOriginMarker(pivot);
            CreateAxisHandle(pivot, VolumeMoveAxis.X);
            CreateAxisHandle(pivot, VolumeMoveAxis.Y);
            CreateAxisHandle(pivot, VolumeMoveAxis.Z);
            SetSelectedAxis(VolumeMoveAxis.None);
        }

        private void CreateAxisHandle(Transform parent, VolumeMoveAxis axis)
        {
            int index = (int)axis - 1;
            Color color = _axisColors[index];
            Vector3 direction = GetAxisDirection(axis);

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = $"Move{axis}";
            shaft.transform.SetParent(parent, false);
            shaft.transform.localPosition = direction * (AxisLength * 0.5f);
            shaft.transform.localRotation = GetAxisRotation(axis);
            shaft.transform.localScale = new Vector3(AxisThickness, AxisLength * 0.5f, AxisThickness);

            var material = CreateLitMaterial(color, color * 0.45f, 0.05f, 0.85f);
            _axisMaterials[index] = material;
            var renderer = shaft.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            var visibleCollider = shaft.GetComponent<Collider>();
            if (visibleCollider != null)
            {
                Object.Destroy(visibleCollider);
            }

            var hitTarget = new GameObject($"Move{axis}HitTarget");
            hitTarget.transform.SetParent(parent, false);
            hitTarget.transform.localPosition = direction * (AxisLength * 0.5f);
            hitTarget.transform.localRotation = Quaternion.identity;
            var boxCollider = hitTarget.AddComponent<BoxCollider>();
            boxCollider.size = GetAxisColliderSize(axis);
            _axisColliders[index] = boxCollider;

            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = $"Move{axis}Tip";
            tip.transform.SetParent(parent, false);
            tip.transform.localScale = Vector3.one * 0.13f;
            tip.transform.localPosition = direction * AxisLength;
            ApplyMaterial(tip, material);

            CreateAxisLabel(parent, axis, direction, color);
        }

        private void CreateOriginMarker(Transform parent)
        {
            var origin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            origin.name = "MoveOrigin";
            origin.transform.SetParent(parent, false);
            origin.transform.localPosition = Vector3.zero;
            origin.transform.localScale = Vector3.one * 0.14f;
            ApplyMaterial(origin, CreateLitMaterial(new Color(0.14f, 0.18f, 0.22f), new Color(0.1f, 0.14f, 0.18f), 0.02f, 0.6f));
        }

        private void CreateAxisLabel(Transform parent, VolumeMoveAxis axis, Vector3 direction, Color color)
        {
            var label = new GameObject($"Move{axis}Label");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = direction * (AxisLength + 0.13f);
            label.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);

            var textMesh = label.AddComponent<TextMesh>();
            textMesh.text = axis.ToString();
            textMesh.characterSize = 0.12f;
            textMesh.fontSize = 32;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = color;

            var meshRenderer = label.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = CreateUnlitColorMaterial(color);
            }
        }

        private static Quaternion GetAxisRotation(VolumeMoveAxis axis)
        {
            switch (axis)
            {
                case VolumeMoveAxis.X:
                    return Quaternion.Euler(0f, 0f, 90f);
                case VolumeMoveAxis.Y:
                    return Quaternion.identity;
                case VolumeMoveAxis.Z:
                    return Quaternion.Euler(90f, 0f, 0f);
                default:
                    return Quaternion.identity;
            }
        }

        private static Vector3 GetAxisColliderSize(VolumeMoveAxis axis)
        {
            switch (axis)
            {
                case VolumeMoveAxis.X:
                    return new Vector3(AxisLength + 0.1f, AxisColliderThickness, AxisColliderThickness);
                case VolumeMoveAxis.Y:
                    return new Vector3(AxisColliderThickness, AxisLength + 0.1f, AxisColliderThickness);
                case VolumeMoveAxis.Z:
                    return new Vector3(AxisColliderThickness, AxisColliderThickness, AxisLength + 0.1f);
                default:
                    return Vector3.one * AxisColliderThickness;
            }
        }

        private void CreateEdge(Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "FrameEdge";
            edge.transform.SetParent(parent, false);
            edge.transform.localPosition = localPosition;
            edge.transform.localScale = localScale;
            Renderer renderer = ApplyMaterial(edge, material);
            if (renderer != null)
            {
                _frameRenderers.Add(renderer);
            }
        }

        private void CreateCornerMarker(Transform parent, Vector3 localPosition)
        {
            var corner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            corner.name = "FrameCorner";
            corner.transform.SetParent(parent, false);
            corner.transform.localPosition = localPosition;
            corner.transform.localScale = Vector3.one * 0.14f;
            Renderer renderer = ApplyMaterial(corner, _cornerMaterial);
            if (renderer != null)
            {
                _cornerRenderers.Add(renderer);
            }
        }

        private void CreateAccentEdge(Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "FrameAccentEdge";
            edge.transform.SetParent(parent, false);
            edge.transform.localPosition = localPosition;
            edge.transform.localScale = localScale;
            Renderer renderer = ApplyMaterial(edge, _accentMaterial);
            if (renderer != null)
            {
                _accentRenderers.Add(renderer);
            }
        }

        private static Renderer ApplyMaterial(GameObject gameObject, Material material)
        {
            var renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            var collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            return renderer;
        }

        private static Material CreateLitMaterial(Color color, Color emission, float metallic, float smoothness)
        {
            var shader = Shader.Find("Standard");
            var material = new Material(shader);
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            return material;
        }

        private static Material CreateUnlitTextureMaterial()
        {
            var shader = Shader.Find("Unlit/Texture");
            return new Material(shader);
        }

        private static Material CreateUnlitColorMaterial(Color color)
        {
            var shader = Shader.Find("Unlit/Color");
            var material = new Material(shader);
            material.color = color;
            return material;
        }

        private static Material CreateGridMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            var material = new Material(shader);
            material.color = new Color(0.88f, 0.96f, 1f, 0.24f);
            return material;
        }

        private LineRenderer GetGridLine(int index)
        {
            while (_gridLines.Count <= index)
            {
                var lineObject = new GameObject($"SliceGridLine_{_gridLines.Count}");
                lineObject.transform.SetParent(_gridAnchor, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.material = _gridMaterial;
                line.useWorldSpace = false;
                line.alignment = LineAlignment.TransformZ;
                line.textureMode = LineTextureMode.Stretch;
                line.positionCount = 2;
                line.widthMultiplier = GridLineThickness;
                line.numCapVertices = 0;
                line.numCornerVertices = 0;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                _gridLines.Add(line);
            }

            return _gridLines[index];
        }

        private void ConfigureGridLine(LineRenderer line, Color color, Vector3 start, Vector3 end)
        {
            line.gameObject.SetActive(true);
            line.startColor = color;
            line.endColor = color;
            line.positionCount = 2;
            line.widthMultiplier = GridLineThickness;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private void SetGridActive(bool isActive, int activeLineCount)
        {
            for (int i = 0; i < _gridLines.Count; i++)
            {
                if (_gridLines[i] != null)
                {
                    _gridLines[i].gameObject.SetActive(isActive && i < activeLineCount);
                }
            }
        }

        private static MaterialVisualTheme GetTheme(EngineeringMaterial material)
        {
            string name = material.Name ?? string.Empty;
            switch (name.ToLowerInvariant())
            {
                case "steel":
                    return new MaterialVisualTheme(new Color(0.70f, 0.76f, 0.82f), new Color(0.18f, 0.24f, 0.30f), new Color(0.93f, 0.96f, 1.0f), 0.82f, 0.88f);
                case "aluminum":
                    return new MaterialVisualTheme(new Color(0.78f, 0.82f, 0.86f), new Color(0.22f, 0.26f, 0.30f), new Color(0.84f, 0.89f, 0.96f), 0.7f, 0.93f);
                case "copper":
                    return new MaterialVisualTheme(new Color(0.79f, 0.42f, 0.24f), new Color(0.36f, 0.14f, 0.06f), new Color(0.98f, 0.58f, 0.22f), 0.74f, 0.78f);
                case "water":
                    return new MaterialVisualTheme(new Color(0.18f, 0.72f, 0.96f), new Color(0.06f, 0.26f, 0.36f), new Color(0.04f, 0.54f, 1.0f), 0.04f, 0.96f);
                case "air":
                    return new MaterialVisualTheme(new Color(0.72f, 0.90f, 1.00f), new Color(0.12f, 0.24f, 0.30f), new Color(0.70f, 0.95f, 1.0f), 0.01f, 0.85f);
                case "concrete":
                    return new MaterialVisualTheme(new Color(0.56f, 0.58f, 0.60f), new Color(0.14f, 0.15f, 0.16f), new Color(0.76f, 0.78f, 0.80f), 0.02f, 0.38f);
                case "glass":
                    return new MaterialVisualTheme(new Color(0.62f, 0.88f, 0.98f), new Color(0.14f, 0.28f, 0.34f), new Color(0.44f, 0.94f, 1.0f), 0.03f, 0.98f);
                default:
                {
                    float waterBias = Mathf.Clamp01((float)material.ElectricPermittivity / 80f);
                    float metalBias = material.YoungsModulus > 0 ? Mathf.Clamp01((float)(material.YoungsModulus / 220e9)) : 0f;
                    Color baseColor = Color.Lerp(new Color(0.34f, 0.84f, 0.92f), new Color(0.72f, 0.74f, 0.78f), metalBias);
                    baseColor = Color.Lerp(baseColor, new Color(0.18f, 0.72f, 0.96f), waterBias);
                    Color emission = baseColor * (0.28f + waterBias * 0.18f);
                    Color accent = Color.Lerp(new Color(0.10f, 0.58f, 1.0f), new Color(0.96f, 0.96f, 0.98f), metalBias);
                    accent = Color.Lerp(accent, new Color(0.04f, 0.54f, 1.0f), waterBias);
                    float metallic = Mathf.Lerp(0.04f, 0.8f, metalBias);
                    float smoothness = Mathf.Lerp(0.55f, 0.9f, Mathf.Max(metalBias, waterBias));
                    return new MaterialVisualTheme(baseColor, emission, accent, metallic, smoothness);
                }
            }
        }

        private readonly struct MaterialVisualTheme
        {
            public MaterialVisualTheme(Color baseColor, Color emissionColor, Color accentColor, float metallic, float smoothness)
            {
                BaseColor = baseColor;
                EmissionColor = emissionColor;
                AccentColor = accentColor;
                Metallic = metallic;
                Smoothness = smoothness;
            }

            public Color BaseColor { get; }

            public Color EmissionColor { get; }

            public Color AccentColor { get; }

            public float Metallic { get; }

            public float Smoothness { get; }
        }

        private static void UpdateRenderers(List<Renderer> renderers, Material material)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sharedMaterial = material;
                }
            }
        }

        private static void SetRenderersVisible(List<Renderer> renderers, bool isVisible)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = isVisible;
                }
            }
        }

        private static Vector3 NormalizeEuler(Vector3 euler)
        {
            return new Vector3(
                NormalizeAngle(euler.x),
                NormalizeAngle(euler.y),
                NormalizeAngle(euler.z));
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f)
            {
                angle -= 360f;
            }

            return angle;
        }
    }
}