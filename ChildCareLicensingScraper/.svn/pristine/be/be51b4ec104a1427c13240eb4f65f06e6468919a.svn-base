using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace ChildCareLicensingInspections
{
    class Program
    {
        static void Main(string[] args)
        {
            //var facRecords = Inspections.GetLicInspectionIDs();
            //var inspRecords = Inspections.BuildLicInspectionIDList(facRecords);
            //Inspections.GetInspectionRecords(inspRecords);

            var inspections = Inspections.GetInspections();
            var inspectionWithDatesAndNumViolations = Inspections.AddInspectionDateAndNumViolations(inspections);
            var updatedRows = Inspections.UpdateInspections(inspectionWithDatesAndNumViolations);
            Console.WriteLine(updatedRows);
        }
    }
}
