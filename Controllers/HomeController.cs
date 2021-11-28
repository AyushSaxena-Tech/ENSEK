using CsvHelper;
using EnsekTest.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace EnsekTest.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private IHostingEnvironment _hostingEnvironment;
        static string projectPath = AppDomain.CurrentDomain.BaseDirectory.Split(new String[] { @"bin\" }, StringSplitOptions.None)[0];
        static IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(projectPath)
            .AddJsonFile("appsettings.json")
            .Build();
        string connectionString = configuration.GetConnectionString("Connection");


        public HomeController(ILogger<HomeController> logger, IHostingEnvironment hostingEnvironment)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        private string verifyAccountID(string AccountId)
        {
            String SQLAccountID = "";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                String SQL = "SELECT [AccountId] FROM [Accounts] where [AccountId] = " + AccountId;
                using (SqlCommand command = new SqlCommand(SQL, connection))
                {
                    using (SqlDataReader SQLreader = command.ExecuteReader())
                    {
                        while (SQLreader.Read())
                        {
                            SQLAccountID = SQLreader["AccountId"].ToString();
                        }
                    }
                }
                connection.Close();
            }
            return SQLAccountID;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost("meter-reading-uploads")]
        public IActionResult dataUpload()
        {
            try
            {
                var file = Request.Form.Files[0];
                string folderName = "Upload";
                string webRootPath = _hostingEnvironment.WebRootPath;
                string newPath = Path.Combine(webRootPath, folderName);
                if (!Directory.Exists(newPath))
                {
                    Directory.CreateDirectory(newPath);
                }
                List<DataErrors> dataErrors = new List<DataErrors>();

                if (file.Length > 0)
                {
                    string fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
                    string fullPath = Path.Combine(newPath, fileName);
                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }

                    var path = $"{Directory.GetCurrentDirectory()}{@"\wwwroot\Upload"}" + "\\" + file.FileName;
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var reader = new StreamReader(path))
                        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                        {
                            csv.Read();
                            csv.ReadHeader();
                            while (csv.Read())
                            {
                                DataErrors dataError = new DataErrors();
                                var meterData = csv.GetRecord<MeterReading>();
                                string SQLAccountID = verifyAccountID(meterData.AccountId);

                                if (SQLAccountID == "")
                                {
                                    dataError.AccountId = meterData.AccountId;
                                    dataError.Desc = "Skipping Record - Cannot Find Account ID in Database";
                                    dataErrors.Add(dataError);
                                }
                                else if (!int.TryParse(meterData.AccountId.ToString(), out _))
                                {
                                    dataError.AccountId = meterData.AccountId;
                                    dataError.Desc = "Skipping Record - Incorrect Value in Account ID";
                                    dataErrors.Add(dataError);
                                }
                                else if (!DateTime.TryParseExact(meterData.MeterReadingDateTime.ToString(), "dd/MM/yyyy hh:mm",CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                                {
                                    dataError.AccountId = meterData.AccountId;
                                    dataError.Desc = "Skipping Record - Incorrect DateTime Format";
                                    dataErrors.Add(dataError);
                                }
                                else if (!int.TryParse(meterData.MeterReadValue.ToString(), out _))
                                {
                                    dataError.AccountId = meterData.AccountId;
                                    dataError.Desc = "Skipping Record - Incorrect Meter Reading data. Data Contains string";
                                    dataErrors.Add(dataError);
                                }
                                else if (meterData.MeterReadValue.ToString().Length != 5)
                                {
                                    dataError.AccountId = meterData.AccountId;
                                    dataError.Desc = "Skipping Record - Incorrect Meter Reading data. Data should be the format 'NNNNN'";
                                    dataErrors.Add(dataError);
                                }
                                else
                                {
                                    string SQLAccountIDDuplicate = "";
                                    String InsertSQLDuplicate = "SELECT [AccountId] FROM [MeterReading] where [AccountId] = " + meterData.AccountId + " and cast(FORMAT([MeterReadingDateTime],'dd/MM/yyyy hh:mm') as varchar) = '" + meterData.MeterReadingDateTime + "'";
                                    using (SqlCommand command = new SqlCommand(InsertSQLDuplicate, connection))
                                    {
                                        using (SqlDataReader SQLreader = command.ExecuteReader())
                                        {
                                            while (SQLreader.Read())
                                            {
                                                SQLAccountIDDuplicate = SQLreader["AccountId"].ToString();
                                            }
                                        }
                                    }

                                    if (SQLAccountIDDuplicate != "")
                                    {
                                        dataError.AccountId = meterData.AccountId;
                                        dataError.Desc = "Skipping Record - Duplicate Entry of Meter Reading - " + meterData.MeterReadValue;
                                        dataErrors.Add(dataError);
                                    }
                                    else
                                    {
                                        string dateTime = DateTime.ParseExact(meterData.MeterReadingDateTime.ToString(), "dd/MM/yyyy hh:mm", CultureInfo.InvariantCulture).ToString("yyyy/MM/dd hh:mm", CultureInfo.InvariantCulture);
                                        String InsertSQL = "INSERT INTO [MeterReading] VALUES (" + meterData.AccountId + ",CONVERT(DATETIME, '" + dateTime + "')," + meterData.MeterReadValue + ")";
                                        using (SqlCommand command = new SqlCommand(InsertSQL, connection))
                                        {
                                            command.ExecuteNonQuery();
                                        }
                                        dataError.AccountId = meterData.AccountId;
                                        dataError.Desc = "Record Inserted";
                                        dataErrors.Add(dataError);
                                    }
                                }
                            }
                        }
                        connection.Close();
                    }
                }
                return Json(JsonConvert.SerializeObject(dataErrors));
            }
            catch (Exception ex)
            {
                return Json("Upload Failed: " + ex.Message);
            }

        }

        [HttpGet("user-accounts/{id}")]
        public IActionResult getUserData(int id)
        {
            try
            {
                GetReturn returnValue = new GetReturn();
                DataErrors dataError = new DataErrors();
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    String SQL = "SELECT [FirstName] ,[LastName], [MeterReadingDateTime], [MeterReadValue] FROM [Accounts], [MeterReading] where [Accounts].AccountId = [MeterReading].AccountId and [Accounts].[AccountId] = " + id;
                    using (SqlCommand command = new SqlCommand(SQL, connection))
                    {
                        using (SqlDataReader SQLreader = command.ExecuteReader())
                        {
                            while (SQLreader.Read())
                            {
                                returnValue.AccountId = id.ToString();
                                returnValue.FirstName = SQLreader["FirstName"].ToString();
                                returnValue.LastName = SQLreader["LastName"].ToString();
                                returnValue.MeterReadingDateTime = SQLreader["MeterReadingDateTime"].ToString();
                                returnValue.MeterReadValue = SQLreader["MeterReadValue"].ToString();
                            }
                        }
                    }
                }
                if (returnValue.AccountId == null)
                {
                    dataError.AccountId = id.ToString();
                    dataError.Desc = "Account ID not in Database";
                    return Json(JsonConvert.SerializeObject(dataError));
                }
                else 
                { 
                    return Json(JsonConvert.SerializeObject(returnValue));
                }
            }
            catch (Exception ex)
            {
                return Json("Error Getting Data: " + ex.Message);
            }
        }

        [HttpPost("user-accounts-create")]
        public IActionResult createUserData([FromBody] UserData userData)
        {
            try
            {
                DataErrors dataError = new DataErrors();
                string SQLAccountID = verifyAccountID(userData.AccountId.ToString());
                if (SQLAccountID != "")
                {
                    dataError.AccountId = userData.AccountId.ToString();
                    dataError.Desc = "Account ID already in Database";
                    return Json(JsonConvert.SerializeObject(dataError));
                }
                if (userData.AccountId.ToString() != null || int.TryParse(userData.AccountId.ToString(), out _) )
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        
                        String SQL = "INSERT INTO [Accounts] VALUES (" + userData.AccountId + ",'" + userData.FirstName + "','" + userData.LastName + "')";
                        using (SqlCommand command = new SqlCommand(SQL, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                    dataError.AccountId = userData.AccountId.ToString();
                    dataError.Desc = "Account ID Created";
                    return Json(JsonConvert.SerializeObject(dataError));
                }
                else
                {
                    dataError.AccountId = userData.AccountId.ToString();
                    dataError.Desc = "Account ID incorrect";
                    return Json(JsonConvert.SerializeObject(dataError));
                }
            }
            catch (Exception ex)
            {
                return Json("Error Getting Data: " + ex.Message);
            }
        }

        [HttpPut("user-accounts-update/{id}")]
        public IActionResult updateUserData(int id,[FromBody] PutData putData)
        {
            try
            {
                DataErrors dataError = new DataErrors();
                string SQLAccountID = verifyAccountID(id.ToString());
                if (SQLAccountID == "")
                {
                    dataError.AccountId = id.ToString();
                    dataError.Desc = "Account ID not in Database";
                    return Json(JsonConvert.SerializeObject(dataError));
                }
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    String SQL = "Update [Accounts] set [FirstName] = '" + putData.FirstName + "', [LastName] = '" + putData.LastName + "' where [AccountId] = " + id;
                    using (SqlCommand command = new SqlCommand(SQL, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                 dataError.AccountId = id.ToString();
                 dataError.Desc = "Data Updated";
                 return Json(JsonConvert.SerializeObject(dataError));
                
            }
            catch (Exception ex)
            {
                return Json("Error Updating Data: " + ex.Message);
            }
        }

        [HttpDelete("user-accounts-delete/{id}")]
        public IActionResult deleteUserData(int id)
        {
            try
            {
                DataErrors dataError = new DataErrors();
                string SQLAccountID = verifyAccountID(id.ToString());
                if (SQLAccountID == "")
                {
                    dataError.AccountId = id.ToString();
                    dataError.Desc = "Account ID not in Database";
                    return Json(JsonConvert.SerializeObject(dataError));
                }
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    String SQL = "Delete [Accounts] where [AccountId] = " + id;
                    using (SqlCommand command = new SqlCommand(SQL, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                dataError.AccountId = id.ToString();
                dataError.Desc = "Data Deleted";
                return Json(JsonConvert.SerializeObject(dataError));
            }
            catch (Exception ex)
            {
                return Json("Error Deleting Data: " + ex.Message);
            }
        }
    }
}
