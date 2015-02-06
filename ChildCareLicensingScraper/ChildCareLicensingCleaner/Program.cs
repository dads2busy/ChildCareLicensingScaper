using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
using System.Configuration;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Transactions;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ChildCareLicensingCleaner
{
    class Program
    {
        static void Main(string[] args)
        {

            //Console.WriteLine(CharUnicodeInfo.GetUnicodeCategory('\n'));
            
            var facilities = new ConcurrentBag<Facility>();

            var pageIDs = GetPageIDs();

            Parallel.ForEach(pageIDs, pageID =>
            {
                var facility = GetFacility(pageID);
                facilities.Add(facility);
                Console.WriteLine(facilities.Count);
            }
            );

            Console.WriteLine("Writing to Database");
            WriteToBase(facilities.ToList<Facility>());
            Console.WriteLine("Finished");
        }

        static Facility GetFacility(int licID)
        {
            Facility facility = new Facility(licID);
            var nonFields = GetCities() + ",(";
            var fields = "Facility Type:,License Type:,Expiration Date:,Administrator:,Business Hours:,Capacity:,Ages:,Inspector:,Qualification:";
            var page = GetPage(licID);
            GetFields(page, fields, nonFields, ref facility);
            GetInspections(page, ref facility);

            return facility;
        }

        static void GetFields(string page, string fields, string nonFields, ref Facility facility)
        {
            string[] fieldArr = fields.Split(',');
            string[] nonFieldArr = nonFields.Split(',');
            page = page.StripTagsCharArray().LicNormal();

            // Truncate front            
            var frontTrunc = page.IndexOf("New Search") + 11;
            if (frontTrunc > 0)
                page = page.Substring(frontTrunc, page.Length - frontTrunc);
            page = page.Substring(new Regex(@"\w").Match(page.Substring(0, 20)).Index, page.Length - new Regex(@"\w").Match(page.Substring(0, 20)).Index);
            // Truncate end
            var endTrunc = page.IndexOf("Inspection");
            if (endTrunc > 0)
                page = page.Substring(0, endTrunc);

            // Split and insert newlines on fields
            foreach (string field in fieldArr)
                page = page.Replace(field, Environment.NewLine + field);
            // Split and insert newlines on nonFields
            foreach (string field in nonFieldArr)
                page = page.Replace(field, Environment.NewLine + field);



            // FIND BY INDEX
            var facilityTypeIndex = page.IndexOf("Facility Type") + 14;
            var licenseTypeIndex = page.IndexOf("License Type") + 13;
            var registrationDurationIndex = page.IndexOf("Registration Duration") + 21;
            var expDateIndex = page.IndexOf("Expiration Date") + 16;
            var qualIndex = page.IndexOf("Qualification");
            var adminIndex = page.IndexOf("Administrator") + 14;
            var busHoursIndex = page.IndexOf("Business Hours") + 15;
            var inspectorIndex = page.IndexOf("Inspector:") + 10;
            var vdssContactIndex = page.IndexOf("VDSS Contact:") + 13;
            var capacityIndex = page.IndexOf("Capacity") + 9;
            var agesIndex = page.IndexOf("Ages:") + 5;

            string facilityType = "No Facility Type";
            if (registrationDurationIndex - 21 > 0)
                facilityType = page.Substring(facilityTypeIndex, (registrationDurationIndex - 21) - facilityTypeIndex);
            else if (facilityTypeIndex - 14 > 0)
                facilityType = page.Substring(facilityTypeIndex, page.IndexOf(Environment.NewLine, facilityTypeIndex) - (facilityTypeIndex));
            facility.FacilityType = facilityType.Replace("\n", "").Replace("\t", ""); ;

            string licenseType = "No License Type";
            if (licenseTypeIndex - 13 > 1)
                licenseType = page.Substring(licenseTypeIndex, page.IndexOf(Environment.NewLine, licenseTypeIndex) - (licenseTypeIndex)).Replace("&nbsp;", "");
            else if (registrationDurationIndex - 21 > 1)
                licenseType = page.Substring(registrationDurationIndex + 1, page.IndexOf(Environment.NewLine, registrationDurationIndex) - (registrationDurationIndex)).Replace("&nbsp;", "");
            facility.LicenseType = licenseType.Replace("\n", "").Replace("\t", "").Replace("\r", "").Trim() ;

            string expDate = "No Date";
            if (expDateIndex - 16 > 0)
                expDate = page.Substring(expDateIndex, page.IndexOf(Environment.NewLine, expDateIndex) - (expDateIndex));
            facility.ExpirationDate = expDate.Replace("\n", "").Replace("\t", ""); ;

            string administrator = "No Administrator";
            if (adminIndex - 14 > 0)
                administrator = page.Substring(adminIndex, page.IndexOf(Environment.NewLine, adminIndex) - (adminIndex));
            facility.Administrator = administrator.Replace("\n", "").Replace("\t", "").Replace("\r", "").Trim();

            string businessHours = "No Hours";
            if (busHoursIndex - 15 > 0)
            {
                var businessHoursString = page.Substring(busHoursIndex, page.IndexOf(Environment.NewLine, busHoursIndex) - (busHoursIndex));
                businessHours = new Regex(@"\b.+\s?\s?\b.+")
                    .Match(businessHoursString)
                    .Value;
            }
            facility.BusinessHours = businessHours.Replace("\n", " ").Replace("\t", " ");

            string capacity = "No Capacity";
            if (capacityIndex - 9 > 0)
                capacity = page.Substring(capacityIndex, page.IndexOf(Environment.NewLine, capacityIndex) - (capacityIndex));
            facility.Capacity = capacity.Replace("\n", "").Replace("\t", ""); ;

            string ages = "No Ages";
            if (agesIndex - 5 > 0)
            {
                if (vdssContactIndex - 13 > 0)
                    ages = page.Substring(agesIndex, (vdssContactIndex - 13) - agesIndex);
                else
                    ages = page.Substring(agesIndex, page.IndexOf(Environment.NewLine, agesIndex) - (agesIndex));
            }
            facility.Ages = ages.Replace("\n", "").Replace("\t", ""); ;

            string inspector = "No Inspector";
            if (inspectorIndex - 10 > 0)
            {
                var newLineIndex = page.IndexOf(Environment.NewLine, inspectorIndex);
                if (newLineIndex > 0)
                    inspector = page.Substring(inspectorIndex, newLineIndex - (inspectorIndex)).Replace("\n", "").Replace("\t", "").Trim();
            }
            else if (vdssContactIndex - 13 > 0)
            {
                var newLineIndex = page.IndexOf(Environment.NewLine, vdssContactIndex);
                if (newLineIndex > 0)
                    inspector = page.Substring(vdssContactIndex, newLineIndex - (vdssContactIndex)).Replace("\n", "").Replace("\t", "").Trim();
            }
            facility.Inspector = inspector;

            // FIND BY PATTERN
            // find City
            var city = new Regex(@"\n+.*[A-Z][A-Z][A-Z]+\b[,]\sVA").Match(page.Substring(15, facilityTypeIndex - 15));
            if (city.Index > 0)
                facility.City = city.Value.Substring(0, city.Length - 4).Replace("\n", "").Replace("\t", "");

            // find Zip
            var zip = new Regex(@"[\s][1-9][0-9][0-9][0-9][0-9]\s*-*").Match(page);
            if (zip.Index > 0)
                facility.Zip = zip.Value.Substring(0,zip.Value.Length-1).Trim();

            // find StreetAddress1
            var address = new Regex(@"\n+\t+[0-9].+\s").Match(page.Substring(0, page.IndexOf(city.Value)));
            if (address.Index > 0)
                facility.StreetAddress1 = address.Value.Substring(0, address.Length).Replace("\n", "").Replace("\t", "").Replace("\r", "").Trim();
            else if (page.IndexOf(city.Value) - new Regex(@"[a-z][A-Z]").Match(page).Index - 2 > 0)
            {
                var noNumberIndex = new Regex(@"\n+.*\s*\w").Match(page).Index;
                var noNumberAddress = page.Substring(noNumberIndex, page.IndexOf(city.Value) - noNumberIndex);
                if (noNumberAddress.Length > 0)
                    facility.StreetAddress1 = noNumberAddress.Replace("\n", "").Replace("\t", "").Replace("\r", "").Trim();
                else
                    facility.StreetAddress1 = "No Address";
            }
            else
                facility.StreetAddress1 = "No Address";

            // find FacilityName
            if (facility.StreetAddress1 != "No Address")
                facility.FacilityName = page.Substring(0, page.IndexOf('\n'));
            else if (city.Index > 0)
                facility.FacilityName = page.Substring(0, city.Index + 15);

            // find FacilityPhone
            var facilityPhone = new Regex(@"\([0-9][0-9][0-9]\) [0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]").Match(page.Substring(0, page.IndexOf("Facility")));
            if (facilityPhone.Index != 0)
                facility.FacilityPhone = facilityPhone.Value;
            else
                facility.FacilityPhone = "No Phone";

            // find InspectorPhone
            var inspectorPhone = new Regex(@"\([0-9][0-9][0-9]\) [0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]").Match(page.Substring(inspectorIndex, page.Length - inspectorIndex));
            if (inspectorPhone.Index != 0)
                facility.InspectorPhone = inspectorPhone.Value;
            else
                facility.InspectorPhone = "No Phone";

        }

        static void GetInspections(string page, ref Facility facility)
        {
            string needle = "Inspection=";
            IEnumerable<int> indexes = page.IndexOfAll(needle);
            IEnumerable<string> strings = indexes.AllStringsFromIndexes(page, needle);
            var hS = new HashSet<string>(strings);

            PropertyInfo InspectPI = facility.GetType().GetProperty("Inspections");
            InspectPI.SetValue(facility, hS.ToList(), null);
        }

        static string GetCities()
        {
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Conn"].ConnectionString))
            {
                conn.Open();
                string queryString = "SELECT ltrim(rtrim(upper(city))) FROM dbo.Cities";
                SqlCommand command = new SqlCommand(queryString, conn);
                var reader = command.ExecuteReader();
                return string.Join(",", reader.AsEnumerable().Select(r => r[0]));
            }
        }

        static string GetPage(int pageID)
        {
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Conn"].ConnectionString))
            {
                conn.Open();
                string queryString = "SELECT Page FROM dbo.pages "
                                   + "WHERE PageID = " + pageID;
                SqlCommand command = new SqlCommand(queryString, conn);
                var reader = command.ExecuteReader();
                string page = "";
                while (reader.Read())
                {
                    page = reader.GetString(0);
                }
                return page;
            }
        }

        static IEnumerable<int> GetPageIDs()
        {
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Conn"].ConnectionString))
            {
                conn.Open();
                string queryString = "SELECT PageID FROM dbo.pages";// WHERE PageID = 37744";
                
                SqlCommand command = new SqlCommand(queryString, conn);
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    yield return reader.GetInt32(reader.GetOrdinal("PageID"));
                }
            }
        }

        static void WriteToBase(List<Facility> programList)
        {
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Conn"].ConnectionString))
            {
                conn.Open();
                using (TransactionScope scope = new TransactionScope())
                {
                    string sqlIns = "INSERT INTO Facilities (FacilityLicID, FacilityName, StreetAddress1, StreetAddress2, City, State, ZIP, FacilityPhone, FacilityType, LicenseType, ExpirationDate, Administrator, BusinessHours, Capacity, Ages, Inspector, InspectorPhone, Inspections) VALUES (@FacilityLicID, @FacilityName, @StreetAddress1, @StreetAddress2, @City, @State, @ZIP, @FacilityPhone, @FacilityType, @LicenseType, @ExpirationDate, @Administrator, @BusinessHours, @Capacity, @Ages, @Inspector, @InspectorPhone, @Inspections)";

                    SqlCommand cmdIns = new SqlCommand(sqlIns, conn);
                    cmdIns.Parameters.Add("@FacilityLicID", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@FacilityName", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@StreetAddress1", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@StreetAddress2", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@City", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@State", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@Zip", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@FacilityPhone", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@FacilityType", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@LicenseType", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@ExpirationDate", System.Data.SqlDbType.Date);
                    cmdIns.Parameters.Add("@Administrator", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@BusinessHours", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@Capacity", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@Ages", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@Inspector", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@InspectorPhone", System.Data.SqlDbType.VarChar);
                    cmdIns.Parameters.Add("@Inspections", System.Data.SqlDbType.VarChar);

                    for (int i = 0; i < programList.Count; i++)
                    {
                        if (programList[i].FacilityName != null)
                        {
                            cmdIns.Parameters["@FacilityLicID"].Value = GetDataValue(programList[i].FacilityLicID);
                            cmdIns.Parameters["@FacilityName"].Value = GetDataValue(programList[i].FacilityName);
                            cmdIns.Parameters["@StreetAddress1"].Value = GetDataValue(programList[i].StreetAddress1);
                            cmdIns.Parameters["@StreetAddress2"].Value = GetDataValue(programList[i].StreetAddress2);
                            cmdIns.Parameters["@City"].Value = GetDataValue(programList[i].City);
                            cmdIns.Parameters["@State"].Value = GetDataValue(programList[i].State);
                            cmdIns.Parameters["@Zip"].Value = GetDataValue(programList[i].Zip);
                            cmdIns.Parameters["@FacilityPhone"].Value = GetDataValue(programList[i].FacilityPhone);
                            cmdIns.Parameters["@FacilityType"].Value = GetDataValue(programList[i].FacilityType);
                            cmdIns.Parameters["@LicenseType"].Value = GetDataValue(programList[i].LicenseType);
                            DateTime expDT = DateTime.MinValue;
                            try { expDT = DateTime.Parse(programList[i].ExpirationDate.Replace(".", "").Replace("Sept", "Sep")); }
                            catch { }
                            cmdIns.Parameters["@ExpirationDate"].Value = GetDataValue(expDT);
                            cmdIns.Parameters["@Administrator"].Value = GetDataValue(programList[i].Administrator);
                            cmdIns.Parameters["@BusinessHours"].Value = GetDataValue(programList[i].BusinessHours);
                            cmdIns.Parameters["@Capacity"].Value = GetDataValue(programList[i].Capacity);
                            cmdIns.Parameters["@Ages"].Value = GetDataValue(programList[i].Ages);
                            cmdIns.Parameters["@Inspector"].Value = GetDataValue(programList[i].Inspector);
                            cmdIns.Parameters["@InspectorPhone"].Value = GetDataValue(programList[i].InspectorPhone);
                            cmdIns.Parameters["@Inspections"].Value = GetDataValue(String.Join(",", programList[i].Inspections.ToArray()));
                            cmdIns.ExecuteNonQuery();
                        }
                    }
                    scope.Complete();
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
    }
}
