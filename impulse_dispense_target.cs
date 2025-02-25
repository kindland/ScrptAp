using Sansar;
using Sansar.Script;
using Sansar.Simulation;
using System;
using System.Collections.Generic;
using System.Linq;

public abstract class LibraryBase : SceneObjectScript
{
    // Assuming LibraryBase contains necessary implementations
}

public class ImpulseOnClick : LibraryBase
{
    [DefaultValue("Click Me!")]
    public Interaction MyInteraction;

    [Tooltip("The impulse to apply when the object is clicked.")]
    [DefaultValue(22.0f)]
    public readonly float ImpulseStrength = 22.0f;

    [Tooltip("The ClusterResource of the object to dispense when clicked.")]
    public ClusterResource DispensedObjectResource;

    [Tooltip("The vertical force to apply to the dispensed object.")]
    [DefaultValue(50.0f)]
    public readonly float VerticalForce = 50.0f;

    [Tooltip("The time in seconds after which the dispensed object will be destroyed.")]
    [DefaultValue(10.0f)]
    public readonly float DestructionTime = 10.0f;

    [Tooltip("The number of objects to dispense per click.")]
    [DefaultValue(1)]
    public readonly int ObjectsPerClick = 1;

    [Tooltip("The minimum impulse to apply to the dispensed object.")]
    [DefaultValue(500.0f)]
    public readonly float MinImpulse = 500.0f;

    [Tooltip("The maximum impulse to apply to the dispensed object.")]
    [DefaultValue(2000.0f)]
    public readonly float MaxImpulse = 2000.0f;

    [Tooltip("The proximity range to detect agents.")]
    [DefaultValue(10.0f)]
    public readonly float ProximityRange = 10.0f;

    [Tooltip("The distance for the raycast sphere.")]
    [DefaultValue(20.0f)]
    public readonly float RaycastDistance = 20.0f;

    private LinkedList<ScenePrivate.CreateClusterData> dispensedObjects = new LinkedList<ScenePrivate.CreateClusterData>();

    public override void Init()
    {
        MyInteraction.Subscribe(OnClick);
    }

    private void OnClick(InteractionData data)
    {
        RigidBodyComponent rigidBody;
        if (ObjectPrivate.TryGetFirstComponent(out rigidBody))
        {
            // Get the hit normal from the interaction data
            Vector hitNormal = data.HitNormal;

            // Manually invert the hit normal to push in the opposite direction
            Vector oppositeNormal = new Vector(-hitNormal.X, -hitNormal.Y, -hitNormal.Z);

            // Scale the inverted normal by the impulse strength
            Vector impulse = oppositeNormal * ImpulseStrength;

            // Log the impulse values
            Log.Write(LogLevel.Info, $"Applying impulse: {impulse}");

            // Apply the impulse
            rigidBody.AddLinearImpulse(impulse, (result) =>
            {
                if (!result.Success)
                {
                    Log.Write(LogLevel.Error, "Failed to apply impulse.");
                }
            });

            // Dispense the specified number of objects with the inverted direction
            for (int i = 0; i < ObjectsPerClick; i++)
            {
                DispenseObject(oppositeNormal);
            }
        }
        else
        {
            Log.Write(LogLevel.Error, "No RigidBodyComponent found.");
        }
    }

    private void DispenseObject(Vector direction)
    {
        if (DispensedObjectResource != null)
        {
            try
            {
                ScenePrivate.CreateCluster(DispensedObjectResource, ObjectPrivate.Position, ObjectPrivate.Rotation, Vector.Zero, (ScenePrivate.CreateClusterData dispensedObject) =>
                {
                    if (dispensedObject.Success)
                    {
                        // Set the new object to be dynamic
                        RigidBodyComponent newRigidBody;
                        if (dispensedObject.ClusterReference.GetObjectPrivates().FirstOrDefault().TryGetFirstComponent(out newRigidBody))
                        {
                            newRigidBody.SetMotionType(RigidBodyMotionType.MotionTypeDynamic);

                            // Apply random force in the inverted direction
                            Random random = new Random();
                            float randomImpulse = (float)(random.NextDouble() * (MaxImpulse - MinImpulse) + MinImpulse);
                            Vector impulse = direction * randomImpulse;
                            newRigidBody.AddLinearImpulse(impulse, (result) =>
                            {
                                if (!result.Success)
                                {
                                    Log.Write(LogLevel.Error, "Failed to apply vertical impulse.");
                                }
                            });

                            // Start homing behavior
                            StartHomingBehavior(dispensedObject.ClusterReference, newRigidBody);
                        }

                        // Log the creation of the new object
                        Log.Write(LogLevel.Info, "Dispensed a new object.");

                        // Add the dispensed object to the list
                        dispensedObjects.AddLast(dispensedObject);

                        // Destroy the object after the specified time
                        CreateDestroyTimer();
                    }
                    else
                    {
                        Log.Write(LogLevel.Error, "Failed to dispense object.");
                    }
                });
            }
            catch (ThrottleException)
            {
                // Throttled
                Log.Write(LogLevel.Warning, "DispenseObject throttle hit. No object created.");
            }
        }
        else
        {
            Log.Write(LogLevel.Error, "No object resource specified to dispense.");
        }
    }

    private void StartHomingBehavior(Cluster clusterReference, RigidBodyComponent rigidBody)
    {
        Timer.Create(TimeSpan.FromSeconds(0.1), () =>
        {
            RigidBodyComponent targetRigidBody = FindTargetRigidBody(clusterReference.GetObjectPrivates().FirstOrDefault().Position, RaycastDistance);
            if (targetRigidBody != null)
            {
                Vector directionToTarget = (targetRigidBody.GetPosition() - clusterReference.GetObjectPrivates().FirstOrDefault().Position).Normalized();
                Vector homingImpulse = directionToTarget * VerticalForce;
                rigidBody.AddLinearImpulse(homingImpulse, (result) =>
                {
                    if (!result.Success)
                    {
                        Log.Write(LogLevel.Error, "Failed to apply homing impulse.");
                    }
                });
            }
        });
    }

    private RigidBodyComponent FindTargetRigidBody(Vector position, float range)
    {
        RigidBodyComponent targetRigidBody = null;

        var hits = ScenePrivate.CastRay(position, position + Vector.ObjectForward * range, 10);
        foreach (var hit in hits)
        {
            ObjectPrivate hitObject = ScenePrivate.FindObject(hit.ComponentId.ObjectId);
            if (hitObject != null && hitObject.TryGetFirstComponent(out RigidBodyComponent rigidBody) && hitObject.TryGetFirstComponent(out CharacterComponent cc))
            {
                if (cc != null)
                {
                    targetRigidBody = rigidBody;
                    break; // Stop the raycast after finding the first target
                }
            }
        }

        return targetRigidBody;
    }

    private void CreateDestroyTimer()
    {
        Timer.Create(TimeSpan.FromSeconds(DestructionTime), () =>
        {
            DestroyOldestObject();
        });
    }

    private void DestroyOldestObject()
    {
        if (dispensedObjects.Count > 0)
        {
            var dispensedObject = dispensedObjects.First;
            dispensedObjects.RemoveFirst();

            dispensedObject.Value.ClusterReference.Destroy();
            Log.Write(LogLevel.Info, "Destroyed the dispensed object after the specified time.");
        }
    }
}