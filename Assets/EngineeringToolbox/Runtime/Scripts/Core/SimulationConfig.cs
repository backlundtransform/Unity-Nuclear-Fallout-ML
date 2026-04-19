using System;
using UnityEngine;
using CSharpNumerics.Engines.Multiphysics.Enums;
using CSharpNumerics.Physics.Materials.Engineering;

namespace EngineeringToolbox.Core
{
    /// <summary>
    /// Serializable configuration for a single simulation run.
    /// Exposed in Inspector via the SimulationManager component.
    /// </summary>
    [Serializable]
    public class SimulationConfig
    {
        [Header("Module")]
        [Tooltip("Which physics module to run")]
        public PhysicsModule module = PhysicsModule.HeatTransfer;

        [Header("Material")]
        [Tooltip("Select a predefined material or choose Custom")]
        public MaterialPreset materialPreset = MaterialPreset.Steel;

        [Header("Custom Material (used when materialPreset = Custom)")]
        [Tooltip("Thermal conductivity k [W/(m·K)]")]
        [Range(0.01f, 500f)] public float thermalConductivity = 50f;
        [Tooltip("Specific heat cp [J/(kg·K)]")]
        [Range(100f, 5000f)] public float specificHeat = 500f;
        [Tooltip("Density ρ [kg/m³]")]
        [Range(0.1f, 20000f)] public float density = 7850f;
        [Tooltip("Dynamic viscosity μ [Pa·s]")]
        public float dynamicViscosity = 1e-3f;
        [Tooltip("Electric permittivity εr (relative)")]
        [Range(1f, 100f)] public float electricPermittivity = 1f;
        [Tooltip("Young's modulus E [Pa]")]
        public float youngsModulus = 200e9f;
        [Tooltip("Poisson's ratio ν")]
        [Range(0f, 0.5f)] public float poissonsRatio = 0.3f;

        [Header("2D Geometry (Heat, Electric)")]
        [Range(0.01f, 10f)] public float width = 0.1f;
        [Range(0.01f, 10f)] public float height = 0.1f;
        [Range(5, 100)] public int nx = 30;
        [Range(5, 100)] public int ny = 30;

        [Header("Boundary Conditions (2D)")]
        public float topBC = 100f;
        public float bottomBC = 0f;
        public float leftBC = 0f;
        public float rightBC = 0f;

        [Header("Time Stepping")]
        [Range(1e-6f, 0.1f)] public float dt = 0.0001f;
        [Range(10, 5000)] public int steps = 500;

        [Header("1D Geometry (Pipe, Beam)")]
        [Range(0.1f, 10f)] public float length = 2f;
        [Range(0.001f, 0.1f)] public float radius = 0.005f;
        [Range(10, 500)] public int nodes = 101;

        [Header("Beam")]
        public BeamSupport beamSupport = BeamSupport.Cantilever;
        [Range(0.01f, 0.5f)] public float sectionWidth = 0.05f;
        [Range(0.01f, 0.5f)] public float sectionHeight = 0.1f;
        public float pointLoadValue = 1000f;
        [Range(0f, 1f)] public float pointLoadPosition = 1f; // fraction of length
        public float distributedLoad = 500f;

        [Header("Pipe Flow")]
        public float pressureGradient = -100f;

        /// <summary>
        /// Returns the <see cref="EngineeringMaterial"/> for the current preset,
        /// or builds a custom one from Inspector fields.
        /// </summary>
        public EngineeringMaterial GetMaterial()
        {
            switch (materialPreset)
            {
                case MaterialPreset.Steel: return EngineeringLibrary.Steel;
                case MaterialPreset.Aluminum: return EngineeringLibrary.Aluminum;
                case MaterialPreset.Copper: return EngineeringLibrary.Copper;
                case MaterialPreset.Water: return EngineeringLibrary.Water;
                case MaterialPreset.Air: return EngineeringLibrary.Air;
                case MaterialPreset.Concrete: return EngineeringLibrary.Concrete;
                case MaterialPreset.Glass: return EngineeringLibrary.Glass;
                case MaterialPreset.Custom:
                    return new EngineeringMaterial(
                        "Custom",
                        thermalConductivity,
                        specificHeat,
                        density,
                        dynamicViscosity,
                        electricPermittivity,
                        youngsModulus,
                        poissonsRatio);
                default: return EngineeringLibrary.Steel;
            }
        }

        /// <summary>
        /// Maps <see cref="PhysicsModule"/> to the CSharpNumerics enum.
        /// </summary>
        public MultiphysicsType GetMultiphysicsType()
        {
            switch (module)
            {
                case PhysicsModule.HeatTransfer: return MultiphysicsType.HeatPlate;
                case PhysicsModule.Electrostatics: return MultiphysicsType.ElectricField;
                case PhysicsModule.PipeFlow: return MultiphysicsType.PipeFlow;
                case PhysicsModule.BeamStress: return MultiphysicsType.BeamStress;
                default: return MultiphysicsType.HeatPlate;
            }
        }
    }

    public enum PhysicsModule
    {
        HeatTransfer,
        Electrostatics,
        PipeFlow,
        BeamStress
    }

    public enum MaterialPreset
    {
        Steel,
        Aluminum,
        Copper,
        Water,
        Air,
        Concrete,
        Glass,
        Custom
    }
}
