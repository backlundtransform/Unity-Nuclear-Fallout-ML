using EngineeringToolbox.Core;
using EngineeringToolbox.Demo;
using UnityEditor;
using UnityEngine;

namespace EngineeringToolbox.Editor
{
    [CustomEditor(typeof(DemoSimulation))]
    public class DemoSimulationEditor : UnityEditor.Editor
    {
        private SerializedProperty _configProperty;
        private SerializedProperty _autoPlayProperty;
        private SerializedProperty _playIntervalProperty;
        private SerializedProperty _electrostaticVectorSpeedProperty;

        private SerializedProperty _moduleProperty;
        private SerializedProperty _materialPresetProperty;
        private SerializedProperty _widthProperty;
        private SerializedProperty _heightProperty;
        private SerializedProperty _nxProperty;
        private SerializedProperty _nyProperty;
        private SerializedProperty _topBoundaryProperty;
        private SerializedProperty _bottomBoundaryProperty;
        private SerializedProperty _leftBoundaryProperty;
        private SerializedProperty _rightBoundaryProperty;
        private SerializedProperty _dtProperty;
        private SerializedProperty _stepsProperty;
        private SerializedProperty _lengthProperty;
        private SerializedProperty _radiusProperty;
        private SerializedProperty _nodesProperty;
        private SerializedProperty _beamSupportProperty;
        private SerializedProperty _sectionWidthProperty;
        private SerializedProperty _sectionHeightProperty;
        private SerializedProperty _pointLoadValueProperty;
        private SerializedProperty _pointLoadPositionProperty;
        private SerializedProperty _distributedLoadProperty;
        private SerializedProperty _pressureGradientProperty;
        private SerializedProperty _inletVelocityProperty;
        private SerializedProperty _cylinderCenterXProperty;
        private SerializedProperty _cylinderCenterYProperty;
        private SerializedProperty _cylinderRadiusProperty;
        private SerializedProperty _currentDensityProperty;
        private SerializedProperty _uniformLoadProperty;

        private PhysicsDiscipline _settingsDiscipline = PhysicsDiscipline.Thermodynamics;
        private PhysicsModule _settingsModule = PhysicsModule.HeatTransfer;

        private static readonly PhysicsDiscipline[] DisciplineOrder =
        {
            PhysicsDiscipline.Thermodynamics,
            PhysicsDiscipline.SolidMechanics,
            PhysicsDiscipline.Electromagnetism,
            PhysicsDiscipline.FluidDynamics
        };

        private static readonly string[] SettingsTabs =
        {
            "Thermodynamics",
            "Solid Mechanics",
            "Electromagnetism",
            "Fluid Dynamics"
        };

