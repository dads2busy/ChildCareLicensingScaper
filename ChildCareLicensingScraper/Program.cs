using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;

namespace ChildCareLicensingScraper
{
    class Program
    {
        static void Main(string[] args)
        {


                for (int x = 11890; x < 40000; x++)
                {
                    String page = GetWebPage("http://www.dss.virginia.gov/printer/facility/search/cc.cgi?rm=Details;ID=" + x);
                    StorePage(x, page);
                }

            

            //string outPage = UnStorePage(19128);
            //Console.WriteLine(outPage);
        }

        static string GetWebPage(string url)
        {
            var request = WebRequest.Create(url);
            var response = (HttpWebResponse)request.GetResponse();
            Stream dataStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream);
            string responseFromServer = reader.ReadToEnd();
            reader.Close();
            dataStream.Close();
            response.Close();
            return responseFromServer;
            
        }

        static void StorePage(int pageID, string page)
        {
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Conn"].ConnectionString)) 
            {
                conn.Open();
                string queryString = "INSERT INTO dbo.pages (PageID, Page) "
                                   + "VALUES (@PageID, @Page)";
                SqlCommand command = new SqlCommand(queryString, conn);
                command.Parameters.Add("@Page", SqlDbType.Text);
                command.Parameters["@Page"].Value = page;
                command.Parameters.Add("@PageID", SqlDbType.Int);
                command.Parameters["@PageID"].Value = pageID;
                var rows = command.ExecuteNonQuery();
                Console.WriteLine(pageID.ToString());
            }

        }

        static string UnStorePage(int pageID)
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
    }
}
