using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using DoAn_Ver2.Models;

namespace DoAn_Ver2.Common
{
    public class ConfigHelper
    {
        // Hàm static để gọi trực tiếp từ View: @ConfigHelper.Get("SiteLogo")
        public static string Get(string key)
        {
            using (var db = new DoAn_DbModel()) 
            {
                var config = db.CauHinhChungs.FirstOrDefault(x => x.KeyName == key);
                return config != null ? config.Value : "";
            }
        }
    }
}