        private void OnEnable()
        {
            _configProperty = serializedObject.FindProperty("config");
            _autoPlayProperty = serializedObject.FindProperty("autoPlay");
            _playIntervalProperty = serializedObject.FindProperty("playInterval");
            _electrostaticVectorSpeedProperty = serializedObject.FindProperty("electrostaticVectorSpeed");

            _moduleProperty = FindConfigProperty("module");
            _materialPresetProperty = FindConfigProperty("materialPreset");
            _widthProperty = FindConfigProperty("width");
            _heightProperty = FindConfigProperty("height");
            _nxProperty = FindConfigProperty("nx");
            _nyProperty = FindConfigProperty("ny");
            _topBoundaryProperty = FindConfigProperty("topBC");
            _bottomBoundaryProperty = FindConfigProperty("bottomBC");
            _leftBoundaryProperty = FindConfigProperty("leftBC");
            _rightBoundaryProperty = FindConfigProperty("rightBC");
            _dtProperty = FindConfigProperty("dt");
            _stepsProperty = FindConfigProperty("steps");
            _lengthProperty = FindConfigProperty("length");
            _radiusProperty = FindConfigProperty("radius");
            _nodesProperty = FindConfigProperty("nodes");
            _beamSupportProperty = FindConfigProperty("beamSupport");
            _sectionWidthProperty = FindConfigProperty("sectionWidth");
            _sectionHeightProperty = FindConfigProperty("sectionHeight");
            _pointLoadValueProperty = FindConfigProperty("pointLoadValue");
            _pointLoadPositionProperty = FindConfigProperty("pointLoadPosition");
            _distributedLoadProperty = FindConfigProperty("distributedLoad");
            _pressureGradientProperty = FindConfigProperty("pressureGradient");
            _inletVelocityProperty = FindConfigProperty("inletVelocity");
            _cylinderCenterXProperty = FindConfigProperty("cylinderCenterX");
            _cylinderCenterYProperty = FindConfigProperty("cylinderCenterY");
            _cylinderRadiusProperty = FindConfigProperty("cylinderRadius");
            _currentDensityProperty = FindConfigProperty("currentDensity");
            _uniformLoadProperty = FindConfigProperty("uniformLoad");

            SyncSettingsViewToActiveModule();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_configProperty == null)
            {
                EditorGUILayout.HelpBox("Simulation config could not be found on this DemoSimulation instance.", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawScriptField();
            DrawDemoSettings();
            EditorGUILayout.Space(8f);
            DrawModuleStatus();
            DrawSettingsFilter();
            EditorGUILayout.Space(4f);
            DrawFilteredConfiguration();

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private SerializedProperty FindConfigProperty(string relativePath)
        {
            return _configProperty.FindPropertyRelative(relativePath);
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                MonoScript script = MonoScript.FromMonoBehaviour((DemoSimulation)target);
                EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
            }
        }

        private void DrawDemoSettings()
        {
            EditorGUILayout.LabelField("Demo Settings", EditorStyles.boldLabel);
            DrawPropertyIfPresent(_autoPlayProperty);
            DrawPropertyIfPresent(_playIntervalProperty);
            DrawPropertyIfPresent(_electrostaticVectorSpeedProperty);
        }

        private void DrawModuleStatus()
        {
            SyncSettingsViewToActiveModule();

            EditorGUILayout.LabelField("Simulation Controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Module and material are controlled in the demo HUD during Play Mode. The selector below only filters which parameter group is shown in the Inspector.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Active Module", PhysicsModuleCatalog.GetModuleLabel(_settingsModule));
                EditorGUILayout.TextField("Active Category", PhysicsModuleCatalog.GetDisciplineLabel(_settingsDiscipline));
                EditorGUILayout.TextField("Active Material", GetEnumDisplayName(_materialPresetProperty));
            }
        }

        private void DrawSettingsFilter()
        {
            EditorGUILayout.LabelField("Settings View", EditorStyles.boldLabel);
            int currentDisciplineIndex = GetDisciplineIndex(_settingsDiscipline);
            int selectedDisciplineIndex = GUILayout.Toolbar(currentDisciplineIndex, SettingsTabs);
            if (selectedDisciplineIndex != currentDisciplineIndex)
            {
                _settingsDiscipline = DisciplineOrder[selectedDisciplineIndex];
                _settingsModule = PhysicsModuleCatalog.GetDefaultModule(_settingsDiscipline);
            }

            PhysicsModule[] modules = PhysicsModuleCatalog.GetModules(_settingsDiscipline);
            string[] moduleLabels = new string[modules.Length];
            for (int index = 0; index < modules.Length; index++)
            {
                moduleLabels[index] = PhysicsModuleCatalog.GetModuleLabel(modules[index]);
            }

            int currentModuleIndex = PhysicsModuleCatalog.GetModuleIndexInDiscipline(_settingsModule);
            int selectedModuleIndex = EditorGUILayout.Popup("Submodel", currentModuleIndex, moduleLabels);
            _settingsModule = modules[selectedModuleIndex];
        }

        private void DrawFilteredConfiguration()
        {
            switch (_settingsModule)
            {
                case PhysicsModule.HeatTransfer:
                    DrawHeatSettings();
                    break;
                case PhysicsModule.BeamStress:
                    DrawBeamSettings();
                    break;
                case PhysicsModule.PlaneStress:
                    DrawPlaneStressSettings();
                    break;
                case PhysicsModule.Electrostatics:
                    DrawElectrostaticSettings();
                    break;
                case PhysicsModule.Magnetostatics:
                    DrawMagnetostaticsSettings();
                    break;
                case PhysicsModule.PipeFlow:
                    DrawPipeFlowSettings();
                    break;
                case PhysicsModule.FluidFlow2D:
                    DrawFluidFlow2DSettings();
                    break;
                case PhysicsModule.CylinderFlow:
                    DrawCylinderFlowSettings();
                    break;
            }
        }

        private void DrawHeatSettings()
        {
            EditorGUILayout.LabelField("Heat Transfer Parameters", EditorStyles.boldLabel);
            DrawGeometry2D();
            DrawBoundaryConditions();
            DrawTimeStepping();
        }

        private void DrawElectrostaticSettings()
        {
            EditorGUILayout.LabelField("Electrostatics Parameters", EditorStyles.boldLabel);
            DrawGeometry2D();
            DrawBoundaryConditions();
        }

