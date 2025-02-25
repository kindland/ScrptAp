using Sansar.Simulation;
using Sansar.Script;
using Sansar;
using System;

public class ImpulseOnClick : SceneObjectScript
{
    [DefaultValue("Click Me!")]
    public Interaction MyInteraction;

    [Tooltip("The impulse to apply when the object is clicked.")]
    [DefaultValue(22.0f)]
    public readonly float ImpulseStrength = 22.0f;

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
        }
        else
        {
            Log.Write(LogLevel.Error, "No RigidBodyComponent found.");
        }
    }
}