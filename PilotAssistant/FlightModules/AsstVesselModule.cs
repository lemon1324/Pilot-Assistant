﻿using System;
using UnityEngine;

namespace PilotAssistant.FlightModules
{

    public class AsstVesselModule : VesselModule
    {
        public Vessel vesselRef;
        public PilotAssistant vesselAsst;
        public SurfSAS vesselSSAS;
        public Stock_SAS vesselStockSAS;
        public VesselData vesselData;

        public void Start()
        {
            try
            {
                vesselRef = GetComponent<Vessel>();
                if (vesselRef == null || vesselRef.isEVA || !vesselRef.isCommandable)
                {
                    vesselRef = null;
                    Destroy(this);
                    return;
                }
                else
                {
                    vesselAsst = new PilotAssistant(this);
                    vesselSSAS = new SurfSAS(this);
                    vesselStockSAS = new Stock_SAS(this);
                    vesselData = new VesselData(this);
                }


                PilotAssistantFlightCore.Instance.addVessel(this);

                vesselAsst.Start();
                vesselSSAS.Start();
                vesselStockSAS.Start();
            }
            catch (Exception ex)
            {
                Debug.LogError("Pilot Assistant: Startup error");
                Debug.LogError(ex.InnerException);
            }

            vesselRef.OnPreAutopilotUpdate += new FlightInputCallback(preAutoPilotUpdate);
            vesselRef.OnPostAutopilotUpdate += new FlightInputCallback(postAutoPilotUpdate);

            GameEvents.onPartCouple.Add(docking);
            GameEvents.onVesselChange.Add(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Add(warpHandler);
        }

        public void Update()
        {
            if (ReferenceEquals(vesselRef, null))
                return;
            vesselAsst.Update();
            vesselSSAS.Update();
        }

        public void warpHandler()
        {
            vesselAsst.warpHandler();
            vesselSSAS.warpHandler();
        }

        public void vesselSwitch(Vessel v)
        {
            if (v == vesselRef)
                vesselAsst.vesselSwitch(v);
        }

        public void docking(GameEvents.FromToAction<Part, Part> evt)
        {
            if (evt.to.vessel == vesselRef)
                Destroy(this);
        }

        public void preAutoPilotUpdate(FlightCtrlState state)
        {
            if (vesselRef.HoldPhysics)
                return;
            vesselData.updateAttitude();
        }

        public void postAutoPilotUpdate(FlightCtrlState state)
        {
            if (vesselRef.HoldPhysics)
                return;
            vesselSSAS.SurfaceSAS(state);
            vesselAsst.vesselController(state);
        }

        public void OnGUI()
        {
            if (PilotAssistantFlightCore.bHideUI || PilotAssistantFlightCore.Instance  == null 
                || PilotAssistantFlightCore.Instance.selectedVesselIndex >= PilotAssistantFlightCore.Instance.controlledVessels.Count
                || PilotAssistantFlightCore.Instance.controlledVessels[PilotAssistantFlightCore.Instance.selectedVesselIndex] != this)
                return;
            vesselAsst.drawGUI();
            vesselSSAS.drawGUI();
            vesselStockSAS.drawGUI();
        }

        public void OnDestroy()
        {
            GameEvents.onVesselChange.Remove(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Remove(warpHandler);
            GameEvents.onPartCouple.Remove(docking);

            if (vesselRef != null)
            {
                vesselRef.OnPreAutopilotUpdate -= preAutoPilotUpdate;
                vesselRef.OnPostAutopilotUpdate -= postAutoPilotUpdate;
            }
            if (!ReferenceEquals(vesselAsst, null))
                vesselAsst.OnDestroy();
            if (!ReferenceEquals(vesselData, null) && !ReferenceEquals(PilotAssistantFlightCore.Instance, null))
                PilotAssistantFlightCore.Instance.removeVessel(this);

            vesselRef = null;
            vesselSSAS = null;
            vesselStockSAS = null;
            vesselAsst = null;
            vesselData = null;
        }
    }
}