        private void DrawPipeFlowSettings()
        {
            EditorGUILayout.LabelField("Pipe Flow Parameters", EditorStyles.boldLabel);
            DrawPropertyIfPresent(_lengthProperty);
            DrawPropertyIfPresent(_radiusProperty);
            DrawPropertyIfPresent(_nodesProperty);
            DrawPropertyIfPresent(_pressureGradientProperty);
            DrawTimeStepping();
        }

        private void DrawBeamSettings()
        {
            EditorGUILayout.LabelField("Beam Parameters", EditorStyles.boldLabel);
            DrawPropertyIfPresent(_lengthProperty);
            DrawPropertyIfPresent(_nodesProperty);
            DrawPropertyIfPresent(_beamSupportProperty);
            DrawPropertyIfPresent(_sectionWidthProperty);
            DrawPropertyIfPresent(_sectionHeightProperty);
            DrawPropertyIfPresent(_pointLoadValueProperty);
            DrawPropertyIfPresent(_pointLoadPositionProperty);
            DrawPropertyIfPresent(_distributedLoadProperty);
        }

        private void DrawPlaneStressSettings()
        {
            EditorGUILayout.LabelField("Plane Stress Parameters", EditorStyles.boldLabel);
            DrawGeometry2D();
            DrawBoundaryConditions();
            DrawPropertyIfPresent(_pointLoadValueProperty);
            DrawPropertyIfPresent(_uniformLoadProperty);
        }

        private void DrawMagnetostaticsSettings()
        {
            EditorGUILayout.LabelField("Magnetostatics Parameters", EditorStyles.boldLabel);
            DrawGeometry2D();
            DrawBoundaryConditions();
            DrawPropertyIfPresent(_currentDensityProperty);
        }

        private void DrawFluidFlow2DSettings()
        {
            EditorGUILayout.LabelField("2D Fluid Flow Parameters", EditorStyles.boldLabel);
            DrawGeometry2D();
            DrawPropertyIfPresent(_inletVelocityProperty);
            DrawTimeStepping();
        }

        private void DrawCylinderFlowSettings()
        {
            EditorGUILayout.LabelField("Cylinder Flow Parameters", EditorStyles.boldLabel);
            DrawGeometry2D();
            DrawPropertyIfPresent(_inletVelocityProperty);
            DrawPropertyIfPresent(_cylinderCenterXProperty);
            DrawPropertyIfPresent(_cylinderCenterYProperty);
            DrawPropertyIfPresent(_cylinderRadiusProperty);
            DrawTimeStepping();
        }

        private void DrawGeometry2D()
        {
            EditorGUILayout.LabelField("Geometry", EditorStyles.miniBoldLabel);
            DrawPropertyIfPresent(_widthProperty);
            DrawPropertyIfPresent(_heightProperty);
            DrawPropertyIfPresent(_nxProperty);
            DrawPropertyIfPresent(_nyProperty);
        }

        private void DrawBoundaryConditions()
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Boundary Conditions", EditorStyles.miniBoldLabel);
            DrawPropertyIfPresent(_topBoundaryProperty);
            DrawPropertyIfPresent(_bottomBoundaryProperty);
            DrawPropertyIfPresent(_leftBoundaryProperty);
            DrawPropertyIfPresent(_rightBoundaryProperty);
        }

        private void DrawTimeStepping()
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Time Stepping", EditorStyles.miniBoldLabel);
            DrawPropertyIfPresent(_dtProperty);
            DrawPropertyIfPresent(_stepsProperty);
        }

        private static void DrawPropertyIfPresent(SerializedProperty property)
        {
            if (property != null)
            {
                EditorGUILayout.PropertyField(property);
            }
        }

        private void SyncSettingsViewToActiveModule()
        {
            if (_moduleProperty == null)
            {
                return;
            }

            _settingsModule = (PhysicsModule)_moduleProperty.enumValueIndex;
            _settingsDiscipline = PhysicsModuleCatalog.GetDiscipline(_settingsModule);
        }

        private static int GetDisciplineIndex(PhysicsDiscipline discipline)
        {
            for (int index = 0; index < DisciplineOrder.Length; index++)
            {
                if (DisciplineOrder[index] == discipline)
                {
                    return index;
                }
            }

            return 0;
        }

        private static string GetEnumDisplayName(SerializedProperty property)
        {
            if (property == null || property.enumValueIndex < 0 || property.enumValueIndex >= property.enumDisplayNames.Length)
            {
                return string.Empty;
            }

            return property.enumDisplayNames[property.enumValueIndex];
        }
    }
}