using System;
using UnityEngine;
using CSharpNumerics.Engines.Multiphysics.Enums;
using CSharpNumerics.Physics.Materials.Engineering;
using CSharpNumerics.Physics.SolidMechanics.Enums;

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
        [Tooltip("Relative magnetic permeability μr")]
        [Range(0.1f, 1000f)] public float magneticPermeability = 1f;

        [Header("2D Geometry (Heat, Electric, Fluid, Magnetic, Stress)")]
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
        [Range(1e-6f, 1f)] public float dt = 0.05f;
        [Range(10, 5000)] public int steps = 300;

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

        [Header("Cylinder Flow")]
        [Tooltip("Inlet velocity U∞ [m/s]")]
        [Range(0.01f, 10f)] public float inletVelocity = 1f;
        [Tooltip("Cylinder centre X position [m]")]
        [Range(0.01f, 5f)] public float cylinderCenterX = 0.2f;
        [Tooltip("Cylinder centre Y position [m]")]
        [Range(0.01f, 5f)] public float cylinderCenterY = 0.2f;
        [Tooltip("Cylinder radius [m]")]
        [Range(0.005f, 1f)] public float cylinderRadius = 0.05f;

        [Header("Magnetostatics")]
        [Tooltip("Current density J at source [A/m²]")]
        public float currentDensity = 1e6f;

        [Header("Plane Stress")]
        [Tooltip("Uniform distributed load [N/m²]")]
        public float uniformLoad = 0f;

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
                case MaterialPreset.Titanium: return EngineeringLibrary.Titanium;
                case MaterialPreset.Brass: return EngineeringLibrary.Brass;
                case MaterialPreset.StainlessSteel: return EngineeringLibrary.StainlessSteel;
                case MaterialPreset.Oil: return EngineeringLibrary.Oil;
                case MaterialPreset.Glycerin: return EngineeringLibrary.Glycerin;
                case MaterialPreset.Wood: return EngineeringLibrary.Wood;
                case MaterialPreset.Rubber: return EngineeringLibrary.Rubber;
                case MaterialPreset.Plastic: return EngineeringLibrary.Plastic;
                case MaterialPreset.Custom:
                    return new EngineeringMaterial(
                        "Custom",
                        thermalConductivity,
                        specificHeat,
                        density,
                        dynamicViscosity,
                        electricPermittivity,
                        youngsModulus,
                        poissonsRatio,
                        magneticPermeability);
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
                case PhysicsModule.FluidFlow2D: return MultiphysicsType.FluidFlow2D;
                case PhysicsModule.CylinderFlow: return MultiphysicsType.CylinderFlow;
                case PhysicsModule.Magnetostatics: return MultiphysicsType.MagneticField;
                case PhysicsModule.PlaneStress: return MultiphysicsType.PlaneStress;
                default: return MultiphysicsType.HeatPlate;
            }
        }
    }

    public enum PhysicsDiscipline
    {
        Thermodynamics,
        SolidMechanics,
        Electromagnetism,
        FluidDynamics
    }

    public enum PhysicsModule
    {
        HeatTransfer,
        Electrostatics,
        PipeFlow,
        BeamStress,
        FluidFlow2D,
        CylinderFlow,
        Magnetostatics,
        PlaneStress
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
        Titanium,
        Brass,
        StainlessSteel,
        Oil,
        Glycerin,
        Wood,
        Rubber,
        Plastic,
        Custom
    }

    public static class PhysicsModuleCatalog
    {
        private static readonly PhysicsModule[] ThermodynamicsModules =
        {
            PhysicsModule.HeatTransfer
        };

        private static readonly PhysicsModule[] SolidMechanicsModules =
        {
            PhysicsModule.BeamStress,
            PhysicsModule.PlaneStress
        };

        private static readonly PhysicsModule[] ElectromagnetismModules =
        {
            PhysicsModule.Electrostatics,
            PhysicsModule.Magnetostatics
        };

        private static readonly PhysicsModule[] FluidDynamicsModules =
        {
            PhysicsModule.PipeFlow,
            PhysicsModule.FluidFlow2D,
            PhysicsModule.CylinderFlow
        };

        public static PhysicsDiscipline GetDiscipline(PhysicsModule module)
        {
            switch (module)
            {
                case PhysicsModule.HeatTransfer:
                    return PhysicsDiscipline.Thermodynamics;
                case PhysicsModule.BeamStress:
                case PhysicsModule.PlaneStress:
                    return PhysicsDiscipline.SolidMechanics;
                case PhysicsModule.Electrostatics:
                case PhysicsModule.Magnetostatics:
                    return PhysicsDiscipline.Electromagnetism;
                case PhysicsModule.PipeFlow:
                case PhysicsModule.FluidFlow2D:
                case PhysicsModule.CylinderFlow:
                    return PhysicsDiscipline.FluidDynamics;
                default:
                    return PhysicsDiscipline.Thermodynamics;
            }
        }

        public static PhysicsModule[] GetModules(PhysicsDiscipline discipline)
        {
            switch (discipline)
            {
                case PhysicsDiscipline.Thermodynamics:
                    return ThermodynamicsModules;
                case PhysicsDiscipline.SolidMechanics:
                    return SolidMechanicsModules;
                case PhysicsDiscipline.Electromagnetism:
                    return ElectromagnetismModules;
                case PhysicsDiscipline.FluidDynamics:
                    return FluidDynamicsModules;
                default:
                    return ThermodynamicsModules;
            }
        }

        public static int GetModuleIndexInDiscipline(PhysicsModule module)
        {
            var modules = GetModules(GetDiscipline(module));
            for (int index = 0; index < modules.Length; index++)
            {
                if (modules[index] == module)
                {
                    return index;
                }
            }

            return 0;
        }

        public static PhysicsModule GetDefaultModule(PhysicsDiscipline discipline)
        {
            var modules = GetModules(discipline);
            return modules.Length > 0 ? modules[0] : PhysicsModule.HeatTransfer;
        }

        public static PhysicsModule GetModule(PhysicsDiscipline discipline, int index)
        {
            var modules = GetModules(discipline);
            if (modules.Length == 0)
            {
                return PhysicsModule.HeatTransfer;
            }

            int clampedIndex = Mathf.Clamp(index, 0, modules.Length - 1);
            return modules[clampedIndex];
        }

        public static string GetDisciplineLabel(PhysicsDiscipline discipline)
        {
            switch (discipline)
            {
                case PhysicsDiscipline.Thermodynamics:
                    return "Thermodynamics";
                case PhysicsDiscipline.SolidMechanics:
                    return "Solid Mechanics";
                case PhysicsDiscipline.Electromagnetism:
                    return "Electromagnetism";
                case PhysicsDiscipline.FluidDynamics:
                    return "Fluid Dynamics";
                default:
                    return "Thermodynamics";
            }
        }

        public static string GetModuleLabel(PhysicsModule module)
        {
            switch (module)
            {
                case PhysicsModule.HeatTransfer:
                    return "Heat Transfer";
                case PhysicsModule.Electrostatics:
                    return "Electrostatics";
                case PhysicsModule.PipeFlow:
                    return "Pipe Flow";
                case PhysicsModule.BeamStress:
                    return "Beam Stress";
                case PhysicsModule.FluidFlow2D:
                    return "2D Fluid Flow";
                case PhysicsModule.CylinderFlow:
                    return "Cylinder Flow";
                case PhysicsModule.Magnetostatics:
                    return "Magnetostatics";
                case PhysicsModule.PlaneStress:
                    return "Plane Stress";
                default:
                    return module.ToString();
            }
        }
    }
}
