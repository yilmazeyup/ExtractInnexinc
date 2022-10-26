using System;
using System.Collections.Generic;
using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using Npgsql;
using System.Net;


namespace Scraper
{
    class Program
    {
        public static ScrapingBrowser _browser = new ScrapingBrowser();



        static void Main(string[] args)
        {
            var mainPageLinks = GetMainPageLinks("https://innexinc.com/index.php/lodemore");
            //System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;



            static List<dynamic> GetMainPageLinks(string url)
            {

                var startTime = DateTime.Now;
                int totalProduct = 1733;
                int productPerPage =8;
                int startFrom = 1;
                List<List<string>> categories = new List<List<string>>();

                List<dynamic> linksList = new List<dynamic>();






                

                //Parallel.For(0,totalProduct,startFrom)

                for (int i = 0; startFrom <= totalProduct;)
                {
                    Uri myUri = new Uri($"{url}/?start_from={startFrom}&product_per_page={productPerPage}&total_products={totalProduct}&order=myqtysort&dir=asc", UriKind.Absolute);
                    var html = GetHtml(myUri.ToString());

                    var links = html.InnerHtml.Split("data-proid");
                    int m = 0;
                    //Parallel.ForEach(links,link =>
                    foreach (var link in links)
                    {
                        new ParallelOptions { MaxDegreeOfParallelism = 4 };
                        Product product = new Product();
                        try
                        {
                            Uri detailUrl = new Uri((link.Split("href=")[1].Split("title")[0].Replace("\"", "").Trim().ToString()), UriKind.Absolute);
                            var detailHtml = GetHtml(detailUrl.ToString());
                            try
                            {
                                product.title = detailHtml.InnerHtml.Split("<tbody>")[1].Split("&quot")[0].Split("5\">")[1]?.Split("</td")[0] == null 
                                    ? detailHtml.InnerHtml.Split("<tbody>")[1].Split("&quot")[0].Split("5\">")[1] : detailHtml.InnerHtml.Split("<tbody>")[1].Split("&quot")[0].Split("5\">")[1].Split("</td")[0];
                                product.image = detailHtml.InnerHtml.Split("gallery-image visible\" src=\"")[1].Split("\" alt")[0];
                                product.upcCode = detailHtml.InnerHtml.Split("<tbody>")[1].Split("UPC:")[1].Split("</td")[0].Split("colspan=\"3\">")[1].ToString();
                                product.price = Convert.ToDouble(detailHtml.InnerHtml.Split("price\">$")[1].Split("</span")[0]);
                            }
                            catch (Exception) { }
                            try
                            {
                                product.sku = detailHtml.InnerHtml.Split("<tbody>")[1].Split("SKU:")[1].Split("</td")[0].Split(">")[2];
                                product.brand = detailHtml.InnerHtml.Split("<tbody>")[1].Split("Brand:")[1].Split("</td")[0].Split("colspan=\"5\">")[1];
                                product.platform = detailHtml.InnerHtml.Split("<tbody>")[1].Split("Platform:")[1].Split("</td")[0].Split("colspan=\"5\">")[1];
                                product.license = detailHtml.InnerHtml.Split("<tbody>")[1].Split("License:")[1].Split("</td")[0].Split("colspan=\"5\">")[1];
                                product.size = detailHtml.InnerHtml.Split("<tbody>")[1].Split("Size:")[1].Split("</td")[0].Split(">")[2];
                                product.color = detailHtml.InnerHtml.Split("<tbody>")[1].Split("Color:")[1].Split("</td")[0].Split(">")[2];
                                product.capacity = detailHtml.InnerHtml.Split("<tbody>")[1].Split("Capacity:")[1].Split("</td")[0].Split(">")[2];
                            }
                            catch (Exception) { }

                            Console.WriteLine(product.upcCode + " " + product.title + " " + product.price);
                            linksList.Add(product);
                        }
                        catch (Exception) { }


                        m++;
                    };

                    startFrom = startFrom + productPerPage;

                };



                var endTime = DateTime.Now;
                Console.WriteLine(startTime);
                Console.WriteLine(endTime);


                //The data in the created list is being written to PostgreSQL.
                using (NpgsqlConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=12345;Database=postgres"))
                {
                    connection.Open();
                    NpgsqlCommand cmd = new NpgsqlCommand();
                    cmd.Connection = connection;
                    cmd.CommandText = "truncate table innexinc";
                    cmd.ExecuteNonQuery();
                    linksList.ForEach(x =>
                    {
                        using (var cmd = new NpgsqlCommand(@"insert into innexinc(title,image,sku,brand,license,upccode,platform,productsize,productcolor,productcapacity,price,createdon)
                                    values(:title,:image,:sku,:brand,:license,:upcCode,:platform,:productsize,:productcolor,:productcapacity,:price,current_timestamp)", connection))
                        {
                            cmd.Parameters.AddWithValue("title", x.title?.ToString() == null ? DBNull.Value : x.title?.ToString());
                            cmd.Parameters.AddWithValue("image", x.image?.ToString() == null ? DBNull.Value : x.image?.ToString());
                            cmd.Parameters.AddWithValue("sku", x.sku?.ToString() == null ? DBNull.Value : x.sku?.ToString());
                            cmd.Parameters.AddWithValue("brand", x.brand?.ToString() == null ? DBNull.Value : x.brand?.ToString());
                            cmd.Parameters.AddWithValue("license", x.license?.ToString() == null ? DBNull.Value : x.license?.ToString());
                            cmd.Parameters.AddWithValue("upcCode", x.upcCode?.ToString() == null ? DBNull.Value : x.upcCode.ToString());
                            cmd.Parameters.AddWithValue("platform", x.platform?.ToString() == null ? DBNull.Value : x.platform?.ToString());
                            cmd.Parameters.AddWithValue("productsize", x.size?.ToString() == null ? DBNull.Value : x.size?.ToString());
                            cmd.Parameters.AddWithValue("productcolor", x.color?.ToString() == null ? DBNull.Value : x.color?.ToString());
                            cmd.Parameters.AddWithValue("productcapacity", x.capacity?.ToString() == null ? DBNull.Value : x.capacity?.ToString());
                            cmd.Parameters.AddWithValue("price", Convert.ToDouble(x.price) == null ? DBNull.Value : Convert.ToDouble(x.price));
                            cmd.ExecuteNonQuery();

                        }



                    });
                    cmd.Dispose();
                    connection.Close();
                };
                //Finally, I give the list as output. Anyone can output the data in csv, xls or any other format they want.
                return linksList;
            }
        }

        static HtmlNode GetHtml(string url)
        {
            //_browser.Headers.Add("Keep-Alive", " timeout=5000, max=20000");
            try
            {

                WebPage webpage = _browser.NavigateToPage(new Uri(url));
                return webpage.Html;
            }
            catch 
            {

                WebPage webpage = _browser.NavigateToPage(new Uri(url));
                return webpage.Html;
            }
            
        }

        static HtmlNode GetDetailHtml(string upcUrl)
        {

            WebPage webpage = _browser.NavigateToPage(new Uri(upcUrl));
            return webpage.Html;
        }



        public class Product
        {
            public string? title { get; set; }
            public string? image { get; set; }
            public string? sku { get; set; }
            public string? brand { get; set; }
            public string? license { get; set; }
            public string? upcCode { get; set; }
            public string? platform { get; set; }
            public string? size { get; set; }
            public string? color { get; set; }
            public string? capacity { get; set; }
            public double price { get; set; }

        }
    }
}