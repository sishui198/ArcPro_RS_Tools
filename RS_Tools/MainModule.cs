﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Threading.Tasks;

namespace RS_Tools
{
    internal class MainModule : Module
    {
        private static MainModule _this = null;

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        public static MainModule Current
        {
            get
            {
                return _this ?? (_this = (MainModule)FrameworkApplication.FindModule("RS_Tools_Module"));
            }
        }

        #region Overrides
        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            //TODO - add your business logic
            //return false to ~cancel~ Application close
            return true;
        }

        #endregion Overrides

        #region Static Methods

        public static void SetState(string stateID, bool state)
        {
            if (FrameworkApplication.State.Contains(stateID))
            {
                if (!state)
                {
                    FrameworkApplication.State.Deactivate(stateID);
                } 
            } else
            {
                FrameworkApplication.State.Activate(stateID);
            }
            
        }

        #endregion

    }
}
