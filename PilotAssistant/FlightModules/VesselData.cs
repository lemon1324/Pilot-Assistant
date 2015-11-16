﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant.FlightModules
{
    using Utility;
    public class VesselData
    {
        public VesselData(AsstVesselModule avm)
        {
            vesModule = avm;
        }

        public AsstVesselModule vesModule;

        public double radarAlt = 0;
        public double pitch = 0;
        public double bank = 0;
        public double yaw = 0;
        public double AoA = 0;
        public double heading = 0;
        public double progradeHeading = 0;
        public double vertSpeed = 0;
        public double acceleration = 0;
        double oldSpd = 0;

        //public Vector3d lastPlanetUp = Vector3d.zero;
        public Vector3d planetUp = Vector3d.zero;
        public Vector3d planetNorth = Vector3d.zero;
        public Vector3d planetEast = Vector3d.zero;

        public Vector3d surfVelForward = Vector3d.zero;
        public Vector3d surfVelRight = Vector3d.zero;

        public Vector3d surfVesForward = Vector3d.zero;
        public Vector3d surfVesRight = Vector3d.zero;

        public Vector3d lastVelocity = Vector3d.zero;
        public Vector3d velocity = Vector3d.zero;

        public Vector3 obtRadial = Vector3.zero;
        public Vector3 obtNormal = Vector3.zero;
        public Vector3 srfRadial = Vector3.zero;
        public Vector3 srfNormal = Vector3.zero;

        /// <summary>
        /// Called in OnPreAutoPilotUpdate. Do not call multiple times per physics frame or the "lastPlanetUp" vector will not be correct and VSpeed will not be calculated correctly
        /// Can't just leave it to a Coroutine becuase it has to be called before anything else
        /// </summary>
        public void updateAttitude()
        {
            //if (PilotAssistantFlightCore.calculateDirection)
            //findVesselFwdAxis(vRef.vesselRef);
            //else
            vesselFacingAxis = vesModule.vesselRef.transform.up;
            planetUp = (vesModule.vesselRef.rootPart.transform.position - vesModule.vesselRef.mainBody.position).normalized;
            planetEast = vesModule.vesselRef.mainBody.getRFrmVel(vesModule.vesselRef.findWorldCenterOfMass()).normalized;
            planetNorth = Vector3d.Cross(planetEast, planetUp).normalized;

            // 4 frames of reference to use. Orientation, Velocity, and both of the previous parallel to the surface
            radarAlt = vesModule.vesselRef.altitude - (vesModule.vesselRef.mainBody.ocean ? Math.Max(vesModule.vesselRef.pqsAltitude, 0) : vesModule.vesselRef.pqsAltitude);
            velocity = vesModule.vesselRef.rootPart.Rigidbody.velocity + Krakensbane.GetFrameVelocity();
            acceleration = Vector3d.Dot(vesModule.vesselRef.acceleration, velocity.normalized);
            vertSpeed = Vector3d.Dot(planetUp, (velocity + lastVelocity) / 2);
            lastVelocity = velocity;

            // Velocity forward and right vectors parallel to the surface
            surfVelRight = Vector3d.Cross(planetUp, vesModule.vesselRef.srf_velocity).normalized;
            surfVelForward = Vector3d.Cross(surfVelRight, planetUp).normalized;
                        
            // Vessel forward and right vectors parallel to the surface
            surfVesRight = Vector3d.Cross(planetUp, vesselFacingAxis).normalized;
            surfVesForward = Vector3d.Cross(surfVesRight, planetUp).normalized;

            obtNormal = Vector3.Cross(vesModule.vesselRef.obt_velocity, planetUp).normalized;
            obtRadial = Vector3.Cross(vesModule.vesselRef.obt_velocity, obtNormal).normalized;
            srfNormal = Vector3.Cross(vesModule.vesselRef.srf_velocity, planetUp).normalized;
            srfRadial = Vector3.Cross(vesModule.vesselRef.srf_velocity, srfNormal).normalized;

            pitch = 90 - Vector3d.Angle(planetUp, vesselFacingAxis);
            heading = (Vector3d.Angle(surfVesForward, planetNorth) * Math.Sign(Vector3d.Dot(surfVesForward, planetEast))).headingClamp(360);
            progradeHeading = (Vector3d.Angle(surfVelForward, planetNorth) * Math.Sign(Vector3d.Dot(surfVelForward, planetEast))).headingClamp(360);
            bank = Vector3d.Angle(surfVesRight, vesModule.vesselRef.ReferenceTransform.right) * Math.Sign(Vector3d.Dot(surfVesRight, -vesModule.vesselRef.ReferenceTransform.forward));

            if (vesModule.vesselRef.srfSpeed > 1)
            {
                Vector3d AoAVec = vesModule.vesselRef.srf_velocity.projectOnPlane(vesModule.vesselRef.ReferenceTransform.right);
                AoA = Vector3d.Angle(AoAVec, vesselFacingAxis) * Math.Sign(Vector3d.Dot(AoAVec, vesModule.vesselRef.ReferenceTransform.forward));

                Vector3d yawVec = vesModule.vesselRef.srf_velocity.projectOnPlane(vesModule.vesselRef.ReferenceTransform.forward);
                yaw = Vector3d.Angle(yawVec, vesselFacingAxis) * Math.Sign(Vector3d.Dot(yawVec, vesModule.vesselRef.ReferenceTransform.right));
            }
            else
                AoA = yaw = 0;

            oldSpd = vesModule.vesselRef.srfSpeed;
        }

        Vector3 vesselFacingAxis = new Vector3();
        /// <summary>
        /// Find the vessel orientation at the CoM by interpolating from surrounding part transforms
        /// This orientation should be significantly more resistant to vessel flex/wobble than the vessel transform (root part) as a free body rotates about it's CoM
        /// 
        /// Has an issue with the origin shifter causing random bounces
        /// </summary>
        public void findVesselFwdAxis(Vessel v)
        {
            Part closestPart = v.rootPart;
            float offset = (closestPart.transform.position - v.CurrentCoM).sqrMagnitude; // only comparing magnitude, sign and actual value don't matter
            
            foreach (Part p in v.Parts)
            {
                float partOffset = (p.partTransform.position - v.CurrentCoM).sqrMagnitude;
                if (partOffset < offset)
                {
                    closestPart = p;
                    offset = partOffset;
                }
            }
            ///
            /// now require two things, accounting for any rotation in part placement, and interpolating with surrounding parts (parent/children/symmetry counterparts) to "shift" the location to the CoM
            /// accounting for rotation is the most important, the nearby position will work for now.
            /// Vector3 location = closestPart.partTransform.position - v.CurrentCoM;
            /// 
            vesselFacingAxis = closestPart.transform.localRotation * closestPart.orgRot.Inverse() * Vector3.up;
            if (ReferenceEquals(closestPart.symmetryCounterparts, null))
            {
                for (int i = 0; i < closestPart.symmetryCounterparts.Count; i++)
                {
                    vesselFacingAxis += closestPart.symmetryCounterparts[i].transform.localRotation * closestPart.symmetryCounterparts[i].orgRot.Inverse() * Vector3.up;
                }
                vesselFacingAxis /= (closestPart.symmetryCounterparts.Count + 1);
            }
        }

        ArrowPointer pointer;
        public void drawArrow(Vector3 dir, Transform t)
        {
            if (ReferenceEquals(pointer, null))
                pointer = ArrowPointer.Create(t, Vector3.zero, dir, 100, Color.red, true);
            else
                pointer.Direction = dir;
        }

        public void destroyArrow()
        {
            UnityEngine.Object.Destroy(pointer);
            pointer = null;
        }
    }
}