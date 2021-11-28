using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EnsekTest.Models
{
    public class UserData
    {
        [Index(0)]
        public int AccountId { get; set; }
        [Index(1)]
        public string FirstName { get; set; }
        [Index(2)]
        public string LastName { get; set; }
    }

    public class DataErrors
    {
        public string AccountId { get; set; }
        public string Desc { get; set; }
    }

    public class MeterReading
    {
        [Index(0)]
        public string AccountId { get; set; }
        [Index(1)]
        public string MeterReadingDateTime { get; set; }
        [Index(2)]
        public string MeterReadValue { get; set; }
    }

    public class GetReturn
    {
        public string AccountId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MeterReadingDateTime { get; set; }
        public string MeterReadValue { get; set; }
    }

    public class PutData
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}
