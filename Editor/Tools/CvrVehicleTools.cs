using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for creating and configuring ChilloutVR CCK drivable vehicles, suspension physics, and passenger seating.
    /// </summary>
    public class ConfigureCvrVehicleTool : McpToolBase
    {
        public ConfigureCvrVehicleTool()
        {
            Name = "configure_cvr_vehicle";
            Description = "Creates and configures ChilloutVR CCK drivable vehicles: 4-wheel chassis rigs, WheelCollider suspension tuning, and passenger seats.";
            IsAsync = false;
        }

        public override JObject Execute(JObject parameters)
        {
            string action = parameters["action"]?.ToObject<string>()?.ToLowerInvariant() ?? "create_car_rig";
            string reason = parameters["reason"]?.ToObject<string>();

            switch (action)
            {
                case "create_car_rig":
                    return HandleCreateCarRig(parameters, reason);

                case "configure_suspension":
                    return HandleConfigureSuspension(parameters, reason);

                case "add_passenger_seats":
                    return HandleAddPassengerSeats(parameters, reason);

                default:
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Unknown action '{action}'. Supported actions: 'create_car_rig', 'configure_suspension', 'add_passenger_seats'",
                        "invalid_action"
                    );
            }
        }

        private JObject HandleCreateCarRig(JObject parameters, string reason)
        {
            string vehicleName = parameters["vehicleName"]?.ToObject<string>() ?? "CVR_Drivable_Car";
            float mass = parameters["mass"]?.ToObject<float?>() ?? 1200f;
            float spring = parameters["spring"]?.ToObject<float?>() ?? 30000f;
            float damper = parameters["damper"]?.ToObject<float?>() ?? 4500f;
            float suspensionDistance = parameters["suspensionDistance"]?.ToObject<float?>() ?? 0.2f;
            float wheelRadius = parameters["wheelRadius"]?.ToObject<float?>() ?? 0.35f;
            bool addHeadlights = parameters["addHeadlights"]?.ToObject<bool?>() ?? true;
            bool addEngineAudio = parameters["addEngineAudio"]?.ToObject<bool?>() ?? true;

            GameObject root = new GameObject(vehicleName);
            Undo.RegisterCreatedObjectUndo(root, "Create CVR Vehicle Rig");

            // Rigidbody
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.5f;
#else
            rb.drag = 0.05f;
            rb.angularDrag = 0.5f;
#endif
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Low Center of Mass
            GameObject com = new GameObject("CenterOfMass");
            com.transform.SetParent(root.transform);
            com.transform.localPosition = new Vector3(0, -0.35f, 0);
            rb.centerOfMass = com.transform.localPosition;

            // Body Visual & Box Collider
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "BodyVisual";
            body.transform.SetParent(root.transform);
            body.transform.localScale = new Vector3(1.9f, 0.85f, 3.8f);
            body.transform.localPosition = new Vector3(0, 0.65f, 0);

            // Driver Seat
            GameObject driverSeat = new GameObject("DriverSeat");
            driverSeat.transform.SetParent(root.transform);
            driverSeat.transform.localPosition = new Vector3(-0.45f, 0.5f, 0.1f);
            Type cvrSeatType = FindCckType("CVRSeat");
            if (cvrSeatType != null) Undo.AddComponent(driverSeat, cvrSeatType);

            // Steering Wheel with CVRInteractable
            GameObject steeringWheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            steeringWheel.name = "SteeringWheel";
            steeringWheel.transform.SetParent(root.transform);
            steeringWheel.transform.localScale = new Vector3(0.35f, 0.04f, 0.35f);
            steeringWheel.transform.localRotation = Quaternion.Euler(60f, 0, 0);
            steeringWheel.transform.localPosition = new Vector3(-0.45f, 0.8f, 0.6f);

            Type cvrInteractableType = FindCckType("CVRInteractable");
            if (cvrInteractableType != null) Undo.AddComponent(steeringWheel, cvrInteractableType);

            // 4 Wheel Colliders & Visuals
            Vector3[] wheelOffsets = new[]
            {
                new Vector3(-0.9f, 0.35f, 1.3f),   // FL
                new Vector3(0.9f, 0.35f, 1.3f),    // FR
                new Vector3(-0.9f, 0.35f, -1.3f),  // RL
                new Vector3(0.9f, 0.35f, -1.3f)   // RR
            };
            string[] wheelNames = new[] { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            JArray wheelsInfo = new JArray();

            for (int i = 0; i < 4; i++)
            {
                GameObject wheelGo = new GameObject(wheelNames[i]);
                wheelGo.transform.SetParent(root.transform);
                wheelGo.transform.localPosition = wheelOffsets[i];

                WheelCollider wc = wheelGo.AddComponent<WheelCollider>();
                wc.radius = wheelRadius;
                wc.suspensionDistance = suspensionDistance;
                JointSpring js = wc.suspensionSpring;
                js.spring = spring;
                js.damper = damper;
                js.targetPosition = 0.5f;
                wc.suspensionSpring = js;

                // Visual Wheel Mesh
                GameObject wheelMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheelMesh.name = "Mesh";
                wheelMesh.transform.SetParent(wheelGo.transform);
                wheelMesh.transform.localScale = new Vector3(wheelRadius * 2f, 0.15f, wheelRadius * 2f);
                wheelMesh.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                wheelMesh.transform.localPosition = Vector3.zero;

                // Remove collider from visual wheel mesh (physics handled by WheelCollider)
                Collider col = wheelMesh.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);

                wheelsInfo.Add(new JObject
                {
                    ["name"] = wheelNames[i],
                    ["radius"] = wheelRadius,
                    ["isSteerable"] = i < 2,
                    ["isMotor"] = i >= 2
                });
            }

            // Headlights
            if (addHeadlights)
            {
                GameObject lightsGroup = new GameObject("Headlights");
                lightsGroup.transform.SetParent(root.transform);

                GameObject leftLight = new GameObject("Light_Left");
                leftLight.transform.SetParent(lightsGroup.transform);
                leftLight.transform.localPosition = new Vector3(-0.65f, 0.6f, 1.9f);
                Light lightL = leftLight.AddComponent<Light>();
                lightL.type = LightType.Spot;
                lightL.range = 35f;
                lightL.spotAngle = 60f;
                lightL.intensity = 2.5f;
                lightL.color = new Color(1.0f, 0.95f, 0.8f);

                GameObject rightLight = new GameObject("Light_Right");
                rightLight.transform.SetParent(lightsGroup.transform);
                rightLight.transform.localPosition = new Vector3(0.65f, 0.6f, 1.9f);
                Light lightR = rightLight.AddComponent<Light>();
                lightR.type = LightType.Spot;
                lightR.range = 35f;
                lightR.spotAngle = 60f;
                lightR.intensity = 2.5f;
                lightR.color = new Color(1.0f, 0.95f, 0.8f);
            }

            // Engine Audio
            if (addEngineAudio)
            {
                GameObject audioGo = new GameObject("EngineAudio");
                audioGo.transform.SetParent(root.transform);
                audioGo.transform.localPosition = new Vector3(0, 0.6f, 1.2f);
                AudioSource audio = audioGo.AddComponent<AudioSource>();
                audio.spatialBlend = 1.0f;
                audio.rolloffMode = AudioRolloffMode.Logarithmic;
                audio.minDistance = 1.5f;
                audio.maxDistance = 30f;
                audio.loop = true;
                audio.playOnAwake = false;
            }

            // Network State Synchronization
            Type cvrVarBufferType = FindCckType("CVRVariableBuffer");
            if (cvrVarBufferType != null)
            {
                Undo.AddComponent(root, cvrVarBufferType);
            }

            EditorUtility.SetDirty(root);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            McpLogger.LogInfo($"[MCP Unity] Created drivable vehicle '{vehicleName}' (mass={mass}kg, spring={spring})" + (reason != null ? $" — {reason}" : ""));

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Successfully created ChilloutVR drivable car rig '{vehicleName}'",
                ["vehicleName"] = vehicleName,
                ["instanceId"] = UnityObjectId.GetObjectId(root),
                ["mass"] = mass,
                ["wheelCount"] = 4,
                ["wheels"] = wheelsInfo,
                ["hasDriverSeat"] = true,
                ["hasHeadlights"] = addHeadlights,
                ["hasEngineAudio"] = addEngineAudio,
                ["cckInstalled"] = cvrSeatType != null
            };
        }

        private JObject HandleConfigureSuspension(JObject parameters, string reason)
        {
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            string objectPath = parameters["objectPath"]?.ToObject<string>();

            float? mass = parameters["mass"]?.ToObject<float?>();
            float? spring = parameters["spring"]?.ToObject<float?>();
            float? damper = parameters["damper"]?.ToObject<float?>();
            float? suspensionDistance = parameters["suspensionDistance"]?.ToObject<float?>();
            float? targetPosition = parameters["targetPosition"]?.ToObject<float?>();
            float? forwardStiffness = parameters["forwardStiffness"]?.ToObject<float?>();
            float? sidewaysStiffness = parameters["sidewaysStiffness"]?.ToObject<float?>();
            float? centerOfMassY = parameters["centerOfMassY"]?.ToObject<float?>();

            JObject findError = GameObjectToolUtils.FindGameObject(instanceId, objectPath, out GameObject targetObject, out string idInfo);
            if (findError != null) return findError;

            Rigidbody rb = targetObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Undo.RecordObject(rb, "Configure Vehicle Rigidbody");
                if (mass.HasValue) rb.mass = Mathf.Max(10f, mass.Value);
                if (centerOfMassY.HasValue) rb.centerOfMass = new Vector3(rb.centerOfMass.x, centerOfMassY.Value, rb.centerOfMass.z);
            }

            WheelCollider[] wheels = targetObject.GetComponentsInChildren<WheelCollider>(true);
            foreach (var wc in wheels)
            {
                Undo.RecordObject(wc, "Configure Wheel Suspension");

                if (suspensionDistance.HasValue) wc.suspensionDistance = Mathf.Max(0.01f, suspensionDistance.Value);

                JointSpring js = wc.suspensionSpring;
                if (spring.HasValue) js.spring = Mathf.Max(100f, spring.Value);
                if (damper.HasValue) js.damper = Mathf.Max(10f, damper.Value);
                if (targetPosition.HasValue) js.targetPosition = Mathf.Clamp01(targetPosition.Value);
                wc.suspensionSpring = js;

                if (forwardStiffness.HasValue)
                {
                    WheelFrictionCurve fc = wc.forwardFriction;
                    fc.stiffness = forwardStiffness.Value;
                    wc.forwardFriction = fc;
                }

                if (sidewaysStiffness.HasValue)
                {
                    WheelFrictionCurve sc = wc.sidewaysFriction;
                    sc.stiffness = sidewaysStiffness.Value;
                    wc.sidewaysFriction = sc;
                }
            }

            EditorUtility.SetDirty(targetObject);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            McpLogger.LogInfo($"[MCP Unity] Tuned suspension on '{targetObject.name}' across {wheels.Length} wheels" + (reason != null ? $" — {reason}" : ""));

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Successfully updated suspension and physics on '{targetObject.name}' ({wheels.Length} wheels tuned)",
                ["vehicleName"] = targetObject.name,
                ["instanceId"] = UnityObjectId.GetObjectId(targetObject),
                ["wheelsTuned"] = wheels.Length,
                ["spring"] = spring,
                ["damper"] = damper,
                ["suspensionDistance"] = suspensionDistance
            };
        }

        private JObject HandleAddPassengerSeats(JObject parameters, string reason)
        {
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            int seatCount = Mathf.Clamp(parameters["seatCount"]?.ToObject<int?>() ?? 3, 1, 8);

            JObject findError = GameObjectToolUtils.FindGameObject(instanceId, objectPath, out GameObject targetObject, out string idInfo);
            if (findError != null) return findError;

            // Pre-configured passenger offsets
            Vector3[] passengerOffsets = new[]
            {
                new Vector3(0.45f, 0.5f, 0.1f),    // Front Passenger
                new Vector3(-0.45f, 0.5f, -0.9f),  // Rear Left
                new Vector3(0.45f, 0.5f, -0.9f),   // Rear Right
                new Vector3(0f, 0.5f, -0.9f),      // Rear Middle
                new Vector3(-0.45f, 0.5f, -1.8f),  // 3rd Row Left
                new Vector3(0.45f, 0.5f, -1.8f),   // 3rd Row Right
                new Vector3(0f, 0.5f, -1.8f),      // 3rd Row Middle
                new Vector3(0f, 0.5f, 0.1f)        // Front Middle
            };

            Type cvrSeatType = FindCckType("CVRSeat");
            JArray createdSeats = new JArray();

            for (int i = 0; i < seatCount && i < passengerOffsets.Length; i++)
            {
                string seatName = $"PassengerSeat_{i + 1}";
                GameObject seatGo = new GameObject(seatName);
                seatGo.transform.SetParent(targetObject.transform);
                seatGo.transform.localPosition = passengerOffsets[i];
                Undo.RegisterCreatedObjectUndo(seatGo, "Add Passenger Seat");

                if (cvrSeatType != null)
                {
                    Undo.AddComponent(seatGo, cvrSeatType);
                }

                createdSeats.Add(new JObject
                {
                    ["name"] = seatName,
                    ["position"] = new JObject { ["x"] = passengerOffsets[i].x, ["y"] = passengerOffsets[i].y, ["z"] = passengerOffsets[i].z }
                });
            }

            EditorUtility.SetDirty(targetObject);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            McpLogger.LogInfo($"[MCP Unity] Added {createdSeats.Count} passenger seats to '{targetObject.name}'" + (reason != null ? $" — {reason}" : ""));

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Successfully added {createdSeats.Count} passenger seats to '{targetObject.name}'",
                ["vehicleName"] = targetObject.name,
                ["instanceId"] = UnityObjectId.GetObjectId(targetObject),
                ["seatsAdded"] = createdSeats.Count,
                ["seats"] = createdSeats
            };
        }

        private static Type FindCckType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = assembly.GetType($"ABI.CCK.Components.{typeName}")
                          ?? assembly.GetType($"ABI.CCK.Scripts.{typeName}")
                          ?? assembly.GetType(typeName);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }
    }
}
