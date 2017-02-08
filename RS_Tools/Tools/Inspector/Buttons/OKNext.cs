﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace RS_Tools.Tools.Inspector.Buttons
{
    internal class OKNext : Button
    {
        DockPane pane = null;
        protected override void OnClick()
        {
            if (pane == null) pane = FrameworkApplication.DockPaneManager.Find("RS_Tools_Tools_Inspector_InspectorSettings");
            if (pane != null) (pane as InspectorSettingsViewModel).OkNext();
        }
    }
}
