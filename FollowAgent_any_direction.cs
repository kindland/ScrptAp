using Sansar;
using Sansar.Script;
using Sansar.Simulation;
using System;
using System.Linq;

public class BulletShooter : SceneObjectScript
{
    [Tooltip("The ClusterResource of the bullet to dispense.")]
    public ClusterResource BulletResource;

    [Tooltip("The speed of the bullet.")]
    [DefaultValue(50.0f)]
    public float BulletSpeed;

    [Tooltip("The interval in seconds between each bullet shot.")]
    [DefaultValue(1.0f)]
    public float ShootInterval;

    private AgentPrivate targetAgent = null;

    public override void Init()
    {
        if (BulletResource == null)
        {
            Log.Write(LogLevel.Error, "BulletShooter requires a ClusterResource set to work properly.");
            return;
        }

        ScenePrivate.User.Subscribe(User.AddUser, (UserData data) =>
        {
            targetAgent = ScenePrivate.FindAgent(data.User);
        });

        ScenePrivate.User.Subscribe(User.RemoveUser, (UserData data) =>
        {
            if ((targetAgent != null) && (targetAgent.AgentInfo.SessionId == data.User))
                targetAgent = null;

            if (targetAgent == null)
                targetAgent = ScenePrivate.GetAgents().FirstOrDefault();
        });

        Timer.Create(TimeSpan.FromSeconds(ShootInterval), true, ShootBullet);
    }

    private void ShootBullet()
    {
        if (targetAgent == null)
            return;

        Vector targetPosition = ScenePrivate.FindObject(targetAgent.AgentInfo.ObjectId).Position;
        Vector shooterPosition = ObjectPrivate.Position;

        Vector direction = (targetPosition - shooterPosition).Normalized();
        Vector velocity = direction * BulletSpeed;

        try
        {
            ScenePrivate.CreateCluster(BulletResource, shooterPosition, ObjectPrivate.Rotation, velocity, (ScenePrivate.CreateClusterData dispensedObject) =>
            {
                if (dispensedObject.Success)
                {
                    // Set the new object to be dynamic
                    RigidBodyComponent newRigidBody;
                    if (dispensedObject.ClusterReference.GetObjectPrivates().FirstOrDefault().TryGetFirstComponent(out newRigidBody))
                    {
                        newRigidBody.SetMotionType(RigidBodyMotionType.MotionTypeDynamic);
                        newRigidBody.AddLinearImpulse(velocity, (result) =>
                        {
                            if (!result.Success)
                            {
                                Log.Write(LogLevel.Error, "Failed to apply impulse to the bullet.");
                            }
                        });
                    }
                }
                else
                {
                    Log.Write(LogLevel.Error, "Failed to dispense bullet.");
                }
            });
        }
        catch (ThrottleException)
        {
            // Throttled
            Log.Write(LogLevel.Warning, "ShootBullet throttle hit. No bullet created.");
        }
    }
}