using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using static RS_Tools.Tools.DomainAppointer.DataService;

namespace RS_Tools.Tools.DomainAppointer.Buttons
{
    internal class Domain05 : Button
    {
        DockPane pane = null;
        protected override void OnClick()
        {
            if (pane == null) pane = FrameworkApplication.DockPaneManager.Find("RS_Tools_Tools_DomainAppointer_DomainAppointerSettings");
            if (pane != null) (pane as DomainAppointerSettingsViewModel).ApplyDomain(DomainCode.Code5);
        }
    }
}
