using UnityEngine;
using UnityEngine.UI;
using NuclearFalloutML.Core;
using NuclearFalloutML.Visualization;
using NuclearFalloutML.Export;

using CSharpNumerics.Engines.GIS.Scenario;
using CSharpNumerics.Engines.GIS.Analysis;

namespace NuclearFalloutML.UI
{
    /// <summary>
    /// UI Controller for the fallout simulation.
    /// Provides input fields for coordinates, wind, Monte Carlo parameters,
    /// and displays clustering / probability results.
    /// Maps user input to SimulationConfig → CSharpNumerics pipeline.
    /// </summary>
    public class SimulationUIController : MonoBehaviour
    {
        [Header("Manager Reference")]
        [SerializeField] private FalloutSimulationManager _manager;

        [Header("Input Fields")]
        [SerializeField] private InputField _latitudeInput;
        [SerializeField] private InputField _longitudeInput;
        [SerializeField] private InputField _windSpeedInput;
        [SerializeField] private InputField _windDirXInput;
        [SerializeField] private InputField _windDirYInput;
        [SerializeField] private InputField _monteCarloInput;
        [SerializeField] private InputField _emissionRateInput;
        [SerializeField] private InputField _stackHeightInput;
        [SerializeField] private Dropdown _stabilityDropdown;

        [Header("Controls")]
        [SerializeField] private Button _runButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _exportButton;
        [SerializeField] private Dropdown _displayModeDropdown;

        [Header("Progress")]
        [SerializeField] private Slider _progressBar;
        [SerializeField] private Text _statusText;

        [Header("Statistics Panel")]
        [SerializeField] private GameObject _statsPanel;
        [SerializeField] private Text _statsText;
        [SerializeField] private Text _clusterInfoText;

        private void Start()
        {
            SetupDefaults();
            BindEvents();
        }

        private void SetupDefaults()
        {
            if (_manager == null)
                _manager = FindObjectOfType<FalloutSimulationManager>();

            if (_manager == null) return;

            var c = _manager.Config;
            SetText(_latitudeInput, c.SourceLatitude.ToString("F4"));
            SetText(_longitudeInput, c.SourceLongitude.ToString("F4"));
            SetText(_windSpeedInput, c.WindSpeedMs.ToString("F1"));
            SetText(_windDirXInput, c.WindDirectionX.ToString("F2"));
            SetText(_windDirYInput, c.WindDirectionY.ToString("F2"));
            SetText(_monteCarloInput, c.MonteCarloIterations.ToString());
            SetText(_emissionRateInput, c.EmissionRateKgPerS.ToString("F2"));
            SetText(_stackHeightInput, c.StackHeightMeters.ToString("F0"));

            if (_progressBar != null) _progressBar.value = 0;
            if (_statsPanel != null) _statsPanel.SetActive(false);
            if (_cancelButton != null) _cancelButton.interactable = false;
        }

        private void BindEvents()
        {
            if (_runButton != null)
                _runButton.onClick.AddListener(OnRunClicked);
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelClicked);
            if (_exportButton != null)
                _exportButton.onClick.AddListener(OnExportClicked);
            if (_displayModeDropdown != null)
                _displayModeDropdown.onValueChanged.AddListener(OnDisplayModeChanged);

            if (_manager != null)
            {
                _manager.OnSimulationProgress += OnProgress;
                _manager.OnStatusUpdate += OnStatus;
                _manager.OnSimulationComplete += OnComplete;
                _manager.OnClusteringComplete += OnClusteringDone;
            }
        }

        private void OnRunClicked()
        {
            if (_manager == null || _manager.IsRunning) return;

            ApplyInputsToConfig();

            if (_runButton != null) _runButton.interactable = false;
            if (_cancelButton != null) _cancelButton.interactable = true;
            if (_progressBar != null) _progressBar.value = 0;
            if (_statsPanel != null) _statsPanel.SetActive(false);

            _manager.RunSimulation();
        }

        private void OnCancelClicked()
        {
            // Cancellation not currently supported
            if (_runButton != null) _runButton.interactable = true;
            if (_cancelButton != null) _cancelButton.interactable = false;
        }

        private void OnExportClicked()
        {
            if (_manager?.ScenarioResult == null) return;

            string dir = System.IO.Path.Combine(Application.dataPath, "..", "FalloutExport");
            FalloutExporter.SaveAll(_manager.ScenarioResult, dir);
            OnStatus($"Exported to: {dir}");
        }

        private void OnDisplayModeChanged(int index)
        {
            _manager?.SetDisplayMode((CesiumFalloutRenderer.FalloutDisplayMode)index);
        }

        private void ApplyInputsToConfig()
        {
            var c = _manager.Config;

            if (double.TryParse(GetText(_latitudeInput), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double lat))
                c.SourceLatitude = lat;

            if (double.TryParse(GetText(_longitudeInput), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double lon))
                c.SourceLongitude = lon;

            if (float.TryParse(GetText(_windSpeedInput), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out float ws))
                c.WindSpeedMs = Mathf.Clamp(ws, 0.5f, 50f);

            if (float.TryParse(GetText(_windDirXInput), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out float wdx))
                c.WindDirectionX = wdx;

            if (float.TryParse(GetText(_windDirYInput), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out float wdy))
                c.WindDirectionY = wdy;

            if (int.TryParse(GetText(_monteCarloInput), out int mc))
                c.MonteCarloIterations = Mathf.Clamp(mc, 10, 100000);

            if (double.TryParse(GetText(_emissionRateInput), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double er))
                c.EmissionRateKgPerS = er;

            if (float.TryParse(GetText(_stackHeightInput), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out float sh))
                c.StackHeightMeters = Mathf.Clamp(sh, 0f, 2000f);

            if (_stabilityDropdown != null)
                c.Stability = (FalloutStabilityClass)_stabilityDropdown.value;
        }

        private void OnProgress(float progress)
        {
            if (_progressBar != null)
                _progressBar.value = progress;
        }

        private void OnStatus(string status)
        {
            if (_statusText != null)
                _statusText.text = status;
        }

        private void OnComplete()
        {
            if (_runButton != null) _runButton.interactable = true;
            if (_cancelButton != null) _cancelButton.interactable = false;
        }

        private void OnClusteringDone(ClusterAnalysisResult clusterResult)
        {
            if (_statsPanel != null) _statsPanel.SetActive(true);

            if (_clusterInfoText != null)
                _clusterInfoText.text = $"Dominant Cluster: {clusterResult.DominantCluster}";

            if (_statsText != null)
                _statsText.text = "Cluster analysis complete.\nSee export directory for details.";
        }

        private void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.OnSimulationProgress -= OnProgress;
                _manager.OnStatusUpdate -= OnStatus;
                _manager.OnSimulationComplete -= OnComplete;
                _manager.OnClusteringComplete -= OnClusteringDone;
            }
        }

        private static void SetText(InputField field, string value)
        {
            if (field != null) field.text = value;
        }

        private static string GetText(InputField field)
        {
            return field != null ? field.text : "";
        }
    }
}
