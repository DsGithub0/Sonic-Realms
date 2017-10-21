﻿using SonicRealms.Core.Triggers;
using SonicRealms.Core.Utils;
using UnityEngine;

namespace SonicRealms.Level.Platforms
{
    /// <summary>
    /// Moves the player forward or backward on the platform.
    /// </summary>
    public class ConveyorBelt : ReactivePlatform
    {
        /// <summary>
        /// The player is moved by this amount on the surface, in units per second. Positive means
        /// forward, negative means backward.
        /// </summary>
        [SerializeField] public float Velocity;

        private float _lastSurfaceAngle;

        public override void Reset()
        {
            base.Reset();
            Velocity = 2.5f;
        }

        // Translate the controller by the amount defined in Velocity and the direction defined by its
        // surface angle.
        public override void OnSurfaceStay(SurfaceCollision collision)
        {
            var controller = collision.Controller;
            if (collision.Controller == null) return;

            controller.transform.Translate(SrMath.UnitVector(controller.SurfaceAngle*Mathf.Deg2Rad)*Velocity*
                                 Time.fixedDeltaTime);

            _lastSurfaceAngle = controller.SurfaceAngle;
        }

        // Transfer momentum to the controller when it leaves the conveyor belt.
        public override void OnSurfaceExit(SurfaceCollision collision)
        {
            var controller = collision.Controller;
            if (collision.Controller == null)
                return;

            if (controller.Grounded)
                controller.GroundVelocity += Velocity;
            else
                controller.Velocity += SrMath.UnitVector(_lastSurfaceAngle*Mathf.Deg2Rad)*Velocity;
        }
    }
}
