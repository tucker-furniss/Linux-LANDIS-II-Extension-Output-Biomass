using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Landis.Library.Metadata;

namespace Landis.Extension.Output.Biomass
{
    public class SummaryLog
    {

        [DataFieldAttribute(Unit = FieldUnits.Year, Desc = "Simulation Year")]
        public int Time { set; get; }

        [DataFieldAttribute(Desc = "Ecoregion Name")]
        public string EcoName { set; get; }

        [DataFieldAttribute(Unit = FieldUnits.Count, Desc = "Number of Active Sites")]
        public int NumActiveSites { set; get; }

        [DataFieldAttribute(Desc = "Mean Aboveground Biomass by Species", SppList = true)]
        public double[] AboveGroundBiomass_ { set; get; }
    }
}
