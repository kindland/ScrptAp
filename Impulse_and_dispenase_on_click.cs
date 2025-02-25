using Sansar.Simulation;
using Sansar.Script;
using Sansar;
using System;
using System.Collections.Generic;
using System.Linq;

public class ImpulseOnClick : SceneObjectScript
{
    [DefaultValue("Click Me!")]
    public Interaction MyInteraction;

    [Tooltip("The impulse to apply when the object is clicked.")]
    [DefaultValue(22.0f)]
    public readonly float ImpulseStrength = 22.0f;

    [Tooltip("The ClusterResource of the object to dispense when clicked.")]
    public ClusterResource DispensedObjectResource;

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

            // Dispense a new object
            DispenseObject();
        }
        else
        {
            Log.Write(LogLevel.Error, "No RigidBodyComponent found.");
        }
    }

    private void DispenseObject()
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
                        }

                        // Log the creation of the new object
                        Log.Write(LogLevel.Info, "Dispensed a new object.");

                        // Add the dispensed object to the list
                        dispensedObjects.AddLast(dispensedObject);

                        // Destroy the object after 10 seconds
                        Timer.Create(TimeSpan.FromSeconds(10), () =>
                        {
                            DestroyOldestObject();
                        });
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

    private void DestroyOldestObject()
    {
        if (dispensedObjects.Count > 0)
        {
            var dispensedObject = dispensedObjects.First;
            dispensedObjects.RemoveFirst();

            dispensedObject.Value.ClusterReference.Destroy();
            Log.Write(LogLevel.Info, "Destroyed the dispensed object after 10 seconds.");
        }
    }
}