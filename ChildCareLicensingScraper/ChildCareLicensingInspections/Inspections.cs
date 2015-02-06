using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Configuration;
using ChildCareLicensingCleaner;
using ChildCareLicensingScraper;
using System.Text.RegularExpressions;
using System.Data;
using System.Transactions;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.ComponentModel;

namespace ChildCareLicensingInspections
{
    static class Inspections
    {
        public static IEnumerable<Inspection> BuildLicInspectionIDList(IEnumerable<Facility> facilities)
        {
            List<Inspection> myList = new List<Inspection>();
            foreach (Facility f in facilities)
            {
                foreach (string s in f.Inspections)
                {
                    if (s != "")
                    {
                        Inspection i = new Inspection(f.FacilityLicID, Int32.Parse(s));
                        yield return i;
                    }
                }
            }
        }
        
        public static IEnumerable<Facility> GetLicInspectionIDs()
        {
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Conn"].ConnectionString))
            {
                conn.Open();
                string queryString = "SELECT FacilityLicID, Inspections FROM dbo.Facilities";

                SqlCommand command = new SqlCommand(queryString, conn);
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Facility f = new Facility(Int32.Parse(reader[0].ToString()));
                    f.Inspections = reader.GetString(reader.GetOrdinal("Inspections")).Split(',').ToList();
                    yield return f;
                }
            }
            Console.WriteLine("Inspection IDs retrieved");
        }

        public static void GetInspectionRecords(IEnumerable<Inspection> inspections)
        {
            foreach (Inspection i in inspections)
            {
                string url = "http://www.dss.virginia.gov/printer/facility/search/cc.cgi?rm=Inspection;Inspection=" + i.InspectionID + ";ID=" + i.FacilityLicID + ";";
                String inspPage = ChildCareLicensingScraper.Program.GetWebPage(url);
                Inspection insp = new Inspection(i.FacilityLicID, i.InspectionID);
                insp.Page = inspPage;
                WritePageToBase(insp);
            }
        }
        
        public static void WritePageToBase(Inspection insp)
        {
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Conn"].ConnectionString))
            {
                conn.Open();
                using (TransactionScope scope = new TransactionScope())
                {
                    string sqlIns = "INSERT INTO Inspections (FacilityLicID, InspectionID, Page) VALUES (@FacilityLicID, @InspectionID, @Page)";

                    SqlCommand cmdIns = new SqlCommand(sqlIns, conn);
                    cmdIns.Parameters.Add("@Page", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@InspectionID", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@FacilityLicID", System.Data.SqlDbType.VarChar);

                    //for (int i = 0; i < pages.Count; i++)
                    //{
                        cmdIns.Parameters["@Page"].Value = GetDataValue(insp.Page);
                        cmdIns.Parameters["@InspectionID"].Value = GetDataValue(insp.InspectionID.ToString());
                        cmdIns.Parameters["@FacilityLicID"].Value = GetDataValue(insp.FacilityLicID.ToString());
                        cmdIns.ExecuteNonQuery();
                        Console.WriteLine(GetDataValue(insp.FacilityLicID.ToString() + ":" + insp.InspectionID.ToString()) + " page written to database");
                    //}
                    scope.Complete();
                }
            }
        }


        public static ConcurrentBag<Inspection> GetInspections()
        {
            var inspections = new ConcurrentBag<Inspection>();
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Conn"].ConnectionString))
            {
                conn.Open();
                string queryString = "SELECT FacilityLicID, InspectionID, InspectionStart, NumViolations, Page FROM dbo.Inspections";

                SqlCommand command = new SqlCommand(queryString, conn);
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Inspection i = new Inspection(Int32.Parse(reader["FacilityLicID"].ToString()), Int32.Parse(reader["InspectionID"].ToString()));
                    i.Page = reader["Page"].ToString();
                    inspections.Add(i);
                }
            }
            return inspections;
        }

        public static int UpdateInspections(ConcurrentBag<Inspection> inspectionList)
        {
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Conn"].ConnectionString))
            {
                using (SqlBulkCopy sbc = new SqlBulkCopy(conn.ConnectionString, SqlBulkCopyOptions.KeepIdentity))
                {
                    conn.Open();

                    // Destination Table
                    sbc.DestinationTableName = "dbo.Inspections2";

                    // Number of records to be processed in one go
                    sbc.BatchSize = inspectionList.Count;

                    // Set the timeout.
                    sbc.BulkCopyTimeout = 120;

                    // Create DataTable and Mappings
                    DataTable dT = new DataTable();
                    var props = TypeDescriptor.GetProperties(typeof(Inspection))
                        //Dirty hack to make sure we only have system data types 
                        //i.e. filter out the relationships/collections
                                     .Cast<PropertyDescriptor>()
                                     .Where(propertyInfo => propertyInfo.PropertyType.Namespace.Equals("System"))
                                     .ToArray();
                    foreach (var propertyInfo in props)
                    {
                        sbc.ColumnMappings.Add(propertyInfo.Name, propertyInfo.Name);
                        dT.Columns.Add(propertyInfo.Name, Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType);
                    }

                    // Copy Values to DataTable
                    var values = new object[props.Length];
                    foreach (var item in inspectionList)
                    {
                        for (var i = 0; i < values.Length; i++)
                        {
                            values[i] = props[i].GetValue(item);
                        }

                        dT.Rows.Add(values);
                    }

                    // Finally write to server
                    sbc.WriteToServer(dT);

                    return 0;
                }
            }
        }

        public static object GetDataValue(object value)
        {
            if (value == null)
            {
                return DBNull.Value;
            }

            return value;
        }

        public static string CleanInspection(string inspectionPage)
        {
            return inspectionPage.StripTagsCharArray().LicNormal();     
        }

        public static ConcurrentBag<Inspection> AddInspectionDateAndNumViolations(ConcurrentBag<Inspection> inspections)
        {
            Parallel.ForEach(inspections, i =>
            {
                var cI = Inspections.CleanInspection(i.Page);
                var regx = new Regex(@"\b\w\w\w\w?\w?\w?\w?\w?\w?\w?\.?\s?\s?\d?\d,\s?\s?\d\d\d\d").Match(cI);

                var startDateString = regx.Value.Replace(".", "").Replace("Sept", "Sep");
                DateTime startDate;
                if (DateTime.TryParse(startDateString, out startDate))
                    i.InspectionStart = startDate;
                else
                    i.InspectionStart = DateTime.MinValue;

                int count = new Regex(@"Standard #").Matches(i.Page).Count;
                i.NumViolations = count;

                Console.WriteLine(i.FacilityLicID + ":" + i.InspectionID + ":" + i.NumViolations);
            }
            );
            return inspections;
        }

    }
}